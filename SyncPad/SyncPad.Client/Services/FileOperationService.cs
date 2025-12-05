using Microsoft.Maui.Storage;
using SyncPad.Client.Core.Services;

namespace SyncPad.Client.Services;

/// <summary>
/// 跨平台文件操作服务实现
/// </summary>
public class FileOperationService : IFileOperationService
{
    public bool CopyFilesToClipboard(IEnumerable<string> filePaths)
    {
#if MACCATALYST
        return Platforms.MacCatalyst.ClipboardService.CopyFilesToClipboard(filePaths);
#elif WINDOWS
        return Platforms.Windows.ClipboardService.CopyFilesToClipboard(filePaths);
#else
        // iOS/Android 不支持文件复制到剪贴板
        return false;
#endif
    }

    public bool CopyFileToClipboard(string filePath)
    {
        return CopyFilesToClipboard(new[] { filePath });
    }

    public async Task<bool> ExportFileAsync(string sourceFilePath, string targetDirectory)
    {
        try
        {
            if (!File.Exists(sourceFilePath))
                return false;

            var fileName = Path.GetFileName(sourceFilePath);
            var targetPath = Path.Combine(targetDirectory, fileName);

            // 如果目标文件已存在，添加数字后缀
            if (File.Exists(targetPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                int counter = 1;
                do
                {
                    targetPath = Path.Combine(targetDirectory, $"{nameWithoutExt} ({counter}){ext}");
                    counter++;
                } while (File.Exists(targetPath));
            }

            await Task.Run(() => File.Copy(sourceFilePath, targetPath));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileOperationService] 导出文件失败: {ex.Message}");
            return false;
        }
    }

    public async Task<int> ExportFilesAsync(IEnumerable<string> sourceFilePaths, string targetDirectory)
    {
        int successCount = 0;
        foreach (var filePath in sourceFilePaths)
        {
            if (await ExportFileAsync(filePath, targetDirectory))
                successCount++;
        }
        return successCount;
    }

    public async Task<string?> PickFolderAsync()
    {
        try
        {
#if MACCATALYST
            return await Platforms.MacCatalyst.FolderPickerService.PickFolderAsync();
#elif WINDOWS
            // TODO: 实现 Windows 原生文件夹选择器
            await Task.CompletedTask;
            return null;
#else
            await Task.CompletedTask;
            return null;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileOperationService] 选择文件夹失败: {ex.Message}");
            return null;
        }
    }
}
