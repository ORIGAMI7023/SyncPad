using SyncPad.Client.ViewModels;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Behaviors;

/// <summary>
/// 文件拖放行为，用于为文件项设置拖出支持
/// 注意：Mac 端禁用内部拖动排序，仅 Windows 端支持
/// </summary>
public class FileDragDropBehavior : Behavior<View>
{
    private View? _associatedObject;

    // 静态变量：存储当前拖动的文件（用于内部拖动）
    private static SelectableFileItem? _currentDraggedItem;

    /// <summary>
    /// 公开当前拖动的文件项（供外部访问）
    /// </summary>
    public static SelectableFileItem? CurrentDraggedItem
    {
        get => _currentDraggedItem;
        set => _currentDraggedItem = value;
    }

    /// <summary>
    /// 是否启用拖放功能（Mac 端禁用）
    /// </summary>
    public static bool IsDragDropEnabled
    {
        get
        {
#if MACCATALYST
            return false;
#else
            return true;
#endif
        }
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _associatedObject = bindable;

#if WINDOWS
        bindable.HandlerChanged += OnHandlerChanged;
#endif
    }

    protected override void OnDetachingFrom(View bindable)
    {
#if WINDOWS
        bindable.HandlerChanged -= OnHandlerChanged;
#endif
        _associatedObject = null;
        base.OnDetachingFrom(bindable);
    }

#if WINDOWS
    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (_associatedObject?.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement uiElement)
        {
            SetupWindowsDragDrop(uiElement);
        }
    }

    private void SetupWindowsDragDrop(Microsoft.UI.Xaml.UIElement uiElement)
    {
        // 设置 Windows 原生拖放支持，用于内部拖动排序
        uiElement.CanDrag = true;

        // 启用 DragStarting 事件
        uiElement.DragStarting += OnWindowsDragStarting;
    }

    private void OnWindowsDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        // 获取 ViewModel
        var page = GetParentPage(_associatedObject);
        if (page?.BindingContext is not ViewModels.PadViewModel viewModel)
            return;

        var targetItem = _associatedObject?.BindingContext as SelectableFileItem;

        // 检查是否是内部拖动
        if (_currentDraggedItem != null && targetItem != null && _currentDraggedItem != targetItem)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.DragUIOverride.Caption = "移动到此位置";
            e.Handled = true;
        }
    }

    private async void OnWindowsDrop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        // 获取 ViewModel
        var page = GetParentPage(_associatedObject);
        if (page?.BindingContext is not ViewModels.PadViewModel viewModel)
            return;

        var targetItem = _associatedObject?.BindingContext as SelectableFileItem;
        if (targetItem == null)
        {
            return;
        }

        // 检查是否是内部拖动
        if (_currentDraggedItem != null && _currentDraggedItem != targetItem)
        {
            await viewModel.SwapFilePositionsAsync(_currentDraggedItem, targetItem);
            e.Handled = true;
        }

        // 清除拖动状态
        _currentDraggedItem = null;
    }

    private async void OnWindowsDragStarting(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.DragStartingEventArgs args)
    {
        // 获取 ViewModel
        var page = GetParentPage(_associatedObject);
        if (page?.BindingContext is not ViewModels.PadViewModel viewModel)
            return;

        if (_associatedObject?.BindingContext is not SelectableFileItem fileItem)
            return;

        // 设置当前拖动的文件
        _currentDraggedItem = fileItem;

        // 设置拖动数据标记，表示这是一个内部拖动
        args.Data.SetText($"SyncPad-Internal-Drag-{fileItem.Id}");
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

        // 设置拖动视觉效果：显示正在拖动的文件图标
        try
        {
            // 使用文件图标作为拖动时的预览
            args.DragUI.SetContentFromDataPackage();
        }
        catch
        {
            // 如果设置预览失败，忽略错误继续拖动
        }

        // 检查文件是否已缓存，如果已缓存，支持拖出到系统
        if (fileItem.Status == FileStatus.Cached)
        {
            var cachedFilePath = GetCachedFilePath(fileItem);
            if (!string.IsNullOrEmpty(cachedFilePath) && System.IO.File.Exists(cachedFilePath))
            {
                try
                {
                    var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(cachedFilePath);
                    args.Data.SetStorageItems(new[] { storageFile });
                }
                catch
                {
                    // 设置拖出失败，忽略错误
                }
            }
        }
    }

    private string? GetCachedFilePath(SelectableFileItem fileItem)
    {
        // 从 Page 获取 ViewModel
        var page = GetParentPage(_associatedObject);
        if (page?.BindingContext is ViewModels.PadViewModel viewModel)
        {
            return viewModel.GetCachedFilePath(fileItem);
        }
        return null;
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
#endif
}
