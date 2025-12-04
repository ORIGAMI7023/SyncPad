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

## 🚧 Phase 2: UI 重构为资源管理器风格（待实现）

### 目标
将文件区域从简单列表改造为类似 Windows 资源管理器的图标视图，提供更直观的文件管理体验。

### UI 设计

#### 桌面端布局（MAUI + Web）
```
┌─────────────────────────────────────────────────┐
│  [📤 上传]  [🔄 刷新]  [🗑️ 清空缓存]          │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌──────┐  ┌──────┐  ┌──────┐  ┌──────┐       │
│  │ 📄   │  │ 📷   │  │ 📁   │  │ ⬇️   │       │
│  │file1 │  │img   │  │doc   │  │下载中│       │
│  │ 云端 │  │已缓存│  │ 云端 │  │ 45%  │       │
│  │ 2.5MB│  │ 1.2MB│  │ 5.1MB│  │ 3.8MB│       │
│  └──────┘  └──────┘  └──────┘  └──────┘       │
│                                                 │
└─────────────────────────────────────────────────┘
```

#### 移动端布局
- 网格视图（2 列）
- 图标 + 文件名 + 状态
- 长按多选

### 实现要点

#### 1. 文件图标系统
- **图标映射**：根据 MimeType 或扩展名显示对应图标
  - 文档类：📄 .txt, .doc, .docx, .pdf
  - 图片类：📷 .jpg, .png, .gif, .bmp
  - 视频类：🎬 .mp4, .avi, .mkv
  - 音频类：🎵 .mp3, .wav, .flac
  - 压缩包：📦 .zip, .rar, .7z
  - 代码类：💻 .cs, .js, .py, .java
  - 未知类：📎 其他

- **状态指示器**：叠加显示
  - 云端：☁️ 角标
  - 下载中：进度环
  - 已缓存：✓ 角标
  - 错误：❌ 角标

#### 2. MAUI 实现
```xaml
<!-- CollectionView 改为 GridView -->
<CollectionView ItemsLayout="VerticalGrid, 4">
    <CollectionView.ItemTemplate>
        <DataTemplate>
            <Grid Padding="10">
                <!-- 文件图标 + 状态叠加 -->
                <Grid WidthRequest="80" HeightRequest="80">
                    <Label Text="{Binding FileIcon}" FontSize="48"/>
                    <!-- 状态角标 -->
                    <Label Text="{Binding StatusBadge}" FontSize="20"
                           HorizontalOptions="End" VerticalOptions="Start"/>
                    <!-- 进度环（下载中时显示）-->
                    <ProgressBar Progress="{Binding DownloadProgress}"
                                 IsVisible="{Binding IsDownloading}"/>
                </Grid>

                <!-- 文件名 -->
                <Label Text="{Binding FileName}" LineBreakMode="MiddleTruncation"/>

                <!-- 文件大小 -->
                <Label Text="{Binding FileSizeText}" FontSize="Small"/>

                <!-- 状态文本 -->
                <Label Text="{Binding StatusText}" FontSize="Small"/>
            </Grid>
        </DataTemplate>
    </CollectionView.ItemTemplate>
</CollectionView>
```

#### 3. Web 实现
```razor
<div class="file-grid">
    @foreach (var file in Files)
    {
        <div class="file-card @(file.IsSelected ? "selected" : "")"
             @onclick="() => ToggleSelection(file)">

            <div class="file-icon-container">
                <span class="file-icon">@GetFileIcon(file.MimeType)</span>
                <span class="status-badge">@GetStatusBadge(file.Status)</span>

                @if (file.IsDownloading)
                {
                    <div class="progress-ring">
                        <svg viewBox="0 0 36 36">
                            <path d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831"
                                  stroke-dasharray="@file.DownloadProgress, 100"/>
                        </svg>
                    </div>
                }
            </div>

            <div class="file-name">@file.FileName</div>
            <div class="file-size">@FormatFileSize(file.FileSize)</div>
            <div class="file-status">@file.StatusText</div>
        </div>
    }
</div>

<style>
.file-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
    gap: 16px;
}

.file-card {
    border: 1px solid #ddd;
    border-radius: 8px;
    padding: 12px;
    cursor: pointer;
    transition: all 0.2s;
}

.file-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.1);
}

.file-icon-container {
    position: relative;
    width: 80px;
    height: 80px;
}

.file-icon {
    font-size: 48px;
}

.status-badge {
    position: absolute;
    top: 0;
    right: 0;
    font-size: 20px;
}
</style>
```

