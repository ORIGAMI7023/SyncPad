using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// 认证状态管理实现
/// </summary>
public class AuthManager : IAuthManager
{
    private readonly IApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;

    // 默认服务器地址（开发环境）
    private string _baseUrl = "https://localhost:7001";

    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);
    public string? Username { get; private set; }
    public int? UserId { get; private set; }
    public string? Token { get; private set; }

    public event Action<bool>? LoginStateChanged;

    public AuthManager(IApiClient apiClient, ITokenStorage tokenStorage)
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

        return false;
    }

    public string GetHubUrl()
    {
        return $"{_baseUrl}/hubs/text";
    }
}
