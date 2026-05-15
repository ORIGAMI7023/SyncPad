namespace SyncPad.Server.Data.Entities;

/// <summary>
/// 用户实体
/// </summary>
public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }

    // 导航属性
    public TextContent? TextContent { get; set; } // 保留用于向后兼容
    public ICollection<FileItem>? Files { get; set; }
    public ICollection<ChatMessage>? ChatMessages { get; set; }
    public EncryptionKey? EncryptionKey { get; set; }
    public ICollection<Device>? Devices { get; set; }
}
