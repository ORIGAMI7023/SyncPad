using Foundation;
using UIKit;

namespace SyncPad.Client.Platforms.MacCatalyst;

/// <summary>
/// Mac 端剪贴板服务，支持复制文件到系统剪贴板
/// </summary>
public static class ClipboardService
{
    /// <summary>
    /// 将文件复制到系统剪贴板（支持在 Finder 中粘贴）
    /// </summary>
    /// <param name="filePaths">要复制的文件路径列表</param>
    /// <returns>是否成功</returns>
    public static bool CopyFilesToClipboard(IEnumerable<string> filePaths)
    {
        try
        {
            var paths = filePaths.Where(File.Exists).ToArray();
            if (paths.Length == 0)
                return false;

            var pasteboard = UIPasteboard.General;

            // 清空剪贴板
            pasteboard.Items = Array.Empty<NSDictionary>();

            // 创建文件 URL 数组
            var urls = paths.Select(p => NSUrl.FromFilename(p)).ToArray();

            // 设置文件 URL 到剪贴板
            // 使用 public.file-url UTI 类型
            pasteboard.SetValue(
                NSArray.FromObjects(urls),
                "public.file-url");

            System.Diagnostics.Debug.WriteLine($"[ClipboardService] 已复制 {paths.Length} 个文件到剪贴板");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClipboardService] 复制文件失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 将单个文件复制到系统剪贴板
    /// </summary>
    public static bool CopyFileToClipboard(string filePath)
    {
        return CopyFilesToClipboard(new[] { filePath });
    }
}
