namespace SyncPad.Client.Core.Services;

/// <summary>
/// 跨平台文件操作服务接口
/// </summary>
public interface IFileOperationService
{
    /// <summary>
    /// 将文件复制到系统剪贴板（支持在文件管理器中粘贴）
    /// </summary>
    /// <param name="filePaths">要复制的文件路径列表</param>
    /// <returns>是否成功</returns>
    bool CopyFilesToClipboard(IEnumerable<string> filePaths);

    /// <summary>
    /// 将单个文件复制到系统剪贴板
    /// </summary>
    bool CopyFileToClipboard(string filePath);

    /// <summary>
    /// 导出文件到指定目录
    /// </summary>
    /// <param name="sourceFilePath">源文件路径</param>
    /// <param name="targetDirectory">目标目录</param>
    /// <returns>是否成功</returns>
    Task<bool> ExportFileAsync(string sourceFilePath, string targetDirectory);

    /// <summary>
    /// 批量导出文件到指定目录
    /// </summary>
    Task<int> ExportFilesAsync(IEnumerable<string> sourceFilePaths, string targetDirectory);

    /// <summary>
    /// 选择目录（用于导出）
    /// </summary>
    /// <returns>选择的目录路径，取消返回 null</returns>
    Task<string?> PickFolderAsync();
}
