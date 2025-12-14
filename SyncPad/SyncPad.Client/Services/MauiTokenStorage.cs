using SyncPad.Client.Core.Services;

namespace SyncPad.Client.Services;

/// <summary>
/// MAUI 平台的 Token 存储实现
/// Mac Catalyst: 使用 Preferences（SecureStorage 不可靠）
/// 其他平台: 使用 SecureStorage
/// </summary>
public class MauiTokenStorage : ITokenStorage
{
    private const string TokenKey = "syncpad_token";
    private const string UsernameKey = "syncpad_username";
    private const string UserIdKey = "syncpad_userid";

    public async Task SaveTokenAsync(string token, string username, int userId)
    {
#if MACCATALYST
        // Mac Catalyst 使用 Preferences（SecureStorage 在 Mac 上不可靠）
        Preferences.Default.Set(TokenKey, token);
        Preferences.Default.Set(UsernameKey, username);
        Preferences.Default.Set(UserIdKey, userId.ToString());
        await Task.CompletedTask;
#else
        await SecureStorage.Default.SetAsync(TokenKey, token);
        await SecureStorage.Default.SetAsync(UsernameKey, username);
        await SecureStorage.Default.SetAsync(UserIdKey, userId.ToString());
#endif
        System.Diagnostics.Debug.WriteLine($"[TokenStorage] Saved token for user: {username}");
    }

    public async Task<(string? Token, string? Username, int? UserId)> GetTokenAsync()
    {
        string? token, username, userIdStr;

#if MACCATALYST
        token = Preferences.Default.Get<string?>(TokenKey, null);
        username = Preferences.Default.Get<string?>(UsernameKey, null);
        userIdStr = Preferences.Default.Get<string?>(UserIdKey, null);
        await Task.CompletedTask;
#else
        token = await SecureStorage.Default.GetAsync(TokenKey);
        username = await SecureStorage.Default.GetAsync(UsernameKey);
        userIdStr = await SecureStorage.Default.GetAsync(UserIdKey);
#endif

        int? userId = null;
        if (int.TryParse(userIdStr, out var id))
        {
            userId = id;
        }

        System.Diagnostics.Debug.WriteLine($"[TokenStorage] Retrieved - Token: {(token != null ? "exists" : "null")}, Username: {username}");
        return (token, username, userId);
    }

    public Task ClearTokenAsync()
    {
#if MACCATALYST
        Preferences.Default.Remove(TokenKey);
        Preferences.Default.Remove(UsernameKey);
        Preferences.Default.Remove(UserIdKey);
#else
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(UsernameKey);
        SecureStorage.Default.Remove(UserIdKey);
#endif
        System.Diagnostics.Debug.WriteLine("[TokenStorage] Cleared all tokens");
        return Task.CompletedTask;
    }
}
