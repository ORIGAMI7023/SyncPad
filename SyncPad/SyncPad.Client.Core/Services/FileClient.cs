using System.Net.Http.Headers;
using System.Net.Http.Json;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

public class FileClient : IFileClient
{
    private readonly HttpClient _httpClient;
    private readonly IAuthManager _authManager;

    public FileClient(HttpClient httpClient, IAuthManager authManager)
    {
        _httpClient = httpClient;
        _authManager = authManager;
    }

    public async Task<ApiResponse<FileListResponse>> GetFilesAsync()
    {
        try
        {
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
        var baseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? "";
        return $"{baseUrl}/api/files/{fileId}";
    }

    public async Task<ApiResponse> DeleteFileAsync(int fileId)
    {
        try
        {
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
