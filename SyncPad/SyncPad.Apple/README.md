# SyncPad Apple 客户端

使用 Swift/SwiftUI 构建的原生 iOS 和 macOS 客户端。

## 项目结构

```
SyncPad.Apple/
├── Shared/                 # iOS/Mac 共享代码 (~85%)
│   ├── Models/             # 数据模型
│   ├── Services/           # 网络服务
│   ├── ViewModels/         # MVVM ViewModel
│   └── Views/              # 共享 UI 组件
├── iOS/                    # iOS 专属代码
├── macOS/                  # macOS 专属代码
└── Resources/              # 资源文件
```

## 在 Xcode 中创建项目

由于 Xcode 项目文件 (.xcodeproj) 是二进制格式，需要手动创建：

### 方法 1：使用 Xcode 创建新项目

1. 打开 Xcode
2. File → New → Project
3. 选择 "Multiplatform" → "App"
4. 填写项目信息：
   - Product Name: `SyncPad`
   - Team: 选择你的开发团队
   - Organization Identifier: `net.origami7023`
   - Interface: SwiftUI
   - Language: Swift
5. 保存位置选择 `SyncPad.Apple` 目录
6. 删除 Xcode 自动生成的文件
7. 将现有的 Swift 文件拖入项目

### 方法 2：手动添加文件到现有项目

1. 在 Xcode 中创建空项目
2. 删除默认生成的文件
3. 将 `Shared/`、`iOS/`、`macOS/` 文件夹拖入项目
4. 配置 Target Membership：
   - `Shared/` 下所有文件：同时属于 iOS 和 macOS Target
   - `iOS/` 下文件：仅属于 iOS Target
   - `macOS/` 下文件：仅属于 macOS Target

## Target 配置

### iOS Target
- Deployment Target: iOS 15.0
- Bundle Identifier: `net.origami7023.syncpad`
- Main: `iOS/SyncPadApp.swift`

### macOS Target
- Deployment Target: macOS 12.0
- Bundle Identifier: `net.origami7023.syncpad`
- Main: `macOS/SyncPadApp.swift`
- App Sandbox: 启用
  - Network: Outgoing Connections (Client)
  - File Access: User Selected File (Read/Write)

## 功能清单

### iOS
- [x] 登录/登出
- [x] 文本实时同步
- [x] 文件列表查看
- [x] 文件上传（通过文件选择器）
- [x] 文件下载和预览
- [x] iPhone: TabView 布局
- [x] iPad: 分栏布局

### macOS
- [x] 登录/登出
- [x] 文本实时同步
- [x] 文件列表查看
- [x] 文件上传（通过文件选择器）
- [x] 文件下载和预览
- [x] 文件拖入上传
- [x] 文件拖出到 Finder
- [x] HSplitView 分栏布局
- [x] 原生菜单栏

## 服务器连接

连接到: `https://syncpad.origami7023.net.cn`

- REST API: `/api/auth/login`, `/api/text`, `/api/files`
- WebSocket (SignalR): `/hubs/text`

## 注意事项

1. **Keychain 权限**：macOS 需要在 Entitlements 中配置 Keychain 访问权限
2. **网络权限**：需要在 Info.plist 中配置 App Transport Security
3. **签名**：iOS 需要有效的开发者证书
