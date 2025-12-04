using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 用户登录
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// 验证 Token 并获取用户 ID
    /// </summary>
    int? ValidateToken(string token);
}
