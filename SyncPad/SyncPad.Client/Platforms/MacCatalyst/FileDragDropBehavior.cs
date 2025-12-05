using SyncPad.Client.ViewModels;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Behaviors;

/// <summary>
/// Mac Catalyst 端文件拖放行为（禁用内部拖动排序）
/// </summary>
public class FileDragDropBehavior : Behavior<View>
{
    private static SelectableFileItem? _currentDraggedItem;

    public static SelectableFileItem? CurrentDraggedItem
    {
        get => _currentDraggedItem;
        set => _currentDraggedItem = value;
    }

    public static bool IsDragDropEnabled => false;

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        // Mac 端不启用拖放功能
    }

    protected override void OnDetachingFrom(View bindable)
    {
        base.OnDetachingFrom(bindable);
    }
}
