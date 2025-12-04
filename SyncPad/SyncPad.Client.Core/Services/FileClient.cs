using System.Net.Http.Headers;
using System.Net.Http.Json;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

public class FileClient : IFileClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthManager _authManager;
    private readonly IApiClient _apiClient;

    public FileClient(HttpClient httpClient, IAuthManager authManager, IApiClient apiClient)
    {
        _httpClient = httpClient;
        _authManager = authManager;
        _apiClient = apiClient;
    }

    private void EnsureBaseAddress()
    {
        if (_httpClient.BaseAddress == null)
        {
            // 从 HubUrl 推断 API BaseUrl
            // HubUrl 格式: http://localhost:5000/hubs/text
            // BaseUrl 格式: http://localhost:5000/
            var hubUrl = _authManager.GetHubUrl();
            if (!string.IsNullOrEmpty(hubUrl))
            {
                var uri = new Uri(hubUrl);
                var baseUrl = $"{uri.Scheme}://{uri.Authority}/";
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        // 确保设置了认证令牌
        var token = _authManager.Token;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<ApiResponse<FileListResponse>> GetFilesAsync()
    {
        try
        {
            EnsureBaseAddress();
            var response = await _httpClient.GetFromJsonAsync<ApiResponse<FileListResponse>>("api/files");
            return response ?? ApiResponse<FileListResponse>.Fail("服务器返回空响应");
        }
        catch (Exception ex)
        {
            return ApiResponse<FileListResponse>.Fail($"网络错误: {ex.Message}");
        }
    }

    public async Task<bool> FileExistsAsync(string fileName)
    {
        try
        {
            EnsureBaseAddress();
            var response = await _httpClient.GetFromJsonAsync<ApiResponse<bool>>(
                $"api/files/exists?fileName={Uri.EscapeDataString(fileName)}");
            return response?.Data ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<FileUploadResponse> UploadFileAsync(string fileName, Stream stream, string? mimeType, bool overwrite = false)
    {
        try
        {
            EnsureBaseAddress();
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(stream);

            if (!string.IsNullOrEmpty(mimeType))
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

            content.Add(streamContent, "file", fileName);

            var url = overwrite ? "api/files?overwrite=true" : "api/files";
            var response = await _httpClient.PostAsync(url, content);
            var result = await response.Content.ReadFromJsonAsync<FileUploadResponse>();

            return result ?? new FileUploadResponse { Success = false, ErrorMessage = "服务器返回空响应" };
        }
        catch (Exception ex)
        {
            return new FileUploadResponse { Success = false, ErrorMessage = $"上传失败: {ex.Message}" };
        }
    }

    public string GetDownloadUrl(int fileId)
    {
        EnsureBaseAddress();
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";
        var token = _authManager.Token ?? "";
        return $"{baseUrl}/api/files/{fileId}?token={Uri.EscapeDataString(token)}";
    }

    public async Task<ApiResponse> DeleteFileAsync(int fileId)
    {
        try
        {
            EnsureBaseAddress();
            var response = await _httpClient.DeleteAsync($"api/files/{fileId}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
            return result ?? ApiResponse.Fail("服务器返回空响应");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"删除失败: {ex.Message}");
        }
    }
}
