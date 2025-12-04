namespace SyncPad.Shared.Models;

/// <summary>
/// 文本同步消息 DTO
/// </summary>
public class TextSyncMessage
{
    /// <summary>
    /// 文本内容
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// 更新时间戳（UTC）
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 发送者用户 ID（用于避免回显给发送者）
    /// </summary>
    public int SenderId { get; set; }
}
