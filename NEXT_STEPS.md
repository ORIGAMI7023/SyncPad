# SyncPad 文件暂存区开发计划

## ✅ Phase 1 已完成（2025-12-04）

### 后端优化
- [x] Range 请求支持（断点续传）
- [x] 软删除优化（7 天保留期）
- [x] 秒传机制（基于 hash 去重）

### 客户端核心架构
- [x] FileStatus 枚举定义
- [x] FileCacheManager 服务实现
- [x] 下载进度追踪
- [x] tmp 目录缓存管理
- [x] 状态同步到 UI

### 下载体验
- [x] 检查缓存状态
- [x] 按需下载到 tmp
- [x] 实时进度显示
- [x] 下载完成自动打开
- [x] 删除时清理缓存

---

## 🚧 Phase 2: 资源管理器风格 + 拖动排序 + 预载系统（待实现）

### 🎯 核心目标
1. 将文件区域重构为类似 Windows 资源管理器的图标网格视图
2. 支持桌面端拖动排序（跨设备同步）
3. 实现智能预载系统（后台低速预载 + 点击全速下载）

---

### 📐 网格系统设计

#### 坐标规则
| 规则 | 设定 |
|------|------|
| 最大列数 | **8 列**（固定） |
| 坐标系统 | `(x: 0-7, y: 0-∞)` |
| 空位处理 | **保留空位**，不自动整理 |
| 新文件位置 | **第一个空位**（左优先、上优先） |
| 窗口缩小 | 超出列**折叠到下一行**显示 |
| 移动端 | **自动排列**，忽略位置数据，不支持拖动 |

#### 窗口缩小时的折叠显示
```
原始 8 列布局（存储坐标）：
[0,0] [1,0] [2,0] [3,0] [4,0] [5,0] [6,0] [7,0]
[0,1] [1,1] ...

窗口缩小到 4 列时显示：
[0,0] [1,0] [2,0] [3,0]
[4,0] [5,0] [6,0] [7,0]  ← 第一行后半部分折叠
[0,1] [1,1] ...          ← 第二行继续
```

---

### 🔄 文件状态系统（四状态）

```csharp
public enum FileStatus
{
    Remote,           // 未预载 - 仅元数据同步，无本地缓存
    PreloadPending,   // 预载排队中 - 等待预载
    Preloading,       // 正在预载 - 后台低速下载中
    Cached            // 预载完成 - 本地已有完整文件
}
```

#### 状态指示器
| 状态 | 角标 | 进度条 |
|------|------|--------|
| Remote | ☁️ | 无 |
| PreloadPending | 🕐 | 无 |
| Preloading | 无 | 显示进度（条形/圆形） |
| Cached | ✓ | 无 |

#### 状态转换流程
```
远程文件上传 → Remote（仅图标同步）
                ↓ (网络 > 5Mbps & 队列未满)
          PreloadPending（等待预载）
                ↓
           Preloading（5Mbps 限速下载，显示进度）
                ↓
             Cached（预载完成）

用户点击文件 → 从任何状态直接转为 Preloading（全速下载）
            → 完成后自动打开文件
```

---

### 🌐 预载逻辑

#### 自动预载触发条件
- 网络速度检测 > 5Mbps
- 后台预载队列未满（最多同时 2 个文件）
- 文件大小 < 阈值（如 < 100MB）

#### 预载参数
| 参数 | 默认值 |
|------|--------|
| 网络阈值 | 5 Mbps |
| 预载限速 | 5 Mbps (625 KB/s) |
| 同时预载数 | 2 |
| 文件大小上限 | 100 MB |

#### 用户点击触发
- 立即全速下载（无限速）
- 优先级高于后台预载
- 显示进度条
- 完成后自动打开文件

---

### 🖼️ UI 设计

#### 桌面端布局（支持拖动排序）
```
┌─────────────────────────────────────────────────┐
│  [📤 上传]  [🔄 刷新]                            │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐       │
│  │ 📄   │  │ 📷   │  │ 📁   │  │ 🎬   │       │
│  │ ☁️   │  │  ✓   │  │ 🕐   │  │ ██▓░ │       │
│  │file1 │  │img   │  │doc   │  │video │       │
│  │ 2.5MB│  │ 1.2MB│  │ 5.1MB│  │ 3.8MB│       │
│  └──────┘  └──────┘  └──────┘  └──────┘       │
│                                                 │
│  （可拖动排序，位置跨设备同步）                    │
└─────────────────────────────────────────────────┘
```

#### 移动端布局（自动排列，不支持拖动）
```
┌─────────────────┐
│ [📤] [🔄]       │
├─────────────────┤
│ ┌─────┐ ┌─────┐ │
│ │ 📄  │ │ 📷  │ │
│ │ ☁️  │ │  ✓  │ │
│ │file │ │img  │ │
│ └─────┘ └─────┘ │
│                 │
│ （2列自动排列） │
└─────────────────┘
```

#### 文件图标映射
| 类型 | 图标 | 扩展名 |
|------|------|--------|
| 文档 | 📄 | .txt, .doc, .docx, .pdf |
| 图片 | 📷 | .jpg, .png, .gif, .bmp, .webp |
| 视频 | 🎬 | .mp4, .avi, .mkv, .mov |
| 音频 | 🎵 | .mp3, .wav, .flac, .aac |
| 压缩包 | 📦 | .zip, .rar, .7z, .tar.gz |
| 代码 | 💻 | .cs, .js, .py, .java, .ts |
| 未知 | 📎 | 其他 |

