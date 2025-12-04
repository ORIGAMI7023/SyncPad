namespace SyncPad.Shared.Models;

/// <summary>
/// 文件状态枚举
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// 仅服务器有，本地无缓存
    /// </summary>
    Remote,

    /// <summary>
    /// 正在下载中（进度由客户端本地计算）
    /// </summary>
    Downloading,

    /// <summary>
    /// 已完整缓存到本地 tmp
    /// </summary>
    Cached,

    /// <summary>
    /// 部分缓存（预留：预载用，暂未实现）
    /// </summary>
    CachedPartial,

    /// <summary>
    /// 下载失败
    /// </summary>
    Error
}
