using SyncPad.Shared.Models;

namespace SyncPad.Client.ViewModels;

public class SelectableFileItem : BaseViewModel
{
    private bool _isSelected;
    private object? _nativeIcon; // 存储平台特定的图标对象

    public FileItemDto File { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// 平台原生文件图标（Windows: BitmapImage）
    /// </summary>
    public object? NativeIcon
    {
        get => _nativeIcon;
        set => SetProperty(ref _nativeIcon, value);
    }

    public bool HasNativeIcon => NativeIcon != null;

    // 图标映射
    public string FileIcon => GetFileIcon(MimeType);

    // 格式化文件大小
    public string FileSizeText => FormatFileSize(FileSize);

    // 文件类型（可读名称）
    public string FileType => GetFileType(MimeType, FileName);

    // 格式化的上传时间
    public string UploadedAtText => UploadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    // 委托 FileItemDto 的属性
    public int Id => File.Id;
    public string FileName => File.FileName;
    public long FileSize => File.FileSize;
    public string? MimeType => File.MimeType;
    public DateTime UploadedAt => File.UploadedAt;
    public DateTime ExpiresAt => File.ExpiresAt;

    public SelectableFileItem(FileItemDto file)
    {
        File = file;
    }

    private string GetFileType(string? mimeType, string fileName)
    {
        if (!string.IsNullOrEmpty(mimeType))
        {
            if (mimeType.StartsWith("image/")) return "图片";
            if (mimeType.StartsWith("video/")) return "视频";
            if (mimeType.StartsWith("audio/")) return "音频";
            if (mimeType == "application/pdf") return "PDF";
            if (mimeType.Contains("word") || mimeType.Contains("document")) return "文档";
            if (mimeType.Contains("sheet") || mimeType.Contains("excel")) return "表格";
            if (mimeType.Contains("presentation") || mimeType.Contains("powerpoint")) return "演示";
            if (mimeType.StartsWith("text/")) return "文本";
            if (mimeType.Contains("zip") || mimeType.Contains("rar") || mimeType.Contains("7z") || mimeType.Contains("tar") || mimeType.Contains("compressed")) return "压缩包";
            if (mimeType.Contains("javascript") || mimeType.Contains("json") || mimeType.Contains("html") || mimeType.Contains("css") || mimeType.Contains("xml")) return "代码";
        }

        // 根据 extension 评估
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => "图片",
            ".mp4" or ".avi" or ".mov" or ".mkv" or ".wmv" => "视频",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "音频",
            ".pdf" => "PDF",
            ".doc" or ".docx" => "文档",
            ".xls" or ".xlsx" => "表格",
            ".ppt" or ".pptx" => "演示",
            ".txt" or ".md" or ".log" => "文本",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "压缩包",
            ".js" or ".ts" or ".py" or ".java" or ".cs" or ".cpp" or ".go" or ".rs" => "代码",
            _ => "文件"
        };
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
