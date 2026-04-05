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
    private readonly IFileCacheManager _cacheManager;
    private readonly IFileOperationService _fileOperationService;

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
    public ICommand RefreshFilesCommand { get; }
    public ICommand SelectFileCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand DeleteFileCommand { get; }
    public ICommand DownloadFileCommand { get; }
    public ICommand ToggleFileSelectionCommand { get; }
    public ICommand BatchDownloadCommand { get; }
    public ICommand BatchDeleteCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand CopyFileCommand { get; }
    public ICommand ExportFileCommand { get; }
    public ICommand BatchCopyCommand { get; }
    public ICommand BatchExportCommand { get; }

    // 重命名命令
    public ICommand RenameFileCommand { get; }

    // 属性变更通知辅助方法
    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedFiles));
        OnPropertyChanged(nameof(SelectedFilesText));
        ((Command)BatchDownloadCommand).ChangeCanExecute();
        ((Command)BatchDeleteCommand).ChangeCanExecute();
    }

    /// <summary>
    /// 清除所有文件的选中状态
    /// </summary>
    public void ClearAllSelection()
    {
        foreach (var file in Files)
        {
            file.IsSelected = false;
        }
        NotifySelectionChanged();
    }

    /// <summary>
    /// 获取已缓存文件的本地路径
    /// </summary>
    public string? GetCachedFilePath(SelectableFileItem file)
    {
        if (_cacheManager.IsCached(file.Id))
        {
            return _cacheManager.GetCachePath(file.Id, file.FileName);
        }
        return null;
    }

    public event Action? LogoutRequested;

    public PadViewModel(IAuthManager authManager, IApiClient apiClient, ITextHubClient textHubClient, IFileClient fileClient, IFileCacheManager cacheManager, IFileOperationService fileOperationService)
    {
        _authManager = authManager;
        _apiClient = apiClient;
        _textHubClient = textHubClient;
        _fileClient = fileClient;
        _cacheManager = cacheManager;
        _fileOperationService = fileOperationService;

        LogoutCommand = new Command(async () => await LogoutAsync());
        RefreshCommand = new Command(async () => await RefreshTextAsync());
        RefreshFilesCommand = new Command(async () => await RefreshFilesAsync());
        SelectFileCommand = new Command(async () => await SelectFileAsync());
        OpenFileCommand = new Command<SelectableFileItem>(async f => await OpenFileAsync(f));
        DeleteFileCommand = new Command<SelectableFileItem>(async f => await DeleteFileAsync(f));
        DownloadFileCommand = new Command<SelectableFileItem>(async f => await DownloadToCacheAsync(f));
        ToggleFileSelectionCommand = new Command<SelectableFileItem>(ToggleFileSelection);
        BatchDownloadCommand = new Command(async () => await BatchDownloadAsync(), () => HasSelectedFiles);
        BatchDeleteCommand = new Command(async () => await BatchDeleteAsync(), () => HasSelectedFiles);
        ClearSelectionCommand = new Command(ClearSelection);
        CopyFileCommand = new Command<SelectableFileItem>(async f => await CopyFileAsync(f));
        ExportFileCommand = new Command<SelectableFileItem>(async f => await ExportFileAsync(f));
        BatchCopyCommand = new Command(async () => await BatchCopyAsync(), () => HasSelectedFiles);
        BatchExportCommand = new Command(async () => await BatchExportAsync(), () => HasSelectedFiles);

        // 重命名命令
        RenameFileCommand = new Command<(SelectableFileItem file, string newName)>(async p => await RenameFileAsync(p.file, p.newName));

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
        await RefreshFilesInternalAsync();
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

    /// <summary>
    /// 刷新文件列表（公开方法）
    /// </summary>
    public async Task RefreshFilesAsync() => await RefreshFilesInternalAsync();

    private async Task RefreshFilesInternalAsync()
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
                        var item = new SelectableFileItem(file);
                        Files.Add(item);

                        // Windows 平台：异步加载真实文件图标
#if WINDOWS
                        _ = LoadFileIconAsync(item);
#endif
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

#if WINDOWS
    private async Task LoadFileIconAsync(SelectableFileItem item)
    {
        try
        {
            var icon = await Platforms.Windows.FileIconService.GetIconForFileNameAsync(item.FileName, large: true);
            if (icon != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    item.NativeIcon = icon;
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载文件图标失败 ({item.FileName}): {ex.Message}");
        }
    }
#endif

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
            }

            // 读取文件到内存流，避免原始流被关闭
            using var memoryStream = new MemoryStream();
            using (var stream = await fileResult.OpenReadAsync())
            {
                if (stream == null || stream.Length == 0)
                {
                    await Application.Current!.MainPage!.DisplayAlert("上传失败", "无法读取文件", "确定");
                    return;
                }
                await stream.CopyToAsync(memoryStream);
            }

            // 重置流位置
            memoryStream.Position = 0;

            var contentType = fileResult.ContentType ?? "application/octet-stream";
            var response = await _fileClient.UploadFileAsync(
                fileResult.FileName,
                memoryStream,
                contentType,
                overwrite: true);

            if (!response.Success)
            {
                await Application.Current!.MainPage!.DisplayAlert("上传失败", response.ErrorMessage, "确定");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
            await Application.Current!.MainPage!.DisplayAlert("上传失败", ex.Message, "确定");
        }
    }

    /// <summary>
    /// 下载文件到缓存（用于批量下载，不打开文件）
    /// </summary>
    private async Task DownloadToCacheAsync(SelectableFileItem file)
    {
        try
        {
            if (_cacheManager.IsCached(file.Id))
                return;

            var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);

            var success = await _fileClient.DownloadFileToCacheAsync(
                file.Id,
                file.FileName,
                cachePath,
                null);

            if (success)
            {
                _cacheManager.SetFileStatus(file.Id, FileStatus.Cached);
            }
            else
            {
                _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
            }
        }
        catch (Exception ex)
        {
            _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
            System.Diagnostics.Debug.WriteLine($"下载文件失败: {ex.Message}");
        }
    }

    private async Task OpenFileAsync(SelectableFileItem file)
    {
        try
        {
            var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);

            // 验证缓存路径
            if (string.IsNullOrEmpty(cachePath))
            {
                await Application.Current!.MainPage!.DisplayAlert("打开失败", "无法确定文件缓存路径", "确定");
                return;
            }

            // 检查是否已缓存
            if (_cacheManager.IsCached(file.Id))
            {
                // 验证文件是否存在
                if (!File.Exists(cachePath))
                {
                    await Application.Current!.MainPage!.DisplayAlert("打开失败", "缓存文件不存在，请重新下载", "确定");
                    _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
                    return;
                }

                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(cachePath)
                });
                return;
            }

            // 未缓存，先下载
            var success = await _fileClient.DownloadFileToCacheAsync(
                file.Id,
                file.FileName,
                cachePath,
                null);

            if (success)
            {
                _cacheManager.SetFileStatus(file.Id, FileStatus.Cached);

                // 验证下载的文件
                if (!File.Exists(cachePath))
                {
                    await Application.Current!.MainPage!.DisplayAlert("打开失败", "文件下载后无法找到", "确定");
                    return;
                }

                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(cachePath)
                });
            }
            else
            {
                _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
                await Application.Current!.MainPage!.DisplayAlert("下载失败", "无法下载文件，请检查网络连接", "确定");
            }
        }
        catch (Exception ex)
        {
            _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
            await Application.Current!.MainPage!.DisplayAlert("打开失败", $"错误：{ex.Message}", "确定");
        }
    }

    public async Task DeleteFileAsync(SelectableFileItem file, bool showConfirmation = true)
    {
        try
        {
            bool confirm = true;

            if (showConfirmation)
            {
                confirm = await Application.Current!.MainPage!.DisplayAlert(
                    "确认删除",
                    $"确定要删除文件 \"{file.FileName}\" 吗？",
                    "删除", "取消");
            }

            if (confirm)
            {
                var response = await _fileClient.DeleteFileAsync(file.Id);
                if (response.Success)
                {
                    await _cacheManager.DeleteCacheAsync(file.Id);
                }
                else
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
            await DownloadToCacheAsync(file);
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

    #region 复制/导出功能

    /// <summary>
    /// 复制单个文件到系统剪贴板
    /// </summary>
    private async Task CopyFileAsync(SelectableFileItem file)
    {
        try
        {
            if (!_cacheManager.IsCached(file.Id))
            {
                await DownloadToCacheAsync(file);
            }

            var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);
            if (File.Exists(cachePath))
            {
                var success = _fileOperationService.CopyFileToClipboard(cachePath);
                if (success)
                {
                    await Application.Current!.MainPage!.DisplayAlert("成功", "文件已复制到剪贴板", "确定");
                }
                else
                {
                    await Application.Current!.MainPage!.DisplayAlert("失败", "复制到剪贴板失败", "确定");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"复制文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导出单个文件到选择的目录
    /// </summary>
    private async Task ExportFileAsync(SelectableFileItem file)
    {
        try
        {
            if (!_cacheManager.IsCached(file.Id))
            {
                await DownloadToCacheAsync(file);
            }

            var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);
            if (!File.Exists(cachePath))
            {
                await Application.Current!.MainPage!.DisplayAlert("失败", "文件未缓存", "确定");
                return;
            }

            var targetFolder = await _fileOperationService.PickFolderAsync();
            if (string.IsNullOrEmpty(targetFolder))
                return;

            var success = await _fileOperationService.ExportFileAsync(cachePath, targetFolder);
            if (success)
            {
                await Application.Current!.MainPage!.DisplayAlert("成功", $"文件已导出到 {targetFolder}", "确定");
            }
            else
            {
                await Application.Current!.MainPage!.DisplayAlert("失败", "导出文件失败", "确定");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"导出文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量复制选中的文件到剪贴板
    /// </summary>
    private async Task BatchCopyAsync()
    {
        try
        {
            var selectedFiles = SelectedFiles.ToList();
            var cachePaths = new List<string>();

            foreach (var file in selectedFiles)
            {
                if (!_cacheManager.IsCached(file.Id))
                {
                    await DownloadToCacheAsync(file);
                }

                var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);
                if (File.Exists(cachePath))
                {
                    cachePaths.Add(cachePath);
                }
            }

            if (cachePaths.Count > 0)
            {
                var success = _fileOperationService.CopyFilesToClipboard(cachePaths);
                if (success)
                {
                    await Application.Current!.MainPage!.DisplayAlert("成功", $"已复制 {cachePaths.Count} 个文件到剪贴板", "确定");
                }
                else
                {
                    await Application.Current!.MainPage!.DisplayAlert("失败", "复制到剪贴板失败", "确定");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"批量复制文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 批量导出选中的文件
    /// </summary>
    private async Task BatchExportAsync()
    {
        try
        {
            var targetFolder = await _fileOperationService.PickFolderAsync();
            if (string.IsNullOrEmpty(targetFolder))
                return;

            var selectedFiles = SelectedFiles.ToList();
            var cachePaths = new List<string>();

            foreach (var file in selectedFiles)
            {
                if (!_cacheManager.IsCached(file.Id))
                {
                    await DownloadToCacheAsync(file);
                }

                var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);
                if (File.Exists(cachePath))
                {
                    cachePaths.Add(cachePath);
                }
            }

            var successCount = await _fileOperationService.ExportFilesAsync(cachePaths, targetFolder);
            await Application.Current!.MainPage!.DisplayAlert("完成", $"已导出 {successCount}/{selectedFiles.Count} 个文件", "确定");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"批量导出文件失败: {ex.Message}");
        }
    }

    #endregion

    private void OnFileUpdateReceived(FileSyncMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (message.Action)
            {
                case "added":
                    if (message.File != null)
                    {
                        var existing = Files.FirstOrDefault(f => f.FileName == message.File.FileName);
                        if (existing != null)
                            Files.Remove(existing);

                        var newItem = new SelectableFileItem(message.File);
                        Files.Insert(0, newItem);

#if WINDOWS
                        _ = LoadFileIconAsync(newItem);
#endif
                    }
                    break;

                case "deleted":
                    if (message.FileId.HasValue)
                    {
                        var toRemove = Files.FirstOrDefault(f => f.Id == message.FileId.Value);
                        if (toRemove != null)
                        {
                            Files.Remove(toRemove);
                            NotifySelectionChanged();
                        }
                    }
                    break;
            }

            OnPropertyChanged(nameof(HasFiles));
            OnPropertyChanged(nameof(HasNoFiles));
        });
    }

    #region 上传支持

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public async Task<bool> FileExistsAsync(string fileName)
    {
        return await _fileClient.FileExistsAsync(fileName);
    }

    /// <summary>
    /// 上传文件（供外部调用）
    /// </summary>
    public async Task UploadFileAsync(string fileName, Stream stream, string contentType, bool overwrite = false)
    {
        try
        {
            var response = await _fileClient.UploadFileAsync(fileName, stream, contentType, overwrite);
            if (!response.Success)
            {
                await Application.Current!.MainPage!.DisplayAlert("上传失败", response.ErrorMessage, "确定");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
        }
    }

    #endregion

    /// <summary>
    /// 重命名文件
    /// </summary>
    public async Task RenameFileAsync(SelectableFileItem file, string newName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                await Application.Current!.MainPage!.DisplayAlert("重命名失败", "文件名不能为空", "确定");
                return;
            }

            if (newName == file.FileName)
            {
                return; // 文件名未改变，无需操作
            }

            // 保存旧文件名，用于更新缓存
            string oldFileName = file.FileName;

            var response = await _fileClient.RenameFileAsync(file.Id, newName);
            if (response.Success)
            {
                // 如果文件已缓存，先更新缓存文件名
                if (_cacheManager.IsCached(file.Id))
                {
                    var oldPath = _cacheManager.GetCachePath(file.Id, oldFileName);
                    var newPath = _cacheManager.GetCachePath(file.Id, newName);
                    if (File.Exists(oldPath))
                    {
                        File.Move(oldPath, newPath);
                    }
                }

                // 更新本地文件项
                file.File.FileName = newName;
            }
            else
            {
                await Application.Current!.MainPage!.DisplayAlert("重命名失败", response.ErrorMessage, "确定");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"重命名文件失败: {ex.Message}");
            await Application.Current!.MainPage!.DisplayAlert("重命名失败", $"发生错误: {ex.Message}", "确定");
        }
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
