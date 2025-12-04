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
                OnPropertyChanged(nameof(IsDownloading));
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
        FileStatus.Downloading => $"下载中 {DownloadProgress}%",
        FileStatus.Cached => "已缓存",
        FileStatus.CachedPartial => "部分缓存",
        FileStatus.Error => "错误",
        _ => "未知"
    };

    public bool IsDownloading => Status == FileStatus.Downloading;
    public bool IsCached => Status == FileStatus.Cached || Status == FileStatus.CachedPartial;

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
}
