namespace SyncPad.Shared.Models;

/// <summary>
/// 文件重命名请求
/// </summary>
public class RenameFileRequest
{
    /// <summary>
    /// 新文件名
    /// </summary>
    public string NewFileName { get; set; } = string.Empty;
}