using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// SignalR 聊天客户端实现
/// </summary>
public class ChatHubClient : IChatHubClient, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _deviceName;
    private readonly string _deviceType;
    private readonly string _deviceId;

    public event Action<bool>? ConnectionStateChanged;
    public event Action<ChatMessageDto>? OnReceiveMessage;
    public event Action<MessageListResponse>? OnReceiveMessages;
    public event Action<List<ChatMessageDto>>? OnReceiveOfflineMessages;
    public event Action<long>? OnMessageDeleted;
    public event Action<long>? OnMessageRead;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public ChatHubClient()
    {
        // 生成设备信息
        _deviceName = GetDeviceName();
        _deviceType = "Web";
        _deviceId = GenerateDeviceId();
    }

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
                // 传递设备信息
                options.Headers.Add("deviceName", _deviceName);
                options.Headers.Add("deviceType", _deviceType);
                options.Headers.Add("deviceId", _deviceId);
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
            Console.WriteLine("[ChatHubClient] 连接已关闭");
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnecting += _ =>
        {
            Console.WriteLine("[ChatHubClient] 正在重连...");
            ConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += _ =>
        {
            Console.WriteLine("[ChatHubClient] 重连成功");
            ConnectionStateChanged?.Invoke(true);
            return Task.CompletedTask;
        };

        // 监听接收消息
        _hubConnection.On<ChatMessageDto>("ReceiveMessage", message =>
        {
            Console.WriteLine($"[ChatHubClient] 收到消息: {message.Id}");
            OnReceiveMessage?.Invoke(message);
        });

        // 监听接收消息列表
        _hubConnection.On<MessageListResponse>("ReceiveMessages", response =>
        {
            Console.WriteLine($"[ChatHubClient] 收到消息列表: {response.Messages.Count} 条");
            OnReceiveMessages?.Invoke(response);
        });

        // 监听接收离线消息
        _hubConnection.On<List<ChatMessageDto>>("ReceiveOfflineMessages", messages =>
        {
            Console.WriteLine($"[ChatHubClient] 收到离线消息: {messages.Count} 条");
            OnReceiveOfflineMessages?.Invoke(messages);
        });

        // 监听消息删除
        _hubConnection.On<long>("MessageDeleted", messageId =>
        {
            Console.WriteLine($"[ChatHubClient] 消息已删除: {messageId}");
            OnMessageDeleted?.Invoke(messageId);
        });

        // 监听消息已读
        _hubConnection.On<long>("MessageRead", messageId =>
        {
            Console.WriteLine($"[ChatHubClient] 消息已读: {messageId}");
            OnMessageRead?.Invoke(messageId);
        });

        // 监听文件列表
        _hubConnection.On<List<FileItemDto>>("ReceiveFileList", files =>
        {
            Console.WriteLine($"[ChatHubClient] 收到文件列表: {files.Count} 个文件");
            // 可以添加一个文件列表接收事件
        });

        try
        {
            await _hubConnection.StartAsync();
            Console.WriteLine("[ChatHubClient] 连接成功");
            ConnectionStateChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatHubClient] 连接失败: {ex.Message}");
            ConnectionStateChanged?.Invoke(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHubClient] 断开连接时出错: {ex.Message}");
            }
            finally
            {
                _hubConnection = null;
                ConnectionStateChanged?.Invoke(false);
            }
        }
    }

    public async Task SendMessageAsync(SendMessageRequest request)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                Console.WriteLine($"[ChatHubClient] 发送消息: {request.Type}");
                await _hubConnection.InvokeAsync("SendMessage", request);
            }
            catch (HubException ex)
            {
                Console.WriteLine($"[ChatHubClient] 发送消息失败: {ex.Message}");
                // 认证失败，断开连接
                await DisconnectAsync();
                throw;
            }
        }
        else
        {
            Console.WriteLine("[ChatHubClient] 未连接，无法发送消息");
            throw new InvalidOperationException("未连接到服务器");
        }
    }

    public async Task RequestMessagesAsync(GetMessagesRequest request)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                Console.WriteLine($"[ChatHubClient] 请求历史消息: BeforeId={request.BeforeId}, Count={request.Count}");
                await _hubConnection.InvokeAsync("RequestMessages", request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHubClient] 请求历史消息失败: {ex.Message}");
                throw;
            }
        }
    }

    public async Task RequestOfflineMessagesAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                Console.WriteLine("[ChatHubClient] 请求离线消息");
                await _hubConnection.InvokeAsync("RequestOfflineMessages");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHubClient] 请求离线消息失败: {ex.Message}");
                throw;
            }
        }
    }

    public async Task DeleteMessageAsync(long messageId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                Console.WriteLine($"[ChatHubClient] 删除消息: {messageId}");
                await _hubConnection.InvokeAsync("DeleteMessage", messageId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHubClient] 删除消息失败: {ex.Message}");
                throw;
            }
        }
    }

    public async Task MarkAsReadAsync(long messageId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                Console.WriteLine($"[ChatHubClient] 标记消息已读: {messageId}");
                await _hubConnection.InvokeAsync("MarkAsRead", messageId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHubClient] 标记消息已读失败: {ex.Message}");
                throw;
            }
        }
    }

    public async Task RequestFileListAsync()
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            try
            {
                Console.WriteLine("[ChatHubClient] 请求文件列表");
                await _hubConnection.InvokeAsync("RequestFileList");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHubClient] 请求文件列表失败: {ex.Message}");
                throw;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    /// <summary>
    /// 获取设备名称
    /// </summary>
    private static string GetDeviceName()
    {
        // 对于Web客户端，返回通用名称
        return "Web Browser";
    }

    /// <summary>
    /// 生成设备ID
    /// </summary>
    private static string GenerateDeviceId()
    {
        // 生成基于GUID的设备ID
        return Guid.NewGuid().ToString();
    }
}
