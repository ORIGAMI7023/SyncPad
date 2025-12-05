namespace SyncPad.Client.Platforms.iOS;

/// <summary>
/// iOS 端剪贴板服务（iOS不支持文件复制到剪贴板）
/// </summary>
public static class ClipboardService
{
    /// <summary>
    /// iOS 不支持将文件复制到系统剪贴板
    /// </summary>
    public static bool CopyFilesToClipboard(IEnumerable<string> filePaths)
    {
        // iOS 系统限制，不支持文件复制到剪贴板
        return false;
    }

    /// <summary>
    /// iOS 不支持将单个文件复制到系统剪贴板
    /// </summary>
    public static bool CopyFileToClipboard(string filePath)
    {
        return false;
    }
}
