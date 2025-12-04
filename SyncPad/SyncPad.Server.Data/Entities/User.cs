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
    public TextContent? TextContent { get; set; }
    public ICollection<FileItem>? Files { get; set; }
}
