using SyncPad.Shared.Models;

namespace SyncPad.Client.ViewModels;

public class SelectableFileItem : BaseViewModel
{
    private bool _isSelected;

    public FileItemDto File { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

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
