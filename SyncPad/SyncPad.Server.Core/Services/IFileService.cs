using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

public interface IFileService
{
    /// <summary>
    /// 获取用户文件列表（status=active 且未过期）
    /// </summary>
    Task<List<FileItemDto>> GetFilesAsync(int userId);

    /// <summary>
    /// 检查 hash 是否已存在
    /// </summary>
    Task<CheckHashResult> CheckHashAsync(string hash);

    /// <summary>
    /// 上传文件（收到文件体后调用）
    /// </summary>
    Task<FileUploadResponse> UploadFileAsync(int userId, string fileName, Stream stream, string? mimeType, string hash);

    /// <summary>
    /// 秒传：激活已 cached 的文件
    /// </summary>
    Task<FileUploadResponse> InstantUploadAsync(int userId, string fileName, string hash);

    /// <summary>
    /// 获取文件下载流
    /// </summary>
    Task<(Stream? Stream, string? MimeType, string? FileName, long FileSize)> DownloadFileAsync(int userId, int fileId);

    /// <summary>
    /// 删除文件（软删除，status 改为 cached）
    /// </summary>
    Task<bool> DeleteFileAsync(int userId, int fileId);

    /// <summary>
    /// 重命名文件
    /// </summary>
    Task<FileItemDto?> RenameFileAsync(int userId, int fileId, string newFileName);

    /// <summary>
    /// 清理过期文件（后台任务调用）
    /// </summary>
    Task CleanupExpiredFilesAsync();
}