---

### 🔧 技术实现

#### 数据库变更（FileItem 实体扩展）
```csharp
public class FileItem
{
    // ... 现有字段 ...

    // 新增位置字段
    public int PositionX { get; set; } = 0;  // 网格列位置 (0-7)
    public int PositionY { get; set; } = 0;  // 网格行位置 (0-∞)
}
```

#### SignalR 扩展
```csharp
public interface ITextHubClient
{
    // ... 现有方法 ...

    // 新增：文件位置变化通知
    Task FilePositionChanged(int fileId, int positionX, int positionY);
}
```

#### 预载服务接口
```csharp
public interface IPreloadService
{
    Task<double> MeasureNetworkSpeedAsync();           // 测速（返回 Mbps）
    Task StartPreloadQueueAsync();                      // 启动后台预载队列
    Task StopPreloadQueueAsync();                       // 停止预载队列
    Task PreloadFileAsync(int fileId, bool fullSpeed); // 预载单个文件
    Task CancelPreloadAsync(int fileId);                // 取消预载
    event Action<int, long, long>? PreloadProgress;     // 进度回调 (fileId, downloaded, total)
}
```

#### ViewModel 扩展
```csharp
public class SelectableFileItem : BaseViewModel
{
    // 位置属性
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // 计算属性
    public string FileIcon => GetFileIcon(MimeType);
    public string StatusBadge => Status switch
    {
        FileStatus.Remote => "☁️",
        FileStatus.PreloadPending => "🕐",
        FileStatus.Cached => "✓",
        _ => ""
    };
    public bool ShowProgress => Status == FileStatus.Preloading;
    public string FileSizeText => FormatFileSize(FileSize);
}
```

---

### 📝 开发任务清单

#### 数据层
- [ ] FileItem 实体添加 PositionX, PositionY 字段
- [ ] 更新 DbContext 配置
- [ ] 重建数据库（删除旧库）

#### 共享层
- [ ] 重构 FileStatus 枚举（四状态）
- [ ] FileItemDto 添加 PositionX, PositionY 字段

#### 服务层
- [ ] IFileService 添加 UpdateFilePositionAsync 方法
- [ ] IFileService 添加 GetNextAvailablePosition 方法（查找第一个空位）
- [ ] 新建 IPreloadService / PreloadService（测速、限速、队列管理）
- [ ] 实现带宽限速逻辑（5Mbps = 625KB/s）

#### SignalR
- [ ] ITextHubClient 添加 FilePositionChanged 方法
- [ ] TextHub 添加 UpdateFilePosition 方法
- [ ] TextHub 广播位置变更

#### 客户端 ViewModel
- [ ] SelectableFileItem 添加位置属性和计算属性
- [ ] PadViewModel 添加预载逻辑
- [ ] PadViewModel 添加位置更新和同步逻辑
- [ ] PadViewModel 处理 FilePositionChanged 事件

#### MAUI UI
- [ ] PadPage.xaml 重构为 8 列固定网格
- [ ] 实现拖放排序（DragGestureRecognizer + DropGestureRecognizer）
- [ ] 实现窗口缩小时的折叠显示
- [ ] 进度条组件
- [ ] 深色模式适配
- [ ] 移动端检测 + 自动排列模式

#### Web UI
- [ ] Pad.razor 重构为 CSS Grid 网格
- [ ] 实现 HTML5 Drag & Drop 排序
- [ ] 实现响应式折叠显示
- [ ] SVG 圆形进度条
- [ ] 深色模式适配
- [ ] 移动端检测 + 自动排列模式

#### 测试
- [ ] 多端位置同步测试
- [ ] 预载逻辑测试
- [ ] 窗口缩放测试
- [ ] 网络限速测试

---

## 🎯 Phase 3: 拖入/拖出系统（后续）

### 拖入上传（从系统文件管理器拖入）
- [ ] MAUI: 实现 Drop 事件处理
- [ ] Web: 实现 HTML5 ondrop 事件
- [ ] 支持多文件同时拖入
- [ ] 显示拖拽悬浮提示
- [ ] 自动分配位置（第一个空位）

### 拖出导出（从 SyncPad 拖到系统）
- [ ] MAUI: 实现 Drag 事件处理
- [ ] Web: 实现 HTML5 ondragstart 事件
- [ ] 从 tmp 复制到目标位置
- [ ] 默认行为：复制（保留服务器文件）
- [ ] Shift+拖出 = 移动（删除服务器文件）

### 用户设置
- [ ] 拖出默认行为配置（复制/移动）
- [ ] 自动清理缓存策略
- [ ] 预载参数配置（网络阈值、限速、同时预载数）

---

## 📋 Phase 4: 优化和扩展（远期规划）

### 性能优化
- [ ] 大文件上传进度显示
- [ ] 上传/下载取消功能
- [ ] 虚拟化滚动（大量文件时）

### 用户体验
- [ ] 文件预览功能（图片、PDF）
- [ ] 搜索和过滤
- [ ] 排序选项（名称、大小、日期）

### 安全和稳定性
- [ ] 上传文件类型白名单
- [ ] 缓存容量限制和清理策略
- [ ] 网络异常重试机制

---

## 开发注意事项

1. **避免过度工程**：按优先级实施，MVP 优先
2. **保持简洁**：只实现明确需求，不添加假设功能
3. **数据库迁移**：每次数据库更新直接删除旧库重建
4. **测试覆盖**：每个 Phase 完成后进行完整测试

---

更新时间：2025-12-05
当前分支：main
