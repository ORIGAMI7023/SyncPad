namespace SyncPad.Shared.Models;

/// <summary>
/// 文件状态枚举（预载系统）
/// </summary>
public enum FileStatus
{
    /// <summary>
    /// 未预载 - 仅元数据同步，无本地缓存
    /// </summary>
    Remote,

    /// <summary>
    /// 预载排队中 - 等待后台预载
    /// </summary>
    PreloadPending,

    /// <summary>
    /// 正在预载 - 后台低速下载中（或用户点击后全速下载）
    /// </summary>
    Preloading,

    /// <summary>
    /// 预载完成 - 本地已有完整文件
    /// </summary>
    Cached
}
