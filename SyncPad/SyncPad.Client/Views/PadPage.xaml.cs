using SyncPad.Client.ViewModels;
using SyncPad.Client.Behaviors;
using SyncPad.Shared.Models;
#if WINDOWS
using SyncPad.Client.Platforms.Windows;
using Windows.Storage;
#endif
#if MACCATALYST
using SyncPad.Client.Platforms.MacCatalyst;
#endif

namespace SyncPad.Client.Views;

public partial class PadPage : ContentPage
{
    private readonly PadViewModel _viewModel;
    private SelectableFileItem? _draggedItem;

    // 选择相关
    private int _lastSelectedIndex = -1;
    private bool _isCtrlPressed;
    private bool _isShiftPressed;

    public PadPage(PadViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        viewModel.LogoutRequested += OnLogoutRequested;

#if WINDOWS
        // 设置 Windows 拖放支持
        SetupWindowsDragDrop();
#endif

#if MACCATALYST
        // 设置 Mac 拖放支持
        SetupMacDragDrop();
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnLogoutRequested()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }

#if WINDOWS
    private void SetupWindowsDragDrop()
    {
        // 为外层 Grid 设置拖放支持（处理空状态）
        DragDropHandler.SetupDropTarget(
            FileAreaGrid,
            onFilesDropped: async (files) =>
            {
                foreach (var file in files)
                {
                    await UploadStorageFileAsync(file);
                }
            },
            onInternalDrop: null, // 外层不处理内部拖动
            onDragOver: null,
            onDragLeave: null
        );

        // 为文件区域设置拖入支持（支持外部文件拖入和内部文件重排）
        DragDropHandler.SetupDropTarget(
            FileGridView,
            // 外部文件拖入回调
            onFilesDropped: async (files) =>
            {
                foreach (var file in files)
                {
                    await UploadStorageFileAsync(file);
                }
            },
            // 内部拖放回调
            onInternalDrop: async (fileId, x, y) =>
            {
                // 查找被拖动的文件
                var draggedFile = _viewModel.Files.FirstOrDefault(f => f.Id == fileId);

                // 计算网格位置（排除被拖动文件自身，避免碰撞检测误判）
                var (targetX, targetY) = CalculateGridPosition(x, y, fileId);

                System.Diagnostics.Debug.WriteLine($"[拖放] 文件 {fileId} 拖到 ({x:F1}, {y:F1}) -> 最终位置 ({targetX}, {targetY})");

                // 更新文件位置（会触发 SignalR 回调，自动使用动画更新）
                await _viewModel.UpdateFilePositionAsync(fileId, targetX, targetY);

                // 不再刷新文件列表，让 SignalR 回调处理更新（会触发动画）
            },
            // DragOver 回调（显示指示器）
            onDragOver: (x, y) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FileGridView.ShowDropIndicator(x, y);
                });
            },
            // DragLeave 回调（隐藏指示器）
            onDragLeave: () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FileGridView.HideDropIndicator();
                });
            }
        );
    }

    private async Task UploadStorageFileAsync(StorageFile storageFile)
    {
        try
        {
            using var stream = await storageFile.OpenStreamForReadAsync();
            var contentType = storageFile.ContentType ?? "application/octet-stream";

            // 检查是否存在同名文件（服务器端检查）
            bool exists = await _viewModel.FileExistsAsync(storageFile.Name);

            if (exists)
            {
                var confirm = await DisplayAlert("文件已存在",
                    $"服务器上已存在文件 \"{storageFile.Name}\"，是否覆盖？\n\n" +
                    $"（如果看不到该文件，请尝试刷新页面）",
                    "覆盖", "取消");
                if (!confirm)
                    return;
            }

            await _viewModel.UploadFileAsync(storageFile.Name, stream, contentType, exists);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
        }
    }
#endif

#if MACCATALYST
    private void SetupMacDragDrop()
    {
        // 为文件区域设置拖入支持（从 Finder 拖入文件）
        DragDropHandler.SetupDropTarget(
            FileGridView,
            onFilesDropped: async (filePaths) =>
            {
                foreach (var filePath in filePaths)
                {
                    await UploadFileFromPathAsync(filePath);
                }
            }
        );
    }

    private async Task UploadFileFromPathAsync(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var contentType = GetMimeType(filePath);

            // 检查是否存在同名文件
            bool exists = await _viewModel.FileExistsAsync(fileName);

            if (exists)
            {
                var confirm = await DisplayAlert("文件已存在",
                    $"文件 \"{fileName}\" 已存在，是否覆盖？",
                    "覆盖", "取消");
                if (!confirm)
                    return;
            }

            using var stream = File.OpenRead(filePath);
            await _viewModel.UploadFileAsync(fileName, stream, contentType, exists);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
        }
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            _ => "application/octet-stream"
        };
    }
