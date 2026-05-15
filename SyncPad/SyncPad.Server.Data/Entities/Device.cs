namespace SyncPad.Server.Data.Entities;

/// <summary>
/// 设备信息实体（用于多设备同步）
/// </summary>
public class Device
{
    /// <summary>
    /// 设备ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 设备名称（如"iPhone 13 Pro"、"MacBook Pro"）
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// 设备类型（Web、iOS、Mac）
    /// </summary>
    public required string DeviceType { get; set; }

    /// <summary>
    /// 设备唯一标识
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// 最后活跃时间
    /// </summary>
    public DateTime LastActiveAt { get; set; }

    /// <summary>
    /// 是否在线
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// 最后同步的消息ID
    /// </summary>
    public long? LastSyncMessageId { get; set; }

    // 导航属性
    public User? User { get; set; }
}
