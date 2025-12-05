using System.Collections.Concurrent;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// 文件缓存管理实现
/// </summary>
public class FileCacheManager : IFileCacheManager
{
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<int, FileStatus> _fileStatuses = new();
    private readonly ConcurrentDictionary<int, (long Downloaded, long Total)> _downloadProgress = new();

    public FileCacheManager()
    {
        // 使用应用数据目录下的 tmp 文件夹
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SyncPad",
            "tmp");

        Directory.CreateDirectory(_cacheDirectory);
    }

    public FileStatus GetFileStatus(int fileId)
    {
        return _fileStatuses.GetOrAdd(fileId, _ => FileStatus.Remote);
    }

    public void SetFileStatus(int fileId, FileStatus status)
    {
        _fileStatuses[fileId] = status;
    }

    public string GetCachePath(int fileId, string fileName)
    {
        // 使用文件 ID + 原始文件名，避免同名冲突
        var safeFileName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return Path.Combine(_cacheDirectory, $"{fileId}_{safeFileName}{extension}");
    }

    public bool IsCached(int fileId)
    {
        var status = GetFileStatus(fileId);
        return status == FileStatus.Cached;
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

        _fileStatuses.Clear();
        _downloadProgress.Clear();
    }

    public async Task DeleteCacheAsync(int fileId)
    {
        await Task.Run(() =>
        {
            // 查找所有以 fileId 开头的缓存文件
            var pattern = $"{fileId}_*";
            var files = Directory.GetFiles(_cacheDirectory, pattern);

            foreach (var file in files)
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

        _fileStatuses.TryRemove(fileId, out _);
        _downloadProgress.TryRemove(fileId, out _);
    }
}
