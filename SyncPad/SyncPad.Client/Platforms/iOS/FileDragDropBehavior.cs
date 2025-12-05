using SyncPad.Client.ViewModels;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Behaviors;

/// <summary>
/// iOS 端文件拖放行为（移动端不支持拖放）
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
        // iOS 端不支持拖放
    }

    protected override void OnDetachingFrom(View bindable)
    {
        base.OnDetachingFrom(bindable);
    }
}
