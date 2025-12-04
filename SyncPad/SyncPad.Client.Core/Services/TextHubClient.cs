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
            .WithAutomaticReconnect()
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

        await _hubConnection.StartAsync();
        ConnectionStateChanged?.Invoke(true);
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
            await _hubConnection.InvokeAsync("SendTextUpdate", content);
        }
    }

    public async Task RequestLatestTextAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("RequestLatestText");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
