namespace SyncPad.Server.Data.Entities;

/// <summary>
/// 文件内容实体（用于去重管理）
/// </summary>
public class FileContent
{
    public int Id { get; set; }

    /// <summary>
    /// SHA-256 哈希值（唯一索引）
    /// </summary>
    public required string ContentHash { get; set; }

    /// <summary>
    /// 引用计数（有多少 FileItem 引用此内容）
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// 文件大小
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最后访问时间（用于 TTL 清理判断）
    /// </summary>
    public DateTime LastAccessedAt { get; set; }
}
