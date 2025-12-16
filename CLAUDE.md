- 本项目尽量避免使用数据库迁移，每次更新数据库直接删除旧的即可，以节省token和减少bug。
- 本项目的解决方案文件在 D:\Programing\C#\SyncPad\SyncPad\SyncPad.slnx , D:\Programing\C#\SyncPad 目录中主要存放项目文件夹 \SyncPad , 相关文档,claude和git的配置文件
- mac 端：Mac Catalyst 始终连接生产服务器 https://syncpad.origami7023.net.cn。
- SyncPad.Apple 为 ios 和 mac 使用的项目文件夹，而不是 SyncPad.Client （现在 ios 和 mac 不再使用 maui 部署）

## Swift/macOS 项目构建

### 无密码构建方式（推荐）
使用 build.sh 脚本可以避免 Xcode 钥匙串密码提示（3次）：
```bash
cd SyncPad/SyncPad.Apple
./build.sh          # Debug 构建
./build.sh release  # Release 构建
```

### Xcode 构建方式
在 Xcode 中直接运行（Cmd+R）会提示输入钥匙串密码3次。如需避免，使用上述脚本构建。