namespace SyncPad.Client.Core.Services;

/// <summary>
/// 文件缓存管理接口（XXHash64 缓存键）
/// </summary>
public interface IFileCacheManager
{
    /// <summary>
    /// 按 hash 查找缓存文件
    /// </summary>
    string? FindCachedFileByHash(string hash);

    /// <summary>
    /// 遍历缓存目录，按 XXHash64 前缀查找匹配 fileName 的缓存文件
    /// </summary>
    string? FindCachedFile(string fileName);

    /// <summary>
    /// 检查文件是否已缓存（按 hash）
    /// </summary>
    bool IsCachedByHash(string hash);

    /// <summary>
    /// 检查文件是否已缓存（按文件名）
    /// </summary>
    bool IsCached(string fileName);

    /// <summary>
    /// 计算文件的 XXHash64 十六进制字符串
    /// </summary>
    string? ComputeXXHash64(string filePath);

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
    /// 删除指定文件缓存（按文件名查找）
    /// </summary>
    Task DeleteCacheAsync(string fileName);

    /// <summary>
    /// 清理过期缓存（默认7天未访问）
    /// </summary>
    Task CleanupExpiredCacheAsync(int expirationDays = 7);
}
