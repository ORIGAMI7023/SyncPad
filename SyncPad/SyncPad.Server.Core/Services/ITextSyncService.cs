using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

/// <summary>
/// 文本同步服务接口
/// </summary>
public interface ITextSyncService
{
    /// <summary>
    /// 获取用户的文本内容
    /// </summary>
    Task<TextSyncMessage?> GetTextAsync(int userId);

    /// <summary>
    /// 更新用户的文本内容
    /// </summary>
    Task<TextSyncMessage> UpdateTextAsync(int userId, string content);
}
