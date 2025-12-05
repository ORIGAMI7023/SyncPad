namespace SyncPad.Server.Data.Entities;

/// <summary>
/// 文件元信息实体
/// </summary>
public class FileItem
{
    public int Id { get; set; }

    /// <summary>
    /// 所属用户 ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 原始文件名（用户可见）
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 文件内容 SHA-256 哈希（用于去重）
    /// </summary>
    public required string ContentHash { get; set; }

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// 上传时间
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// 过期时间（TTL）
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 是否已删除（逻辑删除）
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// 网格位置 X（列坐标，0-7）
    /// </summary>
    public int PositionX { get; set; } = 0;

    /// <summary>
    /// 网格位置 Y（行坐标，0-∞）
    /// </summary>
    public int PositionY { get; set; } = 0;

    // 导航属性
    public User? User { get; set; }
}
