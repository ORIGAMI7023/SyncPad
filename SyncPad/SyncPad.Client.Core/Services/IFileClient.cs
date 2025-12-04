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
    /// 上传文件
    /// </summary>
    Task<FileUploadResponse> UploadFileAsync(string fileName, Stream stream, string? mimeType, bool overwrite = false);

    /// <summary>
    /// 获取文件下载 URL
    /// </summary>
    string GetDownloadUrl(int fileId);

    /// <summary>
    /// 删除文件
    /// </summary>
    Task<ApiResponse> DeleteFileAsync(int fileId);
}
