using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

public interface IFileClient
{
    /// <summary>
    /// 获取文件列表
    /// </summary>
    Task<ApiResponse<FileListResponse>> GetFilesAsync();

    /// <summary>
    /// 检查同名文件是否存在
    /// </summary>
    Task<bool> FileExistsAsync(string fileName);

    /// <summary>
    /// 检查 hash 是否已存在
    /// </summary>
    Task<ApiResponse<CheckHashResult>> CheckHashAsync(string hash);

    /// <summary>
    /// 上传文件（携带 hash）
    /// </summary>
    Task<FileUploadResponse> UploadFileAsync(string fileName, Stream stream, string? mimeType, string hash, bool overwrite = false);

    /// <summary>
    /// 秒传：通过 hash 激活已有文件
    /// </summary>
    Task<FileUploadResponse> InstantUploadAsync(string fileName, string hash);

    /// <summary>
    /// 获取文件下载 URL
    /// </summary>
    string GetDownloadUrl(int fileId);

    /// <summary>
    /// 删除文件
    /// </summary>
    Task<ApiResponse> DeleteFileAsync(int fileId);

    /// <summary>
    /// 重命名文件
    /// </summary>
    Task<ApiResponse<FileItemDto>> RenameFileAsync(int fileId, string newFileName);

    /// <summary>
    /// 下载文件到缓存（支持进度回调）
    /// </summary>
    Task<bool> DownloadFileToCacheAsync(int fileId, string fileName, string cachePath, Action<long, long>? progressCallback = null);
}
