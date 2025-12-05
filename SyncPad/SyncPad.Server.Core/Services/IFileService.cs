using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

public interface IFileService
{
    /// <summary>
    /// 获取用户文件列表（不含已删除）
    /// </summary>
    Task<List<FileItemDto>> GetFilesAsync(int userId);

    /// <summary>
    /// 检查是否存在同名文件
    /// </summary>
    Task<bool> FileExistsAsync(int userId, string fileName);

    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="fileName">原始文件名</param>
    /// <param name="stream">文件流</param>
    /// <param name="mimeType">MIME 类型</param>
    /// <param name="overwrite">是否覆盖同名文件</param>
    /// <returns>上传结果</returns>
    Task<FileUploadResponse> UploadFileAsync(int userId, string fileName, Stream stream, string? mimeType, bool overwrite = false);

    /// <summary>
    /// 获取文件下载流
    /// </summary>
    Task<(Stream? Stream, string? MimeType, string? FileName, long FileSize)> DownloadFileAsync(int userId, int fileId);

    /// <summary>
    /// 删除文件（逻辑删除）
    /// </summary>
    Task<bool> DeleteFileAsync(int userId, int fileId);

    /// <summary>
    /// 清理过期文件（后台任务调用）
    /// </summary>
    Task CleanupExpiredFilesAsync();

    /// <summary>
    /// 更新文件位置
    /// </summary>
    Task<bool> UpdateFilePositionAsync(int userId, int fileId, int positionX, int positionY);

    /// <summary>
    /// 获取下一个可用位置（第一个空位，左优先、上优先）
    /// </summary>
    Task<(int X, int Y)> GetNextAvailablePositionAsync(int userId);
}