#### 4. ViewModel 扩展
```csharp
public class SelectableFileItem : BaseViewModel
{
    // 新增计算属性
    public string FileIcon => GetFileIcon(MimeType);
    public string StatusBadge => Status switch
    {
        FileStatus.Remote => "☁️",
        FileStatus.Cached => "✓",
        FileStatus.Error => "❌",
        _ => ""
    };

    public string FileSizeText => FormatFileSize(FileSize);

    private string GetFileIcon(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return "📎";

        return mimeType switch
        {
            _ when mimeType.StartsWith("image/") => "📷",
            _ when mimeType.StartsWith("video/") => "🎬",
            _ when mimeType.StartsWith("audio/") => "🎵",
            "application/pdf" => "📄",
            "application/zip" => "📦",
            _ => "📎"
        };
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }
}
```

### 开发步骤
1. [ ] 扩展 SelectableFileItem 添加图标和格式化方法
2. [ ] 重构 MAUI PadPage.xaml 为网格布局
3. [ ] 重构 Web Pad.razor 为网格布局
4. [ ] 实现图标映射逻辑
5. [ ] 实现状态指示器（角标、进度环）
6. [ ] 适配深色模式
7. [ ] 测试响应式布局（桌面/移动端）

---

## 🎯 Phase 3: 桌面端拖拽功能（后续）

### 拖入上传
- [ ] MAUI: 实现 Drop 事件处理
- [ ] Web: 实现 HTML5 ondrop 事件
- [ ] 支持多文件同时拖入
- [ ] 显示拖拽悬浮提示

### 拖出导出
- [ ] MAUI: 实现 Drag 事件处理
- [ ] Web: 实现 HTML5 ondragstart 事件
- [ ] 从 tmp 复制到目标位置
- [ ] 可选 Shift+拖出 = 移动（删除服务器文件）

### 用户设置
- [ ] 拖出默认行为配置（复制/移动）
- [ ] 自动清理缓存策略
- [ ] 预载带宽阈值配置（预留）

---

## 📋 Phase 4: 智能预载（远期规划）

### 带宽检测
- [ ] 实现网络速度测试
- [ ] 动态判断是否启用预载

### 预载策略
- [ ] 实现 CachedPartial 状态
- [ ] 低带宽部分预载（如前 5MB）
- [ ] 用户可配置预载阈值

### 后台同步
- [ ] SignalR 推送预载建议
- [ ] 后台下载队列管理

---

## 技术债务和优化

### 性能优化
- [ ] 大文件上传进度显示
- [ ] 上传取消功能
- [ ] 下载取消和恢复功能
- [ ] 虚拟化滚动（大量文件时）

### 用户体验
- [ ] 文件预览功能（图片、PDF）
- [ ] 搜索和过滤
- [ ] 排序选项（名称、大小、日期）
- [ ] 文件详情面板

### 安全和稳定性
- [ ] 上传文件类型白名单
- [ ] 病毒扫描集成（可选）
- [ ] 缓存容量限制和清理策略
- [ ] 网络异常重试机制

---

## 开发注意事项

1. **避免过度工程**：按优先级实施，MVP 优先
2. **保持简洁**：只实现明确需求，不添加假设功能
3. **数据库迁移**：每次数据库更新直接删除旧库重建
4. **测试覆盖**：每个 Phase 完成后进行完整测试

---

生成时间：2025-12-04
当前分支：main
最新提交：e555f1e feat: 实现文件暂存区 Phase 1 - 核心下载架构
