namespace SyncPad.Client.Core.Services;

/// <summary>
/// Token 存储接口（不同平台有不同实现）
/// </summary>
public interface ITokenStorage
{
    /// <summary>
    /// 保存 Token
    /// </summary>
    Task SaveTokenAsync(string token, string username, int userId);

    /// <summary>
    /// 获取 Token
    /// </summary>
    Task<(string? Token, string? Username, int? UserId)> GetTokenAsync();

    /// <summary>
    /// 清除 Token
    /// </summary>
    Task ClearTokenAsync();
}
