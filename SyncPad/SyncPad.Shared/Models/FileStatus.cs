namespace SyncPad.Shared.Models;

/// <summary>
/// 文件状态枚举
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// 远程文件 - 无本地缓存
    /// </summary>
    Remote,

    /// <summary>
    /// 已缓存 - 本地已有完整文件
    /// </summary>
    Cached
}
