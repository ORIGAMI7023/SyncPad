using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// 文件缓存管理接口
/// </summary>
public interface IFileCacheManager
{
    /// <summary>
    /// 获取文件状态
    /// </summary>
    FileStatus GetFileStatus(int fileId);

    /// <summary>
    /// 更新文件状态
    /// </summary>
    void SetFileStatus(int fileId, FileStatus status);

    /// <summary>
    /// 获取文件在 tmp 的缓存路径
    /// </summary>
    string GetCachePath(int fileId, string fileName);

    /// <summary>
    /// 检查文件是否已缓存
    /// </summary>
    bool IsCached(int fileId);

    /// <summary>
    /// 获取下载进度（0-100）
    /// </summary>
    int GetDownloadProgress(int fileId);

    /// <summary>
    /// 更新下载进度
    /// </summary>
    void UpdateDownloadProgress(int fileId, long downloaded, long total);

    /// <summary>
    /// 清理所有缓存
    /// </summary>
    Task ClearAllCacheAsync();

    /// <summary>
    /// 删除指定文件缓存
    /// </summary>
    Task DeleteCacheAsync(int fileId);
}
