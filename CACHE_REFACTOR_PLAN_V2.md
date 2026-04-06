# SyncPad 缓存系统重构计划 V2（服务端 + 客户端）

## 目标

将文件存储改为**内容寻址（CAS）**：服务器以 XXHash64 存储文件体，数据库管理映射关系；客户端以 hash 匹配本地缓存，实现秒传和下载跳过。

## 核心设计

### 服务器存储

**磁盘：** 单一目录，文件以 `{xxhash64hex}.dat` 存储（如 `a1b2c3d4e5f67890.dat`），相同内容只存一份。

**数据库 Files 表：**
| 字段 | 说明 |
|------|------|
| id | 主键 |
| fileName | 用户可见的文件名 |
| fileSize | 文件大小 |
| hash | XXHash64 十六进制（16字符），唯一约束 |
| uploadedAt | 上传时间 |
| expiresAt | 过期时间 |
| userId | 所属用户 |
| status | `active`（正常显示）/ `cached`（已删除，文件体保留） |

**status 与 expiresAt 的关系：**
- `expiresAt` 到期 → 不管 status 是什么，文件都过期清理
- 用户删除文件 → status 改为 `cached`，expiresAt 不变
- 重新上传相同内容 → status 改回 `active`，uploadedAt 和 expiresAt 都更新

---

## 改动清单

### 一、服务器端

#### 1. 数据库迁移

- Files 表新增 `Hash`（string, 16字符）和 `Status`（string, "active"/"cached"）字段
- Hash 字段加唯一索引
- 删除数据库迁移方式：删除旧数据库，重新创建

#### 2. 文件存储改为 hash 命名

**改动文件：** 文件存储服务（如 `FileService.cs` 或 `FileRepository.cs`）

- 文件保存路径从 `{fileId}_{fileName}` 改为 `{hash}.dat`
- 删除文件时不删磁盘文件，只改 status 为 `cached`
- 新增定期清理方法：删除 status=`cached` 且 expiresAt 已过期的文件及其磁盘文件

#### 3. 新增 API：查询 hash 是否存在

**新增接口：** `GET /api/files/check-hash?hash={xxhash64hex}`

**返回：**
```json
{ "exists": true/false, "status": "active"/"cached"/null }
```

#### 4. 修改上传 API（两步上传）

**接口：** `POST /api/files`

**请求改为：**
```json
{ "hash": "a1b2c3d4e5f67890", "fileName": "报告.pdf", "fileSize": 1024 }
```

**服务端逻辑：**
1. 查询 hash 是否已存在：
   - 存在且 status=`cached` → 更新 status=`active`、uploadedAt=now、expiresAt=now+7天、fileName → 返回成功（秒传）
   - 存在且 status=`active` → 检查 fileName 是否相同：
     - 同 fileName → 返回"文件已存在"
     - 不同 fileName → 返回"同名不同文件名的文件已存在，hash 冲突"（实际几乎不会发生）
   - 不存在 → 返回 `{ "needUpload": true }`
2. 客户端收到 `needUpload=true` 后，再发送文件体（multipart/form-data，带 hash 字段）
3. 服务端收到文件体 → 计算 XXHash64 验证 → 保存为 `{hash}.dat` → 创建数据库记录

#### 5. 修改文件列表 API

**接口：** `GET /api/files`

**返回的 FileItemDto 新增 hash 字段：**
```json
{
  "id": 1,
  "fileName": "报告.pdf",
  "fileSize": 1024,
  "hash": "a1b2c3d4e5f67890",
  "uploadedAt": "...",
  "expiresAt": "..."
}
```

只返回 status=`active` 且未过期的文件。

#### 6. 修改删除 API

**接口：** `DELETE /api/files/{id}`

- 不删磁盘文件，只改 status 为 `cached`
- 不删数据库记录

#### 7. 新增服务器端 XXHash64 计算

- 使用 `System.IO.Hashing` 中的 `XxHash64`（.NET 内置，无需 NuGet 包）
- 上传文件体时服务端也计算 hash，与客户端提供的 hash 比对验证

#### 8. 文件过期清理（可选）

- 应用启动时或定时任务：清理 status=`cached` 且 expiresAt 已过期的记录，删除对应磁盘文件

---

### 二、客户端 - macOS 端（SyncPad.Apple）

#### 1. FileModels.swift

- `FileItemDto` 新增 `hash: String` 字段

#### 2. FileCacheManager.swift

- `findCachedFile` 改为按完整 hash 匹配（不再是按 fileName 后缀匹配）
- 新增 `findCachedFile(hash: String) -> URL?`
- 下载流程：先从 FileItemDto 获取 hash → 本地查缓存 → 未命中再下载

#### 3. PadViewModel.swift

- `uploadFile` 改为两步：先计算本地文件 hash → 发送检查请求 → 需要上传才传文件体
- `downloadFile` 改为：先获取 hash → 查本地缓存 → 未命中再下载

#### 4. 上传前计算 hash

- 选择文件后，先计算 XXHash64，再调用 API

---

### 三、客户端 - MAUI 端（SyncPad.Client）

#### 1. FileItemDto 共享模型

- `SyncPad.Shared` 的 `FileItemDto` 新增 `Hash` 属性

#### 2. FileCacheManager.cs

- `FindCachedFile` 改为按完整 hash 匹配

#### 3. PadViewModel.cs

- 上传改为两步（同 macOS 端逻辑）
- 下载改为先查 hash 缓存

---

## 实施顺序

1. **SyncPad.Shared**：FileItemDto 新增 Hash 字段
2. **SyncPad.Server**：数据库改造（新增 Hash、Status 字段，删除旧数据库）
3. **SyncPad.Server**：文件存储改为 `{hash}.dat`
4. **SyncPad.Server**：新增 check-hash API
5. **SyncPad.Server**：修改上传 API（两步上传 + 秒传）
6. **SyncPad.Server**：修改文件列表 API（返回 hash，只返回 active 文件）
7. **SyncPad.Server**：修改删除 API（软删除，改 status）
8. **SyncPad.Server**：服务端 hash 计算验证
9. **macOS 端**：FileItemDto 新增 hash、FileCacheManager 改为 hash 匹配、上传两步化、下载先查缓存
10. **MAUI 端**：同步修改
11. **两端构建测试**

## 测试要点

- **秒传**：上传一个已存在（cached）的文件 → 秒传成功，不传文件体
- **正常上传**：上传新文件 → 服务端计算 hash 验证 → 保存成功
- **hash 冲突**：上传同名不同内容 → 正常保存（hash 不同）
- **下载跳过**：本地已有缓存 → 不下载，直接使用
- **删除后重新上传**：删除文件 → 重新上传相同内容 → 秒传，uploadedAt 和 expiresAt 更新
- **过期清理**：cached 且过期的文件被清理
