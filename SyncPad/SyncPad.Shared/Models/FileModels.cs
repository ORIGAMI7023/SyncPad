namespace SyncPad.Shared.Models;

/// <summary>
/// 文件信息 DTO
/// </summary>
public class FileItemDto
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int PositionX { get; set; } = 0;
    public int PositionY { get; set; } = 0;
}

/// <summary>
/// 文件列表响应
/// </summary>
public class FileListResponse
{
    public List<FileItemDto> Files { get; set; } = [];
}

/// <summary>
/// 文件上传响应
/// </summary>
public class FileUploadResponse
{
    public bool Success { get; set; }
    public FileItemDto? File { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 文件同步消息（SignalR）
/// </summary>
public class FileSyncMessage
{
    public required string Action { get; set; } // "added", "deleted", "position_changed"
    public FileItemDto? File { get; set; }
    public int? FileId { get; set; } // 删除时使用
}

/// <summary>
/// 文件位置更新请求
/// </summary>
public class FilePositionUpdateRequest
{
    public int FileId { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
}
