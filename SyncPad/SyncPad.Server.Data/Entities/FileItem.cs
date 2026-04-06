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
    /// XXHash64 十六进制（16字符，唯一索引）
    /// </summary>
    public required string Hash { get; set; }

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
    /// 文件状态：active（正常显示）/ cached（已删除，文件体保留）
    /// </summary>
    public string Status { get; set; } = "active";

    // 导航属性
    public User? User { get; set; }
}
