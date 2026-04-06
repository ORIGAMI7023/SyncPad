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
        return _cacheManager.FindCachedFile(file.FileName);
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
        DownloadFileCommand = new Command<SelectableFileItem>(async f => await DownloadAndSaveAsync(f));
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
            // 读取文件到内存流
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

            // 写入临时文件计算 XXHash64
            var tempPath = Path.Combine(Path.GetTempPath(), $"syncpad_upload_{Guid.NewGuid()}");
            try
            {
                await File.WriteAllBytesAsync(tempPath, memoryStream.ToArray());
                var hash = _cacheManager.ComputeXXHash64(tempPath);
                if (hash == null)
                {
                    await Application.Current!.MainPage!.DisplayAlert("上传失败", "无法计算文件哈希", "确定");
                    return;
                }

                // 第一步：检查 hash
                var checkResult = await _fileClient.CheckHashAsync(hash);
                if (checkResult.Success && checkResult.Data != null)
                {
                    if (checkResult.Data.Exists && checkResult.Data.Status == "active")
                    {
                        await Application.Current!.MainPage!.DisplayAlert("提示", "文件已存在", "确定");
                        return;
                    }

                    if (checkResult.Data.Exists && checkResult.Data.Status == "cached")
                    {
                        // 秒传
                        var instantResponse = await _fileClient.InstantUploadAsync(fileResult.FileName, hash);
                        if (!instantResponse.Success)
                        {
                            await Application.Current!.MainPage!.DisplayAlert("上传失败", instantResponse.ErrorMessage, "确定");
                        }
                        return;
                    }
                }

                // 正常上传
                memoryStream.Position = 0;
                var contentType = fileResult.ContentType ?? "application/octet-stream";
                var response = await _fileClient.UploadFileAsync(
                    fileResult.FileName,
                    memoryStream,
                    contentType,
                    hash,
                    overwrite: true);

                if (!response.Success)
                {
                    await Application.Current!.MainPage!.DisplayAlert("上传失败", response.ErrorMessage, "确定");
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
            await Application.Current!.MainPage!.DisplayAlert("上传失败", ex.Message, "确定");
        }
    }

    /// <summary>
    /// 下载文件到缓存（XXHash64 命名），返回缓存路径
    /// </summary>
    private async Task<string?> DownloadToCacheAsync(SelectableFileItem file)
    {
        try
        {
            // 优先用 hash 查缓存
            var cachedPath = _cacheManager.FindCachedFileByHash(file.Hash);
            if (cachedPath != null)
                return cachedPath;

            // 下载到临时路径
            var cacheDir = ((FileCacheManager)_cacheManager).GetCacheDirectory();
            var tempPath = Path.Combine(cacheDir, $"tmp_{file.Id}_{file.FileName}");

            var success = await _fileClient.DownloadFileToCacheAsync(
                file.Id,
                file.FileName,
                tempPath,
                null);

            if (!success)
                return null;

            // 计算 XXHash64
            var hash = _cacheManager.ComputeXXHash64(tempPath);
            var safeFileName = file.FileName.Replace("/", "_").Replace("\\", "_");

            string finalPath;
            if (hash != null)
            {
                finalPath = Path.Combine(cacheDir, $"{hash}_{safeFileName}");
            }
            else
            {
                finalPath = Path.Combine(cacheDir, $"0000000000000000_{safeFileName}");
            }

            // 如果目标已存在（同内容），删除临时文件
            if (File.Exists(finalPath))
            {
                File.Delete(tempPath);
                return finalPath;
            }

            // 移动到最终缓存路径
            File.Move(tempPath, finalPath);
            return finalPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"下载文件到缓存失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 统一流程：下载到缓存 → 复制到 Downloads → 打开资源管理器定位
    /// </summary>
    public async Task DownloadAndSaveAsync(SelectableFileItem file)
    {
        try
        {
            var cachePath = await DownloadToCacheAsync(file);
            if (cachePath == null)
            {
                await Application.Current!.MainPage!.DisplayAlert("下载失败", "无法下载文件，请检查网络连接", "确定");
                return;
            }

            // 复制到 Downloads 目录
            var downloadsDir = GetDownloadsDirectory();
            var destPath = Path.Combine(downloadsDir, file.FileName);

            // 处理同名文件
            var counter = 1;
            while (File.Exists(destPath))
            {
                var name = Path.GetFileNameWithoutExtension(file.FileName);
                var ext = Path.GetExtension(file.FileName);
                var newName = string.IsNullOrEmpty(ext) ? $"{name} ({counter})" : $"{name} ({counter}){ext}";
                destPath = Path.Combine(downloadsDir, newName);
                counter++;
            }

            File.Copy(cachePath, destPath);

            // 打开资源管理器定位
            OpenInFileExplorer(destPath);

            // 移除下载成功弹窗，文件已在资源管理器中打开
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("下载失败", $"错误：{ex.Message}", "确定");
        }
    }

    /// <summary>
    /// 打开文件：下载到缓存 → 复制到 Downloads → 用默认应用打开 → 定位到文件
    /// </summary>
    public async Task OpenFileAsync(SelectableFileItem file)
    {
        try
        {
            var cachePath = await DownloadToCacheAsync(file);
            if (cachePath == null)
            {
                await Application.Current!.MainPage!.DisplayAlert("打开失败", "无法下载文件，请检查网络连接", "确定");
                return;
            }

            // 复制到 Downloads 目录
            var downloadsDir = GetDownloadsDirectory();
            var destPath = Path.Combine(downloadsDir, file.FileName);

            // 处理同名文件
            var counter = 1;
            while (File.Exists(destPath))
            {
                var name = Path.GetFileNameWithoutExtension(file.FileName);
                var ext = Path.GetExtension(file.FileName);
                var newName = string.IsNullOrEmpty(ext) ? $"{name} ({counter})" : $"{name} ({counter}){ext}";
                destPath = Path.Combine(downloadsDir, newName);
                counter++;
            }

            File.Copy(cachePath, destPath);

            // 打开资源管理器定位
            OpenInFileExplorer(destPath);

            // 用默认应用打开文件
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(destPath)
            });
        }
        catch (Exception ex)
        {
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
                    await _cacheManager.DeleteCacheAsync(file.FileName);
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
            await DownloadAndSaveAsync(file);
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
            var cachePath = await DownloadToCacheAsync(file);
            if (cachePath != null && File.Exists(cachePath))
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
            var cachePath = await DownloadToCacheAsync(file);
            if (cachePath == null || !File.Exists(cachePath))
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
                var cachePath = await DownloadToCacheAsync(file);
                if (cachePath != null && File.Exists(cachePath))
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
                var cachePath = await DownloadToCacheAsync(file);
                if (cachePath != null && File.Exists(cachePath))
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
    /// 上传文件（供外部调用，含 hash 计算）
    /// </summary>
    public async Task UploadFileAsync(string fileName, Stream stream, string contentType, bool overwrite = false)
    {
        try
        {
            // 写入临时文件计算 hash
            var tempPath = Path.Combine(Path.GetTempPath(), $"syncpad_upload_{Guid.NewGuid()}");
            try
            {
                // 读取流到字节数组
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                await File.WriteAllBytesAsync(tempPath, bytes);

                var hash = _cacheManager.ComputeXXHash64(tempPath);
                if (hash == null)
                {
                    await Application.Current!.MainPage!.DisplayAlert("上传失败", "无法计算文件哈希", "确定");
                    return;
                }

                // 检查 hash
                var checkResult = await _fileClient.CheckHashAsync(hash);
                if (checkResult.Success && checkResult.Data != null)
                {
                    if (checkResult.Data.Exists && checkResult.Data.Status == "active")
                    {
                        await Application.Current!.MainPage!.DisplayAlert("提示", "文件已存在", "确定");
                        return;
                    }

                    if (checkResult.Data.Exists && checkResult.Data.Status == "cached")
                    {
                        var instantResponse = await _fileClient.InstantUploadAsync(fileName, hash);
                        if (!instantResponse.Success)
                        {
                            await Application.Current!.MainPage!.DisplayAlert("上传失败", instantResponse.ErrorMessage, "确定");
                        }
                        return;
                    }
                }

                // 正常上传
                using var uploadStream = new MemoryStream(bytes);
                var response = await _fileClient.UploadFileAsync(fileName, uploadStream, contentType, hash, overwrite);
                if (!response.Success)
                {
                    await Application.Current!.MainPage!.DisplayAlert("上传失败", response.ErrorMessage, "确定");
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
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

            string oldFileName = file.FileName;

            var response = await _fileClient.RenameFileAsync(file.Id, newName);
            if (response.Success)
            {
                // 删除旧缓存（如果存在）
                await _cacheManager.DeleteCacheAsync(oldFileName);

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

    // MARK: - Helpers

    /// <summary>
    /// 获取 Downloads 目录
    /// </summary>
    private static string GetDownloadsDirectory()
    {
        // Windows: 用户目录\Downloads
        // macOS (MAUI): 用户目录\Downloads
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "Downloads");
    }

    /// <summary>
    /// 在文件资源管理器中打开并定位到文件
    /// </summary>
    private static void OpenInFileExplorer(string filePath)
    {
        try
        {
#if WINDOWS
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
#elif MACCATALYST || MACOS
            System.Diagnostics.Process.Start("open", $"-R \"{filePath}\"");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"打开文件管理器失败: {ex.Message}");
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
