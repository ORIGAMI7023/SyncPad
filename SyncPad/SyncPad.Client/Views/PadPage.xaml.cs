using SyncPad.Client.ViewModels;
using SyncPad.Client.Behaviors;
using SyncPad.Shared.Models;
#if WINDOWS
using SyncPad.Client.Platforms.Windows;
using Windows.Storage;
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
                _viewModel.AddDebugLog($"[Windows原生] 内部拖放: FileId={fileId}, 像素坐标=({x:F1},{y:F1})");

                // 查找被拖动的文件
                var draggedFile = _viewModel.Files.FirstOrDefault(f => f.Id == fileId);
                if (draggedFile != null)
                {
                    _viewModel.AddDebugLog($"[Windows原生] 拖动文件: {draggedFile.FileName}, 原位置=({draggedFile.PositionX},{draggedFile.PositionY})");
                }

                // 计算网格位置
                var (targetX, targetY) = CalculateGridPosition(x, y);
                _viewModel.AddDebugLog($"[Windows原生] 计算目标网格位置: ({targetX},{targetY})");

                // 更新文件位置
                _viewModel.AddDebugLog($"[Windows原生] 调用 UpdateFilePositionAsync...");
                await _viewModel.UpdateFilePositionAsync(fileId, targetX, targetY);

                _viewModel.AddDebugLog($"[Windows原生] 等待300ms后刷新...");
                await Task.Delay(300);

                _viewModel.AddDebugLog($"[Windows原生] 调用 RefreshFilesAsync...");
                await _viewModel.RefreshFilesAsync();

                _viewModel.AddDebugLog($"[Windows原生] 位置更新完成，新位置应该是 ({targetX},{targetY})");
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

    #region 内部文件拖放排序

    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is VisualElement visual && visual.BindingContext is SelectableFileItem item)
        {
            _draggedItem = item;
            e.Data.Properties["FileItem"] = item;

            _viewModel.AddDebugLog($"开始拖动: {item.FileName}");

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
        _viewModel.AddDebugLog($"拖动完成");

        if (sender is VisualElement visual)
        {
            visual.Opacity = 1.0;
        }
        _draggedItem = null;
    }

    private void OnCardDragOver(object? sender, DragEventArgs e)
    {
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

                _viewModel.AddDebugLog($"拖动经过: {targetItem.FileName}");
            }
        }
    }

    private async void OnCardDrop(object? sender, DropEventArgs e)
    {
        _viewModel.AddDebugLog($"Drop事件触发: _draggedItem={_draggedItem?.FileName ?? "null"}");

        if (sender is VisualElement visual)
        {
            // 恢复背景色
            visual.BackgroundColor = Colors.Transparent;
        }

        if (_draggedItem == null)
        {
            _viewModel.AddDebugLog("Drop取消: _draggedItem为null");
            return;
        }

        if (sender is Element element && element.BindingContext is SelectableFileItem targetItem)
        {
            _viewModel.AddDebugLog($"Drop目标: {targetItem.FileName}");

            if (_draggedItem == targetItem)
            {
                _viewModel.AddDebugLog("Drop取消: 拖动到自己");
                return;
            }

            // 交换位置
            _viewModel.AddDebugLog("准备调用SwapFilePositionsAsync");
            await _viewModel.SwapFilePositionsAsync(_draggedItem, targetItem);
        }
        else
        {
            _viewModel.AddDebugLog("Drop取消: 无法获取目标元素或BindingContext");
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
            _viewModel.AddDebugLog($"OnFileDragOver触发 (内部拖动)");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else
        {
            _viewModel.AddDebugLog($"OnFileDragOver触发 (外部文件)");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnFileDrop(object? sender, DropEventArgs e)
    {
        _viewModel.AddDebugLog($"OnFileDrop触发, sender={sender?.GetType().Name}");

        var properties = e.Data?.Properties;
        if (properties == null)
        {
            _viewModel.AddDebugLog("OnFileDrop: properties为null");
            return;
        }

        _viewModel.AddDebugLog($"OnFileDrop: properties包含 {properties.Count} 项");

        // 检查是否是内部拖动（通过静态变量）
        if (FileDragDropBehavior.CurrentDraggedItem != null)
        {
            _viewModel.AddDebugLog($"检测到内部拖动: {FileDragDropBehavior.CurrentDraggedItem.FileName}");

            // 计算目标网格位置（X,Y坐标）
            var (targetX, targetY) = CalculateDropTargetPosition(e);
            var draggedItem = FileDragDropBehavior.CurrentDraggedItem;

            _viewModel.AddDebugLog($"拖放目标: Position=({targetX},{targetY})");

            if (targetX >= 0 && targetY >= 0)
            {
                // 直接更新到服务器的新坐标
                await _viewModel.UpdateFilePositionAsync(draggedItem.Id, targetX, targetY);
                _viewModel.AddDebugLog($"文件已移动: {draggedItem.FileName} -> ({targetX},{targetY})");

                // 刷新文件列表
                await Task.Delay(300);
                await _viewModel.RefreshFilesAsync();
            }
            else
            {
                _viewModel.AddDebugLog($"目标位置无效: ({targetX},{targetY})");
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

        _viewModel.AddDebugLog($"坐标转换: ({x:F1},{y:F1}) -> 网格({column},{row})");
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
                _viewModel.AddDebugLog("FileGridView 为 null");
                return (-1, -1);
            }

            // 获取 Drop 位置（相对于 FileGridView）
            var dropPoint = e.GetPosition(gridView);
            if (dropPoint == null)
            {
                _viewModel.AddDebugLog("无法获取 Drop 位置");
                return (-1, -1);
            }

            _viewModel.AddDebugLog($"Drop 位置: X={dropPoint.Value.X:F1}, Y={dropPoint.Value.Y:F1}");

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

            _viewModel.AddDebugLog($"计算结果: X={column}, Y={row}");

            return (column, row);
        }
        catch (Exception ex)
        {
            _viewModel.AddDebugLog($"计算目标位置失败: {ex.Message}");
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

    #region 手动移动文件（调试用）

    private async void OnManualMoveFile(object? sender, EventArgs e)
    {
        try
        {
            // 获取输入的目标位置
            if (!int.TryParse(TargetPositionEntry.Text, out int targetIndex))
            {
                _viewModel.AddDebugLog("请输入有效的数字索引");
                return;
            }

            // 获取选中的文件
            var selectedFile = _viewModel.Files.FirstOrDefault(f => f.IsSelected);
            if (selectedFile == null)
            {
                _viewModel.AddDebugLog("请先选择一个文件");
                return;
            }

            var currentIndex = _viewModel.Files.IndexOf(selectedFile);

            _viewModel.AddDebugLog($"手动移动: {selectedFile.FileName}");
            _viewModel.AddDebugLog($"  当前索引: {currentIndex}");
            _viewModel.AddDebugLog($"  目标索引: {targetIndex}");

            // 验证目标索引
            if (targetIndex < 0)
            {
                _viewModel.AddDebugLog($"  错误: 目标索引不能为负数");
                return;
            }

            if (currentIndex == targetIndex)
            {
                _viewModel.AddDebugLog("  无需移动: 已在目标位置");
                return;
            }

            // 如果目标索引在现有范围内，执行本地移动
            if (targetIndex < _viewModel.Files.Count)
            {
                _viewModel.Files.Move(currentIndex, targetIndex);
                _viewModel.AddDebugLog($"  本地移动完成: {currentIndex} -> {targetIndex}");
            }
            else
            {
                // 目标索引超出范围，只更新服务器，不更新本地 UI
                _viewModel.AddDebugLog($"  目标索引超出当前范围 ({_viewModel.Files.Count})，仅更新服务器");
            }

            // 更新服务器（无论目标索引是否超出范围）
            await _viewModel.UpdateFilePositionAsync(selectedFile.Id, targetIndex, 0);
            _viewModel.AddDebugLog($"  服务器更新完成");

            // 刷新文件列表以查看最终结果
            _viewModel.AddDebugLog($"  正在刷新文件列表...");
            await Task.Delay(500); // 等待服务器处理

            // 触发刷新
            await _viewModel.RefreshFilesAsync();

            // 清空输入框
            TargetPositionEntry.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _viewModel.AddDebugLog($"手动移动失败: {ex.Message}");
        }
    }

    #endregion
}
