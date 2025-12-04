using SyncPad.Client.Core.Services;

namespace SyncPad.Client.Services;

/// <summary>
/// MAUI 平台的 Token 存储实现（使用 SecureStorage）
/// </summary>
public class MauiTokenStorage : ITokenStorage
{
    private const string TokenKey = "syncpad_token";
    private const string UsernameKey = "syncpad_username";
    private const string UserIdKey = "syncpad_userid";

    public async Task SaveTokenAsync(string token, string username, int userId)
    {
        await SecureStorage.Default.SetAsync(TokenKey, token);
        await SecureStorage.Default.SetAsync(UsernameKey, username);
        await SecureStorage.Default.SetAsync(UserIdKey, userId.ToString());
    }

    public async Task<(string? Token, string? Username, int? UserId)> GetTokenAsync()
    {
        var token = await SecureStorage.Default.GetAsync(TokenKey);
        var username = await SecureStorage.Default.GetAsync(UsernameKey);
        var userIdStr = await SecureStorage.Default.GetAsync(UserIdKey);

        int? userId = null;
        if (int.TryParse(userIdStr, out var id))
        {
            userId = id;
        }

        return (token, username, userId);
    }

    public Task ClearTokenAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(UsernameKey);
        SecureStorage.Default.Remove(UserIdKey);
        return Task.CompletedTask;
    }
}
