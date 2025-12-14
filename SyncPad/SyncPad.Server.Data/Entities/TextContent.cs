namespace SyncPad.Server.Data.Entities;

/// <summary>
/// 文本内容实体（每个用户一个）
/// </summary>
public class TextContent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }

    // 导航属性
    public User? User { get; set; }
}
