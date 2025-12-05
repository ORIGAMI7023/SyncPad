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
    public ICommand SelectFileCommand { get; }
    public ICommand PreloadFileCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand DeleteFileCommand { get; }
    public ICommand ToggleFileSelectionCommand { get; }
    public ICommand BatchDownloadCommand { get; }
    public ICommand BatchDeleteCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand CopyFileCommand { get; }
    public ICommand ExportFileCommand { get; }
    public ICommand BatchCopyCommand { get; }
    public ICommand BatchExportCommand { get; }

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
        SelectFileCommand = new Command(async () => await SelectFileAsync());
        PreloadFileCommand = new Command<SelectableFileItem>(async f => await PreloadFileAsync(f));
        OpenFileCommand = new Command<SelectableFileItem>(async f => await OpenFileAsync(f));
        DeleteFileCommand = new Command<SelectableFileItem>(async f => await DeleteFileAsync(f));
        ToggleFileSelectionCommand = new Command<SelectableFileItem>(ToggleFileSelection);
        BatchDownloadCommand = new Command(async () => await BatchDownloadAsync(), () => HasSelectedFiles);
        BatchDeleteCommand = new Command(async () => await BatchDeleteAsync(), () => HasSelectedFiles);
        ClearSelectionCommand = new Command(ClearSelection);
        CopyFileCommand = new Command<SelectableFileItem>(async f => await CopyFileAsync(f));
        ExportFileCommand = new Command<SelectableFileItem>(async f => await ExportFileAsync(f));
        BatchCopyCommand = new Command(async () => await BatchCopyAsync(), () => HasSelectedFiles);
        BatchExportCommand = new Command(async () => await BatchExportAsync(), () => HasSelectedFiles);

        // 监听连接状态变化
        _textHubClient.ConnectionStateChanged += OnConnectionStateChanged;
        _textHubClient.TextUpdateReceived += OnTextUpdateReceived;
        _textHubClient.FileUpdateReceived += OnFileUpdateReceived;
        _textHubClient.FilePositionChanged += OnFilePositionChanged;
    }

    public async Task InitializeAsync()
    {
        // 连接 SignalR
        await ConnectToHubAsync();

        // 加载初始文本
        await RefreshTextAsync();

        // 加载文件列表
        await RefreshFilesInternalAsync();

        // 自动预载所有远程文件（后台执行，不阻塞 UI）
        _ = Task.Run(async () => await AutoPreloadAllFilesAsync());
    }

    /// <summary>
    /// 自动预载所有远程文件
    /// </summary>
    private async Task AutoPreloadAllFilesAsync()
    {
        try
        {
            // 等待一小段时间，确保 UI 已渲染
            await Task.Delay(500);

            // 获取所有需要预载的文件（状态为 Remote）
            var filesToPreload = Files.Where(f => f.Status == FileStatus.Remote).ToList();

            foreach (var file in filesToPreload)
            {
                // 如果文件已经开始预载或已缓存，跳过
                if (file.Status != FileStatus.Remote)
                    continue;

                await PreloadFileAsync(file);

                // 每个文件之间稍微延迟，避免同时下载过多文件
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"自动预载文件失败: {ex.Message}");
        }
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

                    // 服务器已按 PositionY, PositionX 排序返回
                    // 直接添加到集合，FileGridView 会根据 Position(X,Y) 定位每个文件
                    foreach (var file in response.Data.Files)
                    {
                        var item = new SelectableFileItem(file)
                        {
                            Status = _cacheManager.GetFileStatus(file.Id),
                            DownloadProgress = _cacheManager.GetDownloadProgress(file.Id)
                        };
                        Files.Add(item);
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

    private async Task PreloadFileAsync(SelectableFileItem file)
    {
        try
        {
            // 如果已经缓存，不需要重复下载
            if (_cacheManager.IsCached(file.Id))
                return;

            var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);

            // 设置为预载中
            file.Status = FileStatus.Preloading;
            file.DownloadProgress = 0;
            _cacheManager.SetFileStatus(file.Id, FileStatus.Preloading);

            // 下载到缓存（不打开文件）
            var success = await _fileClient.DownloadFileToCacheAsync(
                file.Id,
                file.FileName,
                cachePath,
                (downloaded, total) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _cacheManager.UpdateDownloadProgress(file.Id, downloaded, total);
                        file.DownloadProgress = _cacheManager.GetDownloadProgress(file.Id);
                    });
                });

            if (success)
            {
                file.Status = FileStatus.Cached;
                _cacheManager.SetFileStatus(file.Id, FileStatus.Cached);
            }
            else
            {
                file.Status = FileStatus.Remote;
                _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
            }
        }
        catch (Exception ex)
        {
            file.Status = FileStatus.Remote;
            _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
            System.Diagnostics.Debug.WriteLine($"预载文件失败: {ex.Message}");
        }
    }

    private async Task OpenFileAsync(SelectableFileItem file)
    {
        try
        {
            var cachePath = _cacheManager.GetCachePath(file.Id, file.FileName);

            // 检查是否已缓存
            if (_cacheManager.IsCached(file.Id))
            {
                // 已缓存，直接打开
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(cachePath)
                });
                return;
            }

            // 未缓存，先下载
            file.Status = FileStatus.Preloading;
            file.DownloadProgress = 0;
            _cacheManager.SetFileStatus(file.Id, FileStatus.Preloading);

            var success = await _fileClient.DownloadFileToCacheAsync(
                file.Id,
                file.FileName,
                cachePath,
                (downloaded, total) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _cacheManager.UpdateDownloadProgress(file.Id, downloaded, total);
                        file.DownloadProgress = _cacheManager.GetDownloadProgress(file.Id);
                    });
                });

            if (success)
            {
                file.Status = FileStatus.Cached;
                _cacheManager.SetFileStatus(file.Id, FileStatus.Cached);

                // 下载完成，打开文件
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(cachePath)
                });
            }
            else
            {
                file.Status = FileStatus.Remote;
                _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
                await Application.Current!.MainPage!.DisplayAlert("下载失败", "无法下载文件", "确定");
            }
        }
        catch (Exception ex)
        {
            file.Status = FileStatus.Remote;
            _cacheManager.SetFileStatus(file.Id, FileStatus.Remote);
            System.Diagnostics.Debug.WriteLine($"打开文件失败: {ex.Message}");
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
                if (response.Success)
                {
                    // 删除本地缓存
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
        // 批量下载：只预载到缓存，不自动打开
        foreach (var file in SelectedFiles.ToList())
        {
            await PreloadFileAsync(file);
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
            // 确保文件已缓存
            if (!_cacheManager.IsCached(file.Id))
            {
                await PreloadFileAsync(file);
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
            // 确保文件已缓存
            if (!_cacheManager.IsCached(file.Id))
            {
                await PreloadFileAsync(file);
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
                // 确保文件已缓存
                if (!_cacheManager.IsCached(file.Id))
                {
                    await PreloadFileAsync(file);
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
                // 确保文件已缓存
                if (!_cacheManager.IsCached(file.Id))
                {
                    await PreloadFileAsync(file);
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

    private void OnFilePositionChanged(int fileId, int positionX, int positionY)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var file = Files.FirstOrDefault(f => f.Id == fileId);
            if (file != null)
            {
                var currentIndex = Files.IndexOf(file);

                // 检查位置是否真的变化了
                if (file.PositionX != positionX || file.PositionY != positionY)
                {
                    // 创建更新的文件项（保留其他状态）
                    var updatedDto = new FileItemDto
                    {
                        Id = file.Id,
                        FileName = file.FileName,
                        FileSize = file.FileSize,
                        MimeType = file.MimeType,
                        UploadedAt = file.UploadedAt,
                        ExpiresAt = file.ExpiresAt,
                        PositionX = positionX,
                        PositionY = positionY
                    };

                    var updatedItem = new SelectableFileItem(updatedDto)
                    {
                        Status = file.Status,
                        DownloadProgress = file.DownloadProgress,
                        IsSelected = file.IsSelected
                    };

                    // 替换列表中的文件项
                    if (currentIndex >= 0)
                    {
                        Files[currentIndex] = updatedItem;
                    }
                }
            }
        });
    }

    #region 拖放支持

    /// <summary>
    /// 将拖动的文件移动到目标位置（插入到目标之前）
    /// </summary>
    public async Task SwapFilePositionsAsync(SelectableFileItem draggedItem, SelectableFileItem targetItem)
    {
        try
        {
            var draggedIndex = Files.IndexOf(draggedItem);
            var targetIndex = Files.IndexOf(targetItem);

            if (draggedIndex < 0 || targetIndex < 0 || draggedIndex == targetIndex)
            {
                return;
            }

            // 在列表中移动文件
            Files.Move(draggedIndex, targetIndex);

            // 只通知服务器被拖动文件的新位置
            // 服务器会广播给其他客户端，其他客户端通过 OnFilePositionChanged 更新
            await _textHubClient.UpdateFilePositionAsync(draggedItem.Id, targetIndex, 0);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"移动文件位置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新文件位置（仅更新到服务器）
    /// </summary>
    public async Task UpdateFilePositionAsync(int fileId, int positionX, int positionY)
    {
        try
        {
            await _textHubClient.UpdateFilePositionAsync(fileId, positionX, positionY);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"更新文件位置失败: {ex.Message}");
        }
    }

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

    public void Dispose()
    {
        _textHubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _textHubClient.TextUpdateReceived -= OnTextUpdateReceived;
        _textHubClient.FileUpdateReceived -= OnFileUpdateReceived;
        _textHubClient.FilePositionChanged -= OnFilePositionChanged;
        _throttleCts?.Cancel();
        _throttleCts?.Dispose();
    }
}
