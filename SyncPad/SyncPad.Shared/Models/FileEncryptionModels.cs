namespace SyncPad.Shared.Models;

/// <summary>
/// 文件加密上传请求
/// </summary>
public class FileEncryptionUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string EncryptedDataBase64 { get; set; } = string.Empty;
    public string IVBase64 { get; set; } = string.Empty;
    public bool IsChunked { get; set; } // 是否分块加密
}

/// <summary>
/// 文件加密上传响应
/// </summary>
public class FileEncryptionUploadResponse
{
    public bool Success { get; set; }
    public int? FileId { get; set; }
    public string? ErrorMessage { get; set; }
    public FileItemDto? File { get; set; }
}

/// <summary>
/// 文件加密下载响应
/// </summary>
public class FileEncryptionDownloadResponse
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public string? EncryptedDataBase64 { get; set; }
    public string? IVBase64 { get; set; }
    public long FileSize { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 文件加密进度报告
/// </summary>
public class FileEncryptionProgress
{
    public int FileId { get; set; }
    public int Progress { get; set; } // 0-100
    public string Status { get; set; } = string.Empty; // "encrypting", "decrypting", "completed", "error"
    public string? ErrorMessage { get; set; }
}
