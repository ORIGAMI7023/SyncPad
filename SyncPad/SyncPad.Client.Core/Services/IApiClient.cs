using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// HTTP API 客户端接口
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// 设置 API 基地址
    /// </summary>
    void SetBaseUrl(string baseUrl);

    /// <summary>
    /// 设置认证 Token
    /// </summary>
    void SetToken(string? token);

    /// <summary>
    /// 用户登录
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// 获取文本内容
    /// </summary>
    Task<ApiResponse<TextSyncMessage>> GetTextAsync();

    /// <summary>
    /// 更新文本内容
    /// </summary>
    Task<ApiResponse<TextSyncMessage>> UpdateTextAsync(TextSyncMessage message);
}
