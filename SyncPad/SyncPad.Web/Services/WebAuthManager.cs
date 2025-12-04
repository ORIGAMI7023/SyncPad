using SyncPad.Client.Core.Services;
using SyncPad.Shared.Models;

namespace SyncPad.Web.Services;

/// <summary>
/// Web 版本的认证状态管理（继承自 AuthManager，重写 Hub URL）
/// </summary>
public class WebAuthManager : IAuthManager
{
    private readonly IApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;

    // 服务器地址（根据编译配置切换）
#if DEBUG
    private readonly string _baseUrl = "https://localhost:7167";
#else
    private readonly string _baseUrl = "https://syncpad.origami7023.net.cn";
#endif

    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);
    public string? Username { get; private set; }
    public int? UserId { get; private set; }
    public string? Token { get; private set; }

    public event Action<bool>? LoginStateChanged;

    public WebAuthManager(IApiClient apiClient, ITokenStorage tokenStorage)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _apiClient.SetBaseUrl(_baseUrl);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var request = new LoginRequest
        {
            Username = username,
            Password = password
        };

        var response = await _apiClient.LoginAsync(request);

        if (response.Success && !string.IsNullOrEmpty(response.Token))
        {
            Token = response.Token;
            Username = response.Username;
            UserId = response.UserId;

            _apiClient.SetToken(Token);
            await _tokenStorage.SaveTokenAsync(Token, Username!, UserId!.Value);

            LoginStateChanged?.Invoke(true);
        }

        return response;
    }

    public async Task LogoutAsync()
    {
        Token = null;
        Username = null;
        UserId = null;

        _apiClient.SetToken(null);
        await _tokenStorage.ClearTokenAsync();

        LoginStateChanged?.Invoke(false);
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var (token, username, userId) = await _tokenStorage.GetTokenAsync();

            if (!string.IsNullOrEmpty(token))
            {
                Token = token;
                Username = username;
                UserId = userId;

                _apiClient.SetToken(Token);
                LoginStateChanged?.Invoke(true);
                return true;
            }
        }
        catch
        {
            // 忽略 JS 互操作错误（首次渲染时可能发生）
        }

        return false;
    }

    public string GetHubUrl()
    {
        return $"{_baseUrl}/hubs/text";
    }
}
