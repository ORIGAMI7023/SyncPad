using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SyncPad.Client.Platforms.Windows;

/// <summary>
/// Windows 端剪贴板服务，支持复制文件到系统剪贴板
/// </summary>
public static class ClipboardService
{
    /// <summary>
    /// 将文件复制到系统剪贴板（支持在资源管理器中粘贴）
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

            var dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;

            // 异步获取 StorageFile 对象
            var task = Task.Run(async () =>
            {
                var storageFiles = new List<IStorageItem>();
                foreach (var path in paths)
                {
                    try
                    {
                        var file = await StorageFile.GetFileFromPathAsync(path);
                        storageFiles.Add(file);
                    }
                    catch
                    {
                        // 忽略无法访问的文件
                    }
                }
                return storageFiles;
            });

            var files = task.GetAwaiter().GetResult();
            if (files.Count == 0)
                return false;

            dataPackage.SetStorageItems(files);
            Clipboard.SetContent(dataPackage);

            System.Diagnostics.Debug.WriteLine($"[ClipboardService] 已复制 {files.Count} 个文件到剪贴板");
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
