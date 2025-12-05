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

                // 计算网格位置
                var (targetX, targetY) = CalculateGridPosition(x, y);

                // 更新文件位置
                await _viewModel.UpdateFilePositionAsync(fileId, targetX, targetY);

                await Task.Delay(300);

                await _viewModel.RefreshFilesAsync();
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

            // 检查是否存在同名文件
            bool exists = await _viewModel.FileExistsAsync(storageFile.Name);

            if (exists)
            {
                var confirm = await DisplayAlert("文件已存在",
                    $"文件 \"{storageFile.Name}\" 已存在，是否覆盖？",
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
    private (int X, int Y) CalculateGridPosition(double x, double y)
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

        return (column, row);
    }

    /// <summary>
    /// 计算 Drop 目标的网格坐标（MAUI 事件）
    /// </summary>
    private (int X, int Y) CalculateDropTargetPosition(DropEventArgs e)
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

            // 网格布局：4 列
            const int columns = 4;

            // 每个网格单元大小（包括间距）
            const double cellWidth = 120;
            const double cellHeight = 120;

            // 计算列和行
            int column = (int)(dropPoint.Value.X / cellWidth);
            int row = (int)(dropPoint.Value.Y / cellHeight);

            // 确保列在有效范围内
            if (column < 0) column = 0;
            if (column >= columns) column = columns - 1;
            if (row < 0) row = 0;

            return (column, row);
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
