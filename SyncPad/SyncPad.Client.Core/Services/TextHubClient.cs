using Microsoft.AspNetCore.SignalR.Client;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// SignalR 文本同步客户端实现
/// </summary>
public class TextHubClient : ITextHubClient, IAsyncDisposable
{
    private HubConnection? _hubConnection;

    public event Action<bool>? ConnectionStateChanged;
    public event Action<TextSyncMessage>? TextUpdateReceived;
    public event Action<FileSyncMessage>? FileUpdateReceived;
    public event Action<List<FileItemDto>>? FileListReceived;
    public event Action<int, int, int>? FilePositionChanged;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string hubUrl, string token)
    {
        if (_hubConnection != null)
        {
            await DisconnectAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect(new[] {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        // 监听连接状态变化
        _hubConnection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnecting += _ =>
        {
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += _ =>
        {
            ConnectionStateChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        // 监听文本更新
        _hubConnection.On<TextSyncMessage>("ReceiveTextUpdate", message =>
        {
            TextUpdateReceived?.Invoke(message);
        });

        // 监听文件更新
        _hubConnection.On<FileSyncMessage>("ReceiveFileUpdate", message =>
        {
            FileUpdateReceived?.Invoke(message);
        });

        // 监听文件列表
        _hubConnection.On<List<FileItemDto>>("ReceiveFileList", files =>
        {
            FileListReceived?.Invoke(files);
        });

        // 监听文件位置变更
        _hubConnection.On<int, int, int>("ReceiveFilePositionChanged", (fileId, positionX, positionY) =>
        {
            FilePositionChanged?.Invoke(fileId, positionX, positionY);
        });

        try
        {
            await _hubConnection.StartAsync();
            ConnectionStateChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TextHubClient] 连接失败: {ex.Message}");
            ConnectionStateChanged?.Invoke(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            ConnectionStateChanged?.Invoke(false);
        }
    }

    public async Task SendTextUpdateAsync(string content)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("SendTextUpdate", content);
            }
            catch (HubException ex)
            {
                Console.WriteLine($"[TextHubClient] SendTextUpdate 失败: {ex.Message}");
                // 认证失败，断开连接
                await DisconnectAsync();
                throw;
            }
        }
    }

    public async Task RequestLatestTextAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("RequestLatestText");
        }
    }

    public async Task RequestFileListAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("RequestFileList");
        }
    }

    public async Task UpdateFilePositionAsync(int fileId, int positionX, int positionY)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("UpdateFilePosition", fileId, positionX, positionY);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
