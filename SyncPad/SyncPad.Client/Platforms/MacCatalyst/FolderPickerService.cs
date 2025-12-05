using Foundation;
using UIKit;
using UniformTypeIdentifiers;

namespace SyncPad.Client.Platforms.MacCatalyst;

/// <summary>
/// Mac 端文件夹选择器
/// </summary>
public static class FolderPickerService
{
    /// <summary>
    /// 选择文件夹
    /// </summary>
    /// <returns>选择的文件夹路径，取消返回 null</returns>
    public static Task<string?> PickFolderAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Mac Catalyst 使用 UIDocumentPickerViewController
                var picker = new UIDocumentPickerViewController(
                    new[] { UTTypes.Folder },
                    asCopy: false);

                picker.AllowsMultipleSelection = false;
                picker.ShouldShowFileExtensions = true;

                picker.DidPickDocumentAtUrls += (sender, e) =>
                {
                    if (e.Urls?.Length > 0)
                    {
                        var url = e.Urls[0];
                        // 请求访问权限
                        url.StartAccessingSecurityScopedResource();
                        tcs.SetResult(url.Path);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                };

                picker.WasCancelled += (sender, e) =>
                {
                    tcs.SetResult(null);
                };

                // 获取当前的 ViewController
                var viewController = Platform.GetCurrentUIViewController();
                if (viewController != null)
                {
                    viewController.PresentViewController(picker, true, null);
                }
                else
                {
                    tcs.SetResult(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderPickerService] 选择文件夹失败: {ex.Message}");
                tcs.SetResult(null);
            }
        });

        return tcs.Task;
    }
}
