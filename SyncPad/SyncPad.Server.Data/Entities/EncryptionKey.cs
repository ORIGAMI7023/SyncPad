namespace SyncPad.Server.Data.Entities;

/// <summary>
/// 加密密钥盐值管理实体
/// </summary>
public class EncryptionKey
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 固定盐值（用于PBKDF2密钥派生）
    /// </summary>
    public required string Salt { get; set; }

    /// <summary>
    /// 密钥版本（用于密钥轮换）
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // 导航属性
    public User? User { get; set; }
}
