using Microsoft.JSInterop;
using SyncPad.Client.Core.Services;

namespace SyncPad.Web.Services;

/// <summary>
/// Blazor 平台的 Token 存储实现（使用 localStorage）
/// </summary>
public class BrowserTokenStorage : ITokenStorage
{
    private readonly IJSRuntime _jsRuntime;

    private const string TokenKey = "syncpad_token";
    private const string UsernameKey = "syncpad_username";
    private const string UserIdKey = "syncpad_userid";

    public BrowserTokenStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SaveTokenAsync(string token, string username, int userId)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UsernameKey, username);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UserIdKey, userId.ToString());
    }

    public async Task<(string? Token, string? Username, int? UserId)> GetTokenAsync()
    {
        var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        var username = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", UsernameKey);
        var userIdStr = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", UserIdKey);

        int? userId = null;
        if (int.TryParse(userIdStr, out var id))
        {
            userId = id;
        }

        return (token, username, userId);
    }

    public async Task ClearTokenAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UsernameKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserIdKey);
    }
}
