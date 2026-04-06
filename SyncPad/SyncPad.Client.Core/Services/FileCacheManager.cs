using System.Collections.Concurrent;
using K4os.Hash.xxHash;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// 文件缓存管理实现（XXHash64 缓存键）
/// </summary>
public class FileCacheManager : IFileCacheManager
{
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<int, (long Downloaded, long Total)> _downloadProgress = new();

    public FileCacheManager()
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SyncPad",
            "tmp");

        Directory.CreateDirectory(_cacheDirectory);
    }

    public string? FindCachedFileByHash(string hash)
    {
        if (!Directory.Exists(_cacheDirectory))
            return null;

        foreach (var file in Directory.GetFiles(_cacheDirectory))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith(hash + "_"))
                return file;
        }

        return null;
    }

    public string? FindCachedFile(string fileName)
    {
        var safeFileName = GetSafeFileName(fileName);

        if (!Directory.Exists(_cacheDirectory))
            return null;

        foreach (var file in Directory.GetFiles(_cacheDirectory))
        {
            var name = Path.GetFileName(file);
            // 格式: {16位hex}_{safeFileName}
            var suffix = "_" + safeFileName;
            if (name.EndsWith(suffix) && name.Length > safeFileName.Length + 17)
            {
                var hexPart = name[..16];
                if (hexPart.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return file;
                }
            }
        }

        return null;
    }

    public bool IsCachedByHash(string hash)
    {
        return FindCachedFileByHash(hash) != null;
    }

    public bool IsCached(string fileName)
    {
        return FindCachedFile(fileName) != null;
    }

    public string? ComputeXXHash64(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            var hash = XXH64.DigestOf(data);
            return hash.ToString("x16");
        }
        catch
        {
            return null;
        }
    }

    public int GetDownloadProgress(int fileId)
    {
        if (_downloadProgress.TryGetValue(fileId, out var progress))
        {
            if (progress.Total == 0) return 0;
            return (int)((progress.Downloaded * 100) / progress.Total);
        }
        return 0;
    }

    public void UpdateDownloadProgress(int fileId, long downloaded, long total)
    {
        _downloadProgress[fileId] = (downloaded, total);
    }

    public async Task ClearAllCacheAsync()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            await Task.Run(() =>
            {
                foreach (var file in Directory.GetFiles(_cacheDirectory))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // 忽略删除失败
                    }
                }
            });
        }

        _downloadProgress.Clear();
    }

    public async Task DeleteCacheAsync(string fileName)
    {
        await Task.Run(() =>
        {
            var cachedPath = FindCachedFile(fileName);
            if (cachedPath != null)
            {
                try
                {
                    File.Delete(cachedPath);
                }
                catch
                {
                    // 忽略删除失败
                }
            }
        });
    }

    public async Task CleanupExpiredCacheAsync(int expirationDays = 7)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(_cacheDirectory))
                return;

            var expirationInterval = TimeSpan.FromDays(expirationDays);
            var now = DateTime.UtcNow;

            foreach (var file in Directory.GetFiles(_cacheDirectory))
            {
                try
                {
                    var lastAccess = File.GetLastAccessTimeUtc(file);
                    if (now - lastAccess > expirationInterval)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // 忽略
                }
            }
        });
    }

    /// <summary>
    /// 获取缓存目录路径
    /// </summary>
    public string GetCacheDirectory() => _cacheDirectory;

    /// <summary>
    /// 生成安全的文件名
    /// </summary>
    private static string GetSafeFileName(string fileName)
    {
        return fileName.Replace("/", "_").Replace("\\", "_");
    }
}
