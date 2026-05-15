namespace SyncPad.Shared.Models;

/// <summary>
/// 聊天消息DTO
/// </summary>
public class ChatMessageDto
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string? EncryptedContent { get; set; }
    public int? FileItemId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? EditedAt { get; set; }

    // 文件信息（如果是文件消息）
    public FileItemDto? FileInfo { get; set; }
}

/// <summary>
/// 消息类型枚举（对应服务端）
/// </summary>
public enum MessageType
{
    Text = 0,
    File = 1
}

/// <summary>
/// 发送消息请求
/// </summary>
public class SendMessageRequest
{
    public MessageType Type { get; set; }
    public string? EncryptedContent { get; set; }
    public int? FileItemId { get; set; }
}

/// <summary>
/// 获取消息请求
/// </summary>
public class GetMessagesRequest
{
    public long? BeforeId { get; set; } // 获取此ID之前的消息
    public int Count { get; set; } = 50; // 默认50条
}

/// <summary>
/// 消息列表响应
/// </summary>
public class MessageListResponse
{
    public List<ChatMessageDto> Messages { get; set; } = new();
    public bool HasMore { get; set; }
    public long? OldestMessageId { get; set; }
}
