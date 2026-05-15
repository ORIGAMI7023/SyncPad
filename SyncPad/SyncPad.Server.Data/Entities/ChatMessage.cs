using SyncPad.Shared.Models;

namespace SyncPad.Server.Data.Entities;

/// <summary>
/// 聊天消息实体
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// 雪花ID（分布式唯一标识）
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// 端到端加密的内容（Base64编码）
    /// </summary>
    public string? EncryptedContent { get; set; }

    /// <summary>
    /// 关联的文件ID（文件消息时使用）
    /// </summary>
    public int? FileItemId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 是否已软删除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 编辑时间（预留字段）
    /// </summary>
    public DateTime? EditedAt { get; set; }

    // 导航属性
    public User? User { get; set; }
    public FileItem? FileItem { get; set; }
}
