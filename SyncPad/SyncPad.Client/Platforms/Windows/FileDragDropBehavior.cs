using SyncPad.Client.ViewModels;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Behaviors;

/// <summary>
/// Windows 端文件拖放行为，支持文件拖动排序和拖出到系统
/// </summary>
public class FileDragDropBehavior : Behavior<View>
{
    private View? _associatedObject;
    private static SelectableFileItem? _currentDraggedItem;

    public static SelectableFileItem? CurrentDraggedItem
    {
        get => _currentDraggedItem;
        set => _currentDraggedItem = value;
    }

    public static bool IsDragDropEnabled => true;

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _associatedObject = bindable;
        bindable.HandlerChanged += OnHandlerChanged;
    }

    protected override void OnDetachingFrom(View bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        _associatedObject = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (_associatedObject?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement uiElement)
        {
            SetupWindowsDragDrop(uiElement);
        }
    }

    private void SetupWindowsDragDrop(Microsoft.UI.Xaml.UIElement uiElement)
    {
        uiElement.CanDrag = true;
        uiElement.DragStarting += OnWindowsDragStarting;
    }

    private async void OnWindowsDragStarting(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs args)
    {
        var page = GetParentPage(_associatedObject);
        if (page?.BindingContext is not PadViewModel viewModel)
            return;

        if (_associatedObject?.BindingContext is not SelectableFileItem fileItem)
            return;

        _currentDraggedItem = fileItem;

        args.Data.SetText($"SyncPad-Internal-Drag-{fileItem.Id}");
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

        try
        {
            args.DragUI.SetContentFromDataPackage();
        }
        catch
        {
            // 忽略预览设置失败
        }

        // 如果文件已缓存，支持拖出到系统
        if (fileItem.Status == FileStatus.Cached)
        {
            var cachedFilePath = viewModel.GetCachedFilePath(fileItem);
            if (!string.IsNullOrEmpty(cachedFilePath) && System.IO.File.Exists(cachedFilePath))
            {
                try
                {
                    var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(cachedFilePath);
                    args.Data.SetStorageItems(new[] { storageFile });
                }
                catch
                {
                    // 忽略拖出设置失败
                }
            }
        }
    }

    private static Page? GetParentPage(Element? element)
    {
        while (element != null)
        {
            if (element is Page page)
                return page;
            element = element.Parent;
        }
        return null;
    }
}
