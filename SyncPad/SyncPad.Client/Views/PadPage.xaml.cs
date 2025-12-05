using SyncPad.Client.ViewModels;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Views;

public partial class PadPage : ContentPage
{
    private readonly PadViewModel _viewModel;
    private SelectableFileItem? _draggedItem;

    public PadPage(PadViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        viewModel.LogoutRequested += OnLogoutRequested;
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

    #region 内部文件拖放排序

    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is VisualElement visual && visual.BindingContext is SelectableFileItem item)
        {
            _draggedItem = item;
            e.Data.Properties["FileItem"] = item;

            // 设置拖动时的视觉效果
            visual.Opacity = 0.5;
        }
    }

    private void OnDropCompleted(object? sender, DropCompletedEventArgs e)
    {
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
            }
        }
    }

    private async void OnCardDrop(object? sender, DropEventArgs e)
    {
        if (sender is VisualElement visual)
        {
            // 恢复背景色
            visual.BackgroundColor = Colors.Transparent;
        }

        if (_draggedItem == null)
            return;

        if (sender is Element element && element.BindingContext is SelectableFileItem targetItem)
        {
            if (_draggedItem == targetItem)
                return;

            // 交换位置
            await _viewModel.SwapFilePositionsAsync(_draggedItem, targetItem);
        }

        _draggedItem = null;
    }

    #endregion

    #region 外部文件拖入上传

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        // 检查是否有文件
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnFileDrop(object? sender, DropEventArgs e)
    {
        var properties = e.Data?.Properties;
        if (properties == null)
            return;

        // 检查是否是内部拖动
        if (properties.ContainsKey("FileItem"))
        {
            // 内部拖动到空白处，忽略
            _draggedItem = null;
            return;
        }

        // 处理外部文件拖入
        // 在 Windows 上，可以通过 StorageItems 获取文件
#if WINDOWS
        await HandleWindowsFileDropAsync(e);
#endif
    }

#if WINDOWS
    private async Task HandleWindowsFileDropAsync(DropEventArgs e)
    {
        try
        {
            var dataPackageView = e.Data;
            if (dataPackageView == null)
                return;

            // 获取存储项
            if (dataPackageView.Properties.ContainsKey("StorageItems"))
            {
                var items = dataPackageView.Properties["StorageItems"] as IEnumerable<object>;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item is Windows.Storage.StorageFile storageFile)
                        {
                            await UploadStorageFileAsync(storageFile);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"处理文件拖入失败: {ex.Message}");
        }
    }

    private async Task UploadStorageFileAsync(Windows.Storage.StorageFile storageFile)
    {
        try
        {
            using var stream = await storageFile.OpenStreamForReadAsync();
            var contentType = storageFile.ContentType ?? "application/octet-stream";

            // 检查是否存在同名文件
            bool overwrite = await _viewModel.FileExistsAsync(storageFile.Name);

            if (overwrite)
            {
                var confirm = await DisplayAlert("文件已存在",
                    $"文件 \"{storageFile.Name}\" 已存在，是否覆盖？",
                    "覆盖", "取消");
                if (!confirm)
                    return;
            }

            await _viewModel.UploadFileAsync(storageFile.Name, stream, contentType, overwrite);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
        }
    }
#endif

    #endregion
}
