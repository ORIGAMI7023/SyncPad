using SyncPad.Client.ViewModels;
#if WINDOWS
using SyncPad.Client.Platforms.Windows;
using Windows.Storage;
#endif
#if MACCATALYST
using SyncPad.Client.Platforms.MacCatalyst;
#endif

namespace SyncPad.Client.Views;

public partial class PadPage : ContentPage
{
    private readonly PadViewModel _viewModel;

    // 选择相关
    private int _lastSelectedIndex = -1;
    private bool _isCtrlPressed;
    private bool _isShiftPressed;
    private bool _isEditorFocused;

    public PadPage(PadViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        viewModel.LogoutRequested += OnLogoutRequested;

#if WINDOWS
        SetupWindowsDragDrop();
#endif

#if MACCATALYST
        SetupMacDragDrop();
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    private async void OnLogoutRequested()
    {
        await Shell.Current.GoToAsync("//LoginPage");
    }

#if WINDOWS
    private void SetupWindowsDragDrop()
    {
        // 只为外层 Grid 设置拖放支持，避免重复触发
        DragDropHandler.SetupDropTarget(
            FileAreaGrid,
            onFilesDropped: async (files, x, y) =>
            {
                System.Diagnostics.Debug.WriteLine($"[DragDrop] 检测到 {files.Count} 个文件拖入");

                foreach (var file in files)
                {
                    await UploadStorageFileAsync(file);
                }
            }
        );
    }

    private async Task UploadStorageFileAsync(StorageFile storageFile)
    {
        try
        {
            using var stream = await storageFile.OpenStreamForReadAsync();
            var contentType = storageFile.ContentType ?? "application/octet-stream";

            bool exists = await _viewModel.FileExistsAsync(storageFile.Name);

            if (exists)
            {
                var confirm = await DisplayAlert("文件已存在",
                    $"服务器上已存在文件 \"{storageFile.Name}\"，是否覆盖？",
                    "覆盖", "取消");
                if (!confirm)
                    return;
            }

            await _viewModel.UploadFileAsync(storageFile.Name, stream, contentType, exists);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
        }
    }
#endif

#if MACCATALYST
    private void SetupMacDragDrop()
    {
        DragDropHandler.SetupDropTarget(
            FileCollectionView,
            onFilesDropped: async (filePaths) =>
            {
                foreach (var filePath in filePaths)
                {
                    await UploadFileFromPathAsync(filePath);
                }
            }
        );
    }

    private async Task UploadFileFromPathAsync(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var contentType = GetMimeType(filePath);

            bool exists = await _viewModel.FileExistsAsync(fileName);

            if (exists)
            {
                var confirm = await DisplayAlert("文件已存在",
                    $"文件 \"{fileName}\" 已存在，是否覆盖？",
                    "覆盖", "取消");
                if (!confirm)
                    return;
            }

            using var stream = File.OpenRead(filePath);
            await _viewModel.UploadFileAsync(fileName, stream, contentType, exists);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"上传文件失败: {ex.Message}");
        }
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".zip" => "application/zip",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            _ => "application/octet-stream"
        };
    }
