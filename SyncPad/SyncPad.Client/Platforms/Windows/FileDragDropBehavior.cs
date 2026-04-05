namespace SyncPad.Client.Behaviors;

/// <summary>
/// Windows 端文件拖放行为（已禁用内部拖放排序）
/// </summary>
public class FileDragDropBehavior : Behavior<View>
{
    public static SelectableFileItem? CurrentDraggedItem { get; set; }
    public static bool IsDragDropEnabled => false;

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        base.OnDetachingFrom(bindable);
    }
}
