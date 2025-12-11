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
                System.Diagnostics.Debug.WriteLine($"[FileClient] EnsureBaseAddress - BaseAddress 设置为: {baseUrl}");
            }
        }

        // 确保设置了认证令牌
        var token = _authManager.Token;
        System.Diagnostics.Debug.WriteLine($"[FileClient] EnsureBaseAddress - Token: {(string.IsNullOrEmpty(token) ? "null" : token[..Math.Min(20, token.Length)])}...");

        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            System.Diagnostics.Debug.WriteLine($"[FileClient] EnsureBaseAddress - Authorization 头已设置");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[FileClient] EnsureBaseAddress - 警告: Token 为空！");
        }
    }

    public async Task<ApiResponse<FileListResponse>> GetFilesAsync()
    {
        try
        {
            EnsureBaseAddress();
            System.Diagnostics.Debug.WriteLine($"[FileClient] GetFilesAsync - BaseAddress: {_httpClient.BaseAddress}");
            System.Diagnostics.Debug.WriteLine($"[FileClient] GetFilesAsync - Token: {(_httpClient.DefaultRequestHeaders.Authorization?.Parameter?[..Math.Min(20, _httpClient.DefaultRequestHeaders.Authorization?.Parameter?.Length ?? 0)] ?? "null")}...");

            var httpResponse = await _httpClient.GetAsync("api/files");
            System.Diagnostics.Debug.WriteLine($"[FileClient] GetFilesAsync - StatusCode: {httpResponse.StatusCode}");

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[FileClient] GetFilesAsync - Error: {errorContent}");
                return ApiResponse<FileListResponse>.Fail($"服务器返回错误: {httpResponse.StatusCode}");
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<ApiResponse<FileListResponse>>();
            System.Diagnostics.Debug.WriteLine($"[FileClient] GetFilesAsync - Success: {response?.Success}, FileCount: {response?.Data?.Files?.Count ?? 0}");
            return response ?? ApiResponse<FileListResponse>.Fail("服务器返回空响应");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileClient] GetFilesAsync - Exception: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[FileClient] UploadFileAsync - URL: {_httpClient.BaseAddress}{url}");

            var response = await _httpClient.PostAsync(url, content);
            System.Diagnostics.Debug.WriteLine($"[FileClient] UploadFileAsync - StatusCode: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[FileClient] UploadFileAsync - Error Response: {errorContent}");
                return new FileUploadResponse { Success = false, ErrorMessage = $"上传失败 ({response.StatusCode}): {errorContent}" };
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[FileClient] UploadFileAsync - Response Content: {responseContent}");

            var result = await response.Content.ReadFromJsonAsync<FileUploadResponse>();
            return result ?? new FileUploadResponse { Success = false, ErrorMessage = "服务器返回空响应" };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileClient] UploadFileAsync - Exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[FileClient] UploadFileAsync - StackTrace: {ex.StackTrace}");
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

    public async Task<bool> DownloadFileToCacheAsync(int fileId, string fileName, string cachePath, Action<long, long>? progressCallback = null)
    {
        try
        {
            EnsureBaseAddress();
            var url = $"api/files/{fileId}";

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return false;

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var downloadedBytes = 0L;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                downloadedBytes += bytesRead;

                // 触发进度回调
                progressCallback?.Invoke(downloadedBytes, totalBytes);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"下载文件到缓存失败: {ex.Message}");
            return false;
        }
    }
}
