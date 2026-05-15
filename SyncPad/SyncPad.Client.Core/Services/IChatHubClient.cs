using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// SignalR 聊天客户端接口
/// </summary>
public interface IChatHubClient
{
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event Action<bool>? ConnectionStateChanged;

    /// <summary>
    /// 收到消息事件
    /// </summary>
    event Action<ChatMessageDto>? OnReceiveMessage;

    /// <summary>
    /// 收到消息列表事件
    /// </summary>
    event Action<MessageListResponse>? OnReceiveMessages;

    /// <summary>
    /// 收到离线消息事件
    /// </summary>
    event Action<List<ChatMessageDto>>? OnReceiveOfflineMessages;

    /// <summary>
    /// 消息被删除事件
    /// </summary>
    event Action<long>? OnMessageDeleted;

    /// <summary>
    /// 消息已读事件
    /// </summary>
    event Action<long>? OnMessageRead;

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 连接到 Hub
    /// </summary>
    Task ConnectAsync(string hubUrl, string token);

    /// <summary>
    /// 断开连接
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 发送消息
    /// </summary>
    Task SendMessageAsync(SendMessageRequest request);

    /// <summary>
    /// 请求历史消息
    /// </summary>
    Task RequestMessagesAsync(GetMessagesRequest request);

    /// <summary>
    /// 请求离线消息
    /// </summary>
    Task RequestOfflineMessagesAsync();

    /// <summary>
    /// 删除消息
    /// </summary>
    Task DeleteMessageAsync(long messageId);

    /// <summary>
    /// 标记消息为已读
    /// </summary>
    Task MarkAsReadAsync(long messageId);

    /// <summary>
    /// 请求文件列表
    /// </summary>
    Task RequestFileListAsync();
}