#endif

    #region 外部文件拖入上传（MAUI 标准方式）

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnFileDrop(object? sender, DropEventArgs e)
    {
        var properties = e.Data?.Properties;
        if (properties == null)
            return;

        // Windows 平台通过 DragDropHandler 处理外部文件拖入
        // 这里不需要额外处理
    }

    #endregion

    #region 文件选择（单击/Ctrl/Shift）

    private void OnFileCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Element element && element.BindingContext is SelectableFileItem item)
        {
            var currentIndex = _viewModel.Files.IndexOf(item);
            if (currentIndex < 0) return;

            // 检测右键点击（通过 Buttons 属性）
            var isRightClick = e.Buttons.ToString().Contains("Right");

            if (isRightClick)
            {
                // 右键点击：直接选中当前项，不清除其他选中项（除非没有按 Ctrl）
                if (!_isCtrlPressed)
                {
                    foreach (var f in _viewModel.Files)
                    {
                        f.IsSelected = false;
                    }
                }
                item.IsSelected = true;
                _lastSelectedIndex = currentIndex;
                _viewModel.NotifySelectionChanged();
                return; // 右键点击只处理选中，不处理其他逻辑
            }

            // 左键点击的原有逻辑
            if (_isCtrlPressed)
            {
                item.IsSelected = !item.IsSelected;
                _lastSelectedIndex = item.IsSelected ? currentIndex : -1;
            }
            else if (_isShiftPressed && _lastSelectedIndex >= 0 && _lastSelectedIndex < _viewModel.Files.Count)
            {
                var startIndex = Math.Min(_lastSelectedIndex, currentIndex);
                var endIndex = Math.Max(_lastSelectedIndex, currentIndex);
                startIndex = Math.Max(0, startIndex);
                endIndex = Math.Min(_viewModel.Files.Count - 1, endIndex);

                foreach (var f in _viewModel.Files)
                {
                    f.IsSelected = false;
                }

                for (int i = startIndex; i <= endIndex; i++)
                {
                    _viewModel.Files[i].IsSelected = true;
                }
            }
            else
            {
                foreach (var f in _viewModel.Files)
                {
                    f.IsSelected = false;
                }
                item.IsSelected = true;
                _lastSelectedIndex = currentIndex;
            }

            _viewModel.NotifySelectionChanged();
        }
    }

    private void OnFileAreaTapped(object? sender, TappedEventArgs e)
    {
        _viewModel.ClearAllSelection();
    }

    #endregion

    #region 移动端标签页切换

    private void OnTextTabClicked(object? sender, EventArgs e)
    {
        if (TextTabContent != null && FileTabContent != null &&
            TextTabButton != null && FileTabButton != null)
        {
            TextTabContent.IsVisible = true;
            FileTabContent.IsVisible = false;

            TextTabButton.FontAttributes = FontAttributes.Bold;
            FileTabButton.FontAttributes = FontAttributes.None;

            var isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            TextTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#1E1E1E") : Colors.White;
            FileTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#2B2B2B") : Color.FromArgb("#E9ECEF");
        }
    }

    private void OnFileTabClicked(object? sender, EventArgs e)
    {
        if (TextTabContent != null && FileTabContent != null &&
            TextTabButton != null && FileTabButton != null)
        {
            TextTabContent.IsVisible = false;
            FileTabContent.IsVisible = true;

            TextTabButton.FontAttributes = FontAttributes.None;
            FileTabButton.FontAttributes = FontAttributes.Bold;

            var isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            TextTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#2B2B2B") : Color.FromArgb("#E9ECEF");
            FileTabButton.BackgroundColor = isDarkTheme ? Color.FromArgb("#1E1E1E") : Colors.White;
        }
    }

    #endregion

    #region 右键选中（平台特定）

    private void OnFileItemFrameLoaded(object? sender, EventArgs e)
    {
#if WINDOWS
        if (sender is Frame frame && frame.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeElement)
        {
            nativeElement.RightTapped += (s, args) =>
            {
                if (frame.BindingContext is SelectableFileItem file)
                {
                    SelectFileForContextMenu(file);
                }
            };
        }
#endif
    }

    #endregion

    #region 右键菜单事件

    // 在右键菜单打开前先选中文件
    private void SelectFileForContextMenu(SelectableFileItem file)
    {
        if (!_isCtrlPressed)
        {
            // 如果没有按 Ctrl，清除其他选中项
            foreach (var f in _viewModel.Files)
            {
                f.IsSelected = false;
            }
        }
        file.IsSelected = true;
        _lastSelectedIndex = _viewModel.Files.IndexOf(file);
        _viewModel.NotifySelectionChanged();
    }

    private async void OnContextMenuOpen(object? sender, EventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem item && item.BindingContext is SelectableFileItem file)
            {
                SelectFileForContextMenu(file);
                await _viewModel.OpenFileAsync(file);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] 打开文件失败: {ex.Message}");
        }
    }

    private async void OnContextMenuRename(object? sender, EventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem item && item.BindingContext is SelectableFileItem file)
            {
                SelectFileForContextMenu(file);
                string newName = await DisplayPromptAsync("重命名文件", "请输入新的文件名：", initialValue: file.FileName, accept: "重命名", cancel: "取消");
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    await _viewModel.RenameFileAsync(file, newName);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] 重命名文件失败: {ex.Message}");
        }
    }

    private async void OnContextMenuDownload(object? sender, EventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem item && item.BindingContext is SelectableFileItem file)
            {
                SelectFileForContextMenu(file);
                await _viewModel.DownloadAndSaveAsync(file);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] 下载文件失败: {ex.Message}");
        }
    }

    private async void OnContextMenuDelete(object? sender, EventArgs e)
    {
        try
        {
            if (sender is MenuFlyoutItem item && item.BindingContext is SelectableFileItem file)
            {
                SelectFileForContextMenu(file);
                await _viewModel.DeleteFileAsync(file, showConfirmation: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ContextMenu] 删除文件失败: {ex.Message}");
        }
    }

    #endregion

    #region 文本编辑事件

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is Editor editor && !string.IsNullOrEmpty(e.NewTextValue))
        {
            if (_viewModel.Content != e.NewTextValue)
            {
                _viewModel.Content = e.NewTextValue;
            }
        }
    }

    #endregion

    #region 键盘事件（用于检测 Ctrl/Shift）

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if WINDOWS
        SetupWindowsKeyboardHandling();
#endif
    }

#if WINDOWS
    private Microsoft.UI.Xaml.Window? _nativeWindow;

    private void SetupWindowsKeyboardHandling()
    {
        var mauiWindow = Application.Current?.Windows.FirstOrDefault();
        if (mauiWindow?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            _nativeWindow = nativeWindow;

            if (nativeWindow.Content is Microsoft.UI.Xaml.UIElement rootElement)
            {
                rootElement.PreviewKeyDown += OnPreviewKeyDown;
                rootElement.PreviewKeyUp += OnPreviewKeyUp;
            }
        }
    }

    private async void OnPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Control)
        {
            _isCtrlPressed = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Shift)
        {
            _isShiftPressed = true;
        }
        else if (e.Key == Windows.System.VirtualKey.A && _isCtrlPressed)
        {
            // 只在焦点不在 Editor 时全选文件
            if (!_isEditorFocused)
            {
                foreach (var file in _viewModel.Files)
                {
                    file.IsSelected = true;
                }
                _viewModel.NotifySelectionChanged();
                e.Handled = true;
            }
            // 否则让 Editor 处理 Ctrl+A（不设置 e.Handled）
        }
        else if (e.Key == Windows.System.VirtualKey.Delete)
        {
            var selectedFiles = _viewModel.Files.Where(f => f.IsSelected).ToList();
            if (selectedFiles.Count > 0)
            {
                foreach (var file in selectedFiles)
                {
                    await _viewModel.DeleteFileAsync(file, showConfirmation: false);
                }
            }
        }
    }

    private void OnPreviewKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Control)
        {
            _isCtrlPressed = false;
        }
        else if (e.Key == Windows.System.VirtualKey.Shift)
        {
            _isShiftPressed = false;
        }
    }

#endif

    private void OnEditorFocused(object? sender, FocusEventArgs e)
    {
        _isEditorFocused = true;
    }

    private void OnEditorUnfocused(object? sender, FocusEventArgs e)
    {
        _isEditorFocused = false;
    }

    #endregion
}
