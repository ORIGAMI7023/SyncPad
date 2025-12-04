using System.Collections.ObjectModel;
using System.Windows.Input;
using SyncPad.Client.Core.Services;
using SyncPad.Shared.Models;

namespace SyncPad.Client.ViewModels;

public class PadViewModel : BaseViewModel, IDisposable
{
    private readonly IAuthManager _authManager;
    private readonly IApiClient _apiClient;
    private readonly ITextHubClient _textHubClient;
    private readonly IFileClient _fileClient;

    private string _content = string.Empty;
    private string _connectionStatus = "未连接";
    private bool _isConnected;
    private bool _isUpdatingFromServer;
    private CancellationTokenSource? _throttleCts;
    private readonly object _throttleLock = new();

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value) && !_isUpdatingFromServer)
            {
                // 节流发送更新
                ThrottleSendUpdate();
            }
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string Username => _authManager.Username ?? "未知用户";

    // 文件列表
    private ObservableCollection<SelectableFileItem> _files = [];
    public ObservableCollection<SelectableFileItem> Files
    {
        get => _files;
        set => SetProperty(ref _files, value);
    }

    public bool HasFiles => Files.Count > 0;
    public bool HasNoFiles => Files.Count == 0;

    // 多选支持
    public IEnumerable<SelectableFileItem> SelectedFiles => Files.Where(f => f.IsSelected);

    private bool _isSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set => SetProperty(ref _isSelectionMode, value);
    }

    public bool HasSelectedFiles => SelectedFiles.Any();
    public string SelectedFilesText => $"已选择 {SelectedFiles.Count()} 个文件";

    public ICommand LogoutCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectFileCommand { get; }
    public ICommand DownloadFileCommand { get; }
    public ICommand DeleteFileCommand { get; }
    public ICommand ToggleFileSelectionCommand { get; }
    public ICommand BatchDownloadCommand { get; }
    public ICommand BatchDeleteCommand { get; }
    public ICommand ClearSelectionCommand { get; }

    // 属性变更通知辅助方法
    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedFiles));
        OnPropertyChanged(nameof(SelectedFilesText));
        ((Command)BatchDownloadCommand).ChangeCanExecute();
        ((Command)BatchDeleteCommand).ChangeCanExecute();
    }

    public event Action? LogoutRequested;

    public PadViewModel(IAuthManager authManager, IApiClient apiClient, ITextHubClient textHubClient, IFileClient fileClient)
    {
        _authManager = authManager;
        _apiClient = apiClient;
        _textHubClient = textHubClient;
        _fileClient = fileClient;

        LogoutCommand = new Command(async () => await LogoutAsync());
        RefreshCommand = new Command(async () => await RefreshTextAsync());
        SelectFileCommand = new Command(async () => await SelectFileAsync());
        DownloadFileCommand = new Command<SelectableFileItem>(async f => await DownloadFileAsync(f));
        DeleteFileCommand = new Command<SelectableFileItem>(async f => await DeleteFileAsync(f));
        ToggleFileSelectionCommand = new Command<SelectableFileItem>(ToggleFileSelection);
        BatchDownloadCommand = new Command(async () => await BatchDownloadAsync(), () => HasSelectedFiles);
        BatchDeleteCommand = new Command(async () => await BatchDeleteAsync(), () => HasSelectedFiles);
        ClearSelectionCommand = new Command(ClearSelection);

        // 监听连接状态变化
        _textHubClient.ConnectionStateChanged += OnConnectionStateChanged;
        _textHubClient.TextUpdateReceived += OnTextUpdateReceived;
        _textHubClient.FileUpdateReceived += OnFileUpdateReceived;
    }

    public async Task InitializeAsync()
    {
        // 连接 SignalR
        await ConnectToHubAsync();

        // 加载初始文本
        await RefreshTextAsync();

        // 加载文件列表
        await RefreshFilesAsync();
    }

    private async Task ConnectToHubAsync()
    {
        try
        {
            ConnectionStatus = "连接中...";
            var hubUrl = _authManager.GetHubUrl();
            var token = _authManager.Token;

            if (!string.IsNullOrEmpty(token))
            {
                await _textHubClient.ConnectAsync(hubUrl, token);
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"连接失败: {ex.Message}";
        }
    }

    private async Task RefreshTextAsync()
    {
        try
        {
            var response = await _apiClient.GetTextAsync();
            if (response.Success && response.Data != null)
            {
                _isUpdatingFromServer = true;
                Content = response.Data.Content;
                _isUpdatingFromServer = false;
            }
        }
        catch (Exception ex)
        {
            // 静默处理错误
            System.Diagnostics.Debug.WriteLine($"刷新文本失败: {ex.Message}");
        }
    }

    private void OnConnectionStateChanged(bool connected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = connected;
            ConnectionStatus = connected ? "已连接" : "已断开";
        });
    }

    private void OnTextUpdateReceived(TextSyncMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isUpdatingFromServer = true;
            Content = message.Content;
            _isUpdatingFromServer = false;
        });
    }

    private void ThrottleSendUpdate()
    {
        lock (_throttleLock)
        {
            _throttleCts?.Cancel();
            _throttleCts = new CancellationTokenSource();
            var token = _throttleCts.Token;

            Task.Delay(300, token).ContinueWith(async _ =>
            {
                if (!token.IsCancellationRequested && _textHubClient.IsConnected)
                {
                    await _textHubClient.SendTextUpdateAsync(Content);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private async Task LogoutAsync()
    {
        await _textHubClient.DisconnectAsync();
        await _authManager.LogoutAsync();
        LogoutRequested?.Invoke();
    }

    private async Task RefreshFilesAsync()
    {
        try
        {
            var response = await _fileClient.GetFilesAsync();
            if (response.Success && response.Data != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Files.Clear();
                    foreach (var file in response.Data.Files)
                    {
                        Files.Add(new SelectableFileItem(file));
                    }
                    OnPropertyChanged(nameof(HasFiles));
                    OnPropertyChanged(nameof(HasNoFiles));
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"刷新文件列表失败: {ex.Message}");
        }
    }

    private async Task SelectFileAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "选择要上传的文件"
            });

            if (result != null)
            {
                await UploadFileAsync(result);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"选择文件失败: {ex.Message}");
        }
    }

    private async Task UploadFileAsync(FileResult fileResult)
    {
        try
        {
            // 检查同名文件
            if (await _fileClient.FileExistsAsync(fileResult.FileName))
            {
                var overwrite = await Application.Current!.MainPage!.DisplayAlert(
                    "文件已存在",
                    $"文件 \"{fileResult.FileName}\" 已存在，是否覆盖？",
                    "覆盖", "取消");

                if (!overwrite) return;

                using var stream = await fileResult.OpenReadAsync();
                await _fileClient.UploadFileAsync(fileResult.FileName, stream, fileResult.ContentType, overwrite: true);
            }
            else
            {
                using var stream = await fileResult.OpenReadAsync();
                var response = await _fileClient.UploadFileAsync(fileResult.FileName, stream, fileResult.ContentType);

                if (!response.Success)
                {
                    await Application.Current!.MainPage!.DisplayAlert("上传失败", response.ErrorMessage, "确定");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
        }
    }

    private async Task DownloadFileAsync(SelectableFileItem file)
    {
        try
        {
            var url = _fileClient.GetDownloadUrl(file.Id);

            // 使用浏览器打开下载链接（不阻塞UI）
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"下载文件失败: {ex.Message}");
        }
    }

    private async Task DeleteFileAsync(SelectableFileItem file)
    {
        try
        {
            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                "确认删除",
                $"确定要删除文件 \"{file.FileName}\" 吗？",
                "删除", "取消");

            if (confirm)
            {
                var response = await _fileClient.DeleteFileAsync(file.Id);
                if (!response.Success)
                {
                    await Application.Current!.MainPage!.DisplayAlert("删除失败", response.ErrorMessage, "确定");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"删除文件失败: {ex.Message}");
        }
    }

    private void ToggleFileSelection(SelectableFileItem file)
    {
        if (file != null)
        {
            file.IsSelected = !file.IsSelected;
            NotifySelectionChanged();
        }
    }

    private void ClearSelection()
    {
        foreach (var file in Files)
        {
            file.IsSelected = false;
        }
        NotifySelectionChanged();
    }

    private async Task BatchDownloadAsync()
    {
        foreach (var file in SelectedFiles.ToList())
        {
            await DownloadFileAsync(file);
        }
    }

    private async Task BatchDeleteAsync()
    {
        try
        {
            var confirm = await Application.Current!.MainPage!.DisplayAlert(
                "确认批量删除",
                $"确定要删除已选择的 {SelectedFiles.Count()} 个文件吗？",
                "删除", "取消");

            if (confirm)
            {
                var filesToDelete = SelectedFiles.ToList();
                foreach (var file in filesToDelete)
                {
                    var response = await _fileClient.DeleteFileAsync(file.Id);
                    if (!response.Success)
                    {
                        await Application.Current!.MainPage!.DisplayAlert(
                            "删除失败",
                            $"文件 \"{file.FileName}\" 删除失败: {response.ErrorMessage}",
                            "确定");
                    }
                }

                ClearSelection();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"批量删除文件失败: {ex.Message}");
        }
    }

    private void OnFileUpdateReceived(FileSyncMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (message.Action)
            {
                case "added":
                    if (message.File != null)
                    {
                        // 移除同名旧文件（如果存在）
                        var existing = Files.FirstOrDefault(f => f.FileName == message.File.FileName);
                        if (existing != null)
                            Files.Remove(existing);

                        Files.Insert(0, new SelectableFileItem(message.File));
                    }
                    break;

                case "deleted":
                    if (message.FileId.HasValue)
                    {
                        var toRemove = Files.FirstOrDefault(f => f.Id == message.FileId.Value);
                        if (toRemove != null)
                        {
                            Files.Remove(toRemove);
                            // 选中状态会自动随着移除而清除
                            NotifySelectionChanged();
                        }
                    }
                    break;
            }

            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(HasNoFiles));
        });
    }

    public void Dispose()
    {
        _textHubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _textHubClient.TextUpdateReceived -= OnTextUpdateReceived;
        _textHubClient.FileUpdateReceived -= OnFileUpdateReceived;
        _throttleCts?.Cancel();
        _throttleCts?.Dispose();
    }
}
