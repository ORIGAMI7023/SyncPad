using SyncPad.Shared.Models;

namespace SyncPad.Client.ViewModels;

public class SelectableFileItem : BaseViewModel
{
    private bool _isSelected;
    private FileStatus _status = FileStatus.Remote;
    private int _downloadProgress;

    public FileItemDto File { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public FileStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsPreloading));
                OnPropertyChanged(nameof(IsCached));
            }
        }
    }

    public int DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    // UI 辅助属性
    public string StatusText => Status switch
    {
        FileStatus.Remote => "云端",
        FileStatus.PreloadPending => "队列中",
        FileStatus.Preloading => $"预载中 {DownloadProgress}%",
        FileStatus.Cached => "已缓存",
        _ => "未知"
    };

    public bool IsPreloading => Status == FileStatus.Preloading;
    public bool IsCached => Status == FileStatus.Cached;

    // 图标映射
    public string FileIcon => GetFileIcon(MimeType);

    public string StatusBadge => Status switch
    {
        FileStatus.Remote => "☁️",
        FileStatus.PreloadPending => "🕐",
        FileStatus.Cached => "✓",
        _ => ""
    };

    public bool ShowProgress => Status == FileStatus.Preloading;

    // 格式化文件大小
    public string FileSizeText => FormatFileSize(FileSize);

    // 委托 FileItemDto 的属性
    public int Id => File.Id;
    public string FileName => File.FileName;
    public long FileSize => File.FileSize;
    public string? MimeType => File.MimeType;
    public DateTime UploadedAt => File.UploadedAt;
    public DateTime ExpiresAt => File.ExpiresAt;
    public int PositionX => File.PositionX;
    public int PositionY => File.PositionY;

    public SelectableFileItem(FileItemDto file)
    {
        File = file;
    }

    private string GetFileIcon(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return "📎";

        return mimeType switch
        {
            _ when mimeType.StartsWith("image/") => "📷",
            _ when mimeType.StartsWith("video/") => "🎬",
            _ when mimeType.StartsWith("audio/") => "🎵",
            "application/pdf" => "📄",
            "application/msword" => "📄",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "📄",
            "text/plain" => "📄",
            "application/zip" => "📦",
            "application/x-rar-compressed" => "📦",
            "application/x-7z-compressed" => "📦",
            "application/x-tar" => "📦",
            "text/html" => "💻",
            "text/css" => "💻",
            "application/javascript" => "💻",
            "text/javascript" => "💻",
            "application/json" => "💻",
            "application/xml" => "💻",
            _ when mimeType.Contains("csharp") => "💻",
            _ when mimeType.Contains("python") => "💻",
            _ when mimeType.Contains("java") => "💻",
            _ => "📎"
        };
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }
}
