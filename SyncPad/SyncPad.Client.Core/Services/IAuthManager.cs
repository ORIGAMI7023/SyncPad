using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// 认证状态管理接口
/// </summary>
public interface IAuthManager
{
    /// <summary>
    /// 当前用户是否已登录
    /// </summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// 当前用户名
    /// </summary>
    string? Username { get; }

    /// <summary>
    /// 当前用户 ID
    /// </summary>
    int? UserId { get; }

    /// <summary>
    /// 当前 Token
    /// </summary>
    string? Token { get; }

    /// <summary>
    /// 登录状态变化事件
    /// </summary>
    event Action<bool>? LoginStateChanged;

    /// <summary>
    /// 登录
    /// </summary>
    Task<LoginResponse> LoginAsync(string username, string password);

    /// <summary>
    /// 登出
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 尝试从存储恢复登录状态
    /// </summary>
    Task<bool> TryRestoreSessionAsync();

    /// <summary>
    /// 获取 Hub URL
    /// </summary>
    string GetHubUrl();
}
