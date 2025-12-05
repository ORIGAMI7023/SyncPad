using SyncPad.Client.Core.Services;

namespace SyncPad.Client.Services;

/// <summary>
/// 文件系统 Token 存储实现（Mac Catalyst 开发模式备用方案）
/// </summary>
public class FileTokenStorage : ITokenStorage
{
    private readonly string _storageFile;

    public FileTokenStorage()
    {
        var appDataPath = FileSystem.AppDataDirectory;
        _storageFile = Path.Combine(appDataPath, "syncpad_token.json");
    }

    public async Task SaveTokenAsync(string token, string username, int userId)
    {
        var data = new
        {
            Token = token,
            Username = username,
            UserId = userId
        };

        var json = System.Text.Json.JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(_storageFile, json);
    }

    public async Task<(string? Token, string? Username, int? UserId)> GetTokenAsync()
    {
        try
        {
            if (!File.Exists(_storageFile))
                return (null, null, null);

            var json = await File.ReadAllTextAsync(_storageFile);
            var data = System.Text.Json.JsonSerializer.Deserialize<TokenData>(json);

            return (data?.Token, data?.Username, data?.UserId);
        }
        catch
        {
            return (null, null, null);
        }
    }

    public Task ClearTokenAsync()
    {
        if (File.Exists(_storageFile))
            File.Delete(_storageFile);
        return Task.CompletedTask;
    }

    private class TokenData
    {
        public string? Token { get; set; }
        public string? Username { get; set; }
        public int? UserId { get; set; }
    }
}
