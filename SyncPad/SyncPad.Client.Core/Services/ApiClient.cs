using System.Net.Http.Headers;
using System.Net.Http.Json;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// HTTP API 客户端实现
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void SetBaseUrl(string baseUrl)
    {
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public void SetToken(string? token)
    {
        _token = token;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return result ?? new LoginResponse { Success = false, ErrorMessage = "服务器返回空响应" };
        }
        catch (Exception ex)
        {
            return new LoginResponse { Success = false, ErrorMessage = $"网络错误: {ex.Message}" };
        }
    }

    public async Task<ApiResponse<TextSyncMessage>> GetTextAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ApiResponse<TextSyncMessage>>("api/text");
            return response ?? ApiResponse<TextSyncMessage>.Fail("服务器返回空响应");
        }
        catch (Exception ex)
        {
            return ApiResponse<TextSyncMessage>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<ApiResponse<TextSyncMessage>> UpdateTextAsync(TextSyncMessage message)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/text", message);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TextSyncMessage>>();
            return result ?? ApiResponse<TextSyncMessage>.Fail("服务器返回空响应");
        }
        catch (Exception ex)
        {
            return ApiResponse<TextSyncMessage>.Fail($"网络错误: {ex.Message}");
        }
    }
}