#endif

    #region 内部文件拖放排序

    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
#if MACCATALYST
        // Mac 端禁用内部拖动排序
        e.Cancel = true;
        return;
#endif

        if (sender is VisualElement visual && visual.BindingContext is SelectableFileItem item)
        {
            _draggedItem = item;
            e.Data.Properties["FileItem"] = item;

            // 设置拖动时的视觉效果
            visual.Opacity = 0.5;

#if WINDOWS
            // 如果文件已缓存，设置为可拖出
            if (item.Status == FileStatus.Cached)
            {
                var filePath = _viewModel.GetCachedFilePath(item);
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    e.Data.Properties["CachedFilePath"] = filePath;
                }
            }
#endif
        }
    }

    private void OnDropCompleted(object? sender, DropCompletedEventArgs e)
    {
#if MACCATALYST
        return;
#endif

        if (sender is VisualElement visual)
        {
            visual.Opacity = 1.0;
        }
        _draggedItem = null;
    }

    private void OnCardDragOver(object? sender, DragEventArgs e)
    {
#if MACCATALYST
        // Mac 端禁用内部拖动排序
        return;
#endif

        if (sender is Element element && element.BindingContext is SelectableFileItem targetItem)
        {
            // 如果是内部拖动且不是拖动到自己
            if (_draggedItem != null && _draggedItem != targetItem)
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                // 高亮目标
                if (sender is VisualElement visual)
                {
                    visual.BackgroundColor = Color.FromRgba(0, 120, 212, 0.1);
                }
            }
        }
    }

    private async void OnCardDrop(object? sender, DropEventArgs e)
    {
#if MACCATALYST
        // Mac 端禁用内部拖动排序
        return;
#endif

        if (sender is VisualElement visual)
        {
            // 恢复背景色
            visual.BackgroundColor = Colors.Transparent;
        }

        if (_draggedItem == null)
        {
            return;
        }

        if (sender is Element element && element.BindingContext is SelectableFileItem targetItem)
        {
            if (_draggedItem == targetItem)
            {
                return;
            }

            // 交换位置
            await _viewModel.SwapFilePositionsAsync(_draggedItem, targetItem);
        }

        _draggedItem = null;
    }

    #endregion

    #region 外部文件拖入上传（MAUI 标准方式，作为备用）

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        // 检查是否是内部拖动
        if (FileDragDropBehavior.CurrentDraggedItem != null)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnFileDrop(object? sender, DropEventArgs e)
    {
        var properties = e.Data?.Properties;
        if (properties == null)
        {
            return;
        }

        // 检查是否是内部拖动（通过静态变量）
        if (FileDragDropBehavior.CurrentDraggedItem != null)
        {
            // 计算目标网格位置（X,Y坐标）
            var (targetX, targetY) = CalculateDropTargetPosition(e);
            var draggedItem = FileDragDropBehavior.CurrentDraggedItem;

            if (targetX >= 0 && targetY >= 0)
            {
                // 直接更新到服务器的新坐标
                await _viewModel.UpdateFilePositionAsync(draggedItem.Id, targetX, targetY);

                // 刷新文件列表
                await Task.Delay(300);
                await _viewModel.RefreshFilesAsync();
            }

            // 清除拖动状态
            FileDragDropBehavior.CurrentDraggedItem = null;
            _draggedItem = null;
            return;
        }

        // 检查是否是内部拖动（MAUI 方式，备用）
        if (properties.ContainsKey("FileItem"))
        {
            // 内部拖动到空白处，忽略
            _draggedItem = null;
            return;
        }

        // Windows 平台通过 DragDropHandler 处理，这里不需要额外处理
    }

    /// <summary>
    /// 从原始坐标计算网格位置（用于 Windows 原生拖放）
    /// </summary>
    private (int X, int Y) CalculateGridPosition(double x, double y, int? excludeFileId = null)
    {
        const int columns = 4;
        const double cellWidth = 120;
        const double cellHeight = 120;

        int column = (int)(x / cellWidth);
        int row = (int)(y / cellHeight);

        // 限制列范围
        if (column < 0) column = 0;
        if (column >= columns) column = columns - 1;
        if (row < 0) row = 0;

        // 检查碰撞并找到最近的空位置
        var finalPosition = FindNearestEmptyPosition(column, row, excludeFileId);
        return finalPosition;
    }

    /// <summary>
    /// 检查指定位置是否被占用（排除指定文件ID）
    /// </summary>
    private bool IsPositionOccupied(int x, int y, int? excludeFileId = null)
    {
        return _viewModel.Files.Any(f =>
            f.PositionX == x &&
            f.PositionY == y &&
            (!excludeFileId.HasValue || f.Id != excludeFileId.Value));
    }

    /// <summary>
    /// 从目标位置开始搜索最近的空位置（优先向右）
    /// </summary>
    private (int X, int Y) FindNearestEmptyPosition(int targetX, int targetY, int? excludeFileId = null)
    {
        const int maxColumns = 4;

        // 如果目标位置本身就是空的，直接返回
        if (!IsPositionOccupied(targetX, targetY, excludeFileId))
        {
            return (targetX, targetY);
        }

        // 搜索策略：优先向右，然后向左，再向下，最后向上
        int maxSearchRadius = 20;

        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            // 1. 优先搜索同一行右侧
            for (int dx = 1; dx <= radius; dx++)
            {
                int checkX = targetX + dx;
                if (checkX < maxColumns && !IsPositionOccupied(checkX, targetY, excludeFileId))
                {
                    return (checkX, targetY);
                }
            }

            // 2. 搜索同一行左侧
            for (int dx = 1; dx <= radius; dx++)
            {
                int checkX = targetX - dx;
                if (checkX >= 0 && !IsPositionOccupied(checkX, targetY, excludeFileId))
                {
                    return (checkX, targetY);
                }
            }

            // 3. 搜索下方行（优先右侧）
            for (int dy = 1; dy <= radius; dy++)
            {
                int checkY = targetY + dy;

                // 从目标列开始向右搜索
                for (int dx = 0; dx <= radius; dx++)
                {
                    int checkX = targetX + dx;
                    if (checkX < maxColumns && !IsPositionOccupied(checkX, checkY, excludeFileId))
                    {
                        return (checkX, checkY);
                    }
                }

                // 再向左搜索
                for (int dx = 1; dx <= radius; dx++)
                {
                    int checkX = targetX - dx;
                    if (checkX >= 0 && !IsPositionOccupied(checkX, checkY, excludeFileId))
                    {
                        return (checkX, checkY);
                    }
                }
            }

            // 4. 搜索上方行（优先右侧）
            for (int dy = 1; dy <= radius; dy++)
            {
                int checkY = targetY - dy;
                if (checkY < 0) continue;

                // 从目标列开始向右搜索
                for (int dx = 0; dx <= radius; dx++)
                {
                    int checkX = targetX + dx;
                    if (checkX < maxColumns && !IsPositionOccupied(checkX, checkY, excludeFileId))
                    {
                        return (checkX, checkY);
                    }
                }

                // 再向左搜索
                for (int dx = 1; dx <= radius; dx++)
                {
                    int checkX = targetX - dx;
                    if (checkX >= 0 && !IsPositionOccupied(checkX, checkY, excludeFileId))
                    {
                        return (checkX, checkY);
                    }
                }
            }
        }

        // 如果搜索失败，返回列表末尾的下一个空位置
        int maxY = _viewModel.Files.Any() ? _viewModel.Files.Max(f => f.PositionY) : 0;

        // 从末尾开始找第一个空位
        for (int y = maxY; y <= maxY + 5; y++)
        {
            for (int x = 0; x < maxColumns; x++)
            {
                if (!IsPositionOccupied(x, y, excludeFileId))
                {
                    return (x, y);
                }
            }
        }

        return (0, maxY + 1);
    }

    /// <summary>
    /// 计算 Drop 目标的网格坐标（MAUI 事件）
    /// </summary>
    private (int X, int Y) CalculateDropTargetPosition(DropEventArgs e, int? excludeFileId = null)
    {
        try
        {
            // 获取 FileGridView
            var gridView = FileGridView;
            if (gridView == null)
            {
                return (-1, -1);
            }

            // 获取 Drop 位置（相对于 FileGridView）
            var dropPoint = e.GetPosition(gridView);
            if (dropPoint == null)
            {
                return (-1, -1);
            }

            // 每个网格单元大小（包括间距）
            const double cellWidth = 120;
            const double cellHeight = 120;

            // 使用统一的碰撞检测逻辑
            return CalculateGridPosition(dropPoint.Value.X, dropPoint.Value.Y, excludeFileId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"计算目标位置失败: {ex.Message}");
            return (-1, -1);
        }
    }

    #endregion

    #region 文件选择（单击/Ctrl/Shift）

    private void OnFileCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Element element && element.BindingContext is SelectableFileItem item)
        {
            var currentIndex = _viewModel.Files.IndexOf(item);

            if (_isCtrlPressed)
            {
                // Ctrl + 单击：切换选中状态
                item.IsSelected = !item.IsSelected;
                _lastSelectedIndex = item.IsSelected ? currentIndex : -1;
            }
            else if (_isShiftPressed && _lastSelectedIndex >= 0)
            {
                // Shift + 单击：范围选择
                var startIndex = Math.Min(_lastSelectedIndex, currentIndex);
                var endIndex = Math.Max(_lastSelectedIndex, currentIndex);

                // 先清除所有选择
                foreach (var f in _viewModel.Files)
                {
                    f.IsSelected = false;
                }

                // 选中范围内的文件
                for (int i = startIndex; i <= endIndex; i++)
                {
                    _viewModel.Files[i].IsSelected = true;
                }
            }
            else
            {
                // 普通单击：清除其他选择，仅选中当前
                foreach (var f in _viewModel.Files)
                {
                    f.IsSelected = false;
                }
                item.IsSelected = true;
                _lastSelectedIndex = currentIndex;
            }

            _viewModel.NotifySelectionChanged();
        }
    }

    private void OnFileAreaTapped(object? sender, TappedEventArgs e)
    {
        // 点击空白区域，清除所有选择
        _viewModel.ClearAllSelection();
    }

    #endregion

    #region 移动端标签页切换

    private void OnTextTabClicked(object? sender, EventArgs e)
    {
        if (TextTabContent != null && FileTabContent != null &&
            TextTabButton != null && FileTabButton != null)
        {
            // 切换内容可见性
            TextTabContent.IsVisible = true;
            FileTabContent.IsVisible = false;

            // 更新按钮样式 - 文本标签页激活
            TextTabButton.FontAttributes = FontAttributes.Bold;
            FileTabButton.FontAttributes = FontAttributes.None;

            // 根据主题更新背景色
            var isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            TextTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#1E1E1E") : Colors.White;
            FileTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#2B2B2B") : Color.FromArgb("#E9ECEF");
        }
    }

    private void OnFileTabClicked(object? sender, EventArgs e)
    {
        if (TextTabContent != null && FileTabContent != null &&
            TextTabButton != null && FileTabButton != null)
        {
            // 切换内容可见性
            TextTabContent.IsVisible = false;
            FileTabContent.IsVisible = true;

            // 更新按钮样式 - 文件标签页激活
            TextTabButton.FontAttributes = FontAttributes.None;
            FileTabButton.FontAttributes = FontAttributes.Bold;

            // 根据主题更新背景色
            var isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            TextTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#2B2B2B") : Color.FromArgb("#E9ECEF");
            FileTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#1E1E1E") : Colors.White;
        }
    }

    #endregion

    #region 文本编辑事件

    /// <summary>
    /// 处理 Editor 的 TextChanged 事件，确保粘贴等操作能触发同步
    /// </summary>
    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        // 确保 ViewModel 的 Content 属性与 Editor 的实际文本同步
        // 这样可以修复移动端长按粘贴后不同步的问题
        if (sender is Editor editor && !string.IsNullOrEmpty(e.NewTextValue))
        {
            // 只有当新值与 ViewModel 的值不同时才更新
            // 避免触发循环更新
            if (_viewModel.Content != e.NewTextValue)
            {
                _viewModel.Content = e.NewTextValue;
            }
        }
    }

    #endregion

    // 框选功能待后续实现，MAUI CollectionView 不易获取项目位置

    #region 键盘事件（用于检测 Ctrl/Shift）

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if WINDOWS
        if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement uiElement)
        {
            uiElement.KeyDown += OnKeyDown;
            uiElement.KeyUp += OnKeyUp;
        }
#endif
    }

#if WINDOWS
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Control)
            _isCtrlPressed = true;
        else if (e.Key == Windows.System.VirtualKey.Shift)
            _isShiftPressed = true;
    }

    private void OnKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Control)
            _isCtrlPressed = false;
        else if (e.Key == Windows.System.VirtualKey.Shift)
            _isShiftPressed = false;
    }
#endif

    #endregion
}
