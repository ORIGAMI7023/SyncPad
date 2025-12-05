using Foundation;
using AppKit;

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
                var openPanel = NSOpenPanel.OpenPanel;
                openPanel.CanChooseFiles = false;
                openPanel.CanChooseDirectories = true;
                openPanel.AllowsMultipleSelection = false;
                openPanel.Title = "选择导出文件夹";
                openPanel.Prompt = "选择";

                var result = openPanel.RunModal();

                if (result == 1 && openPanel.Urls.Length > 0)
                {
                    var url = openPanel.Urls[0];
                    tcs.SetResult(url.Path);
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
