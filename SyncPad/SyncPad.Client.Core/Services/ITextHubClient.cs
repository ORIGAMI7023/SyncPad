using SyncPad.Shared.Models;

namespace SyncPad.Client.Core.Services;

/// <summary>
/// SignalR 文本同步客户端接口
/// </summary>
public interface ITextHubClient
{
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event Action<bool>? ConnectionStateChanged;

    /// <summary>
    /// 收到文本更新事件
    /// </summary>
    event Action<TextSyncMessage>? TextUpdateReceived;

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
    /// 发送文本更新
    /// </summary>
    Task SendTextUpdateAsync(string content);

    /// <summary>
    /// 请求获取最新文本
    /// </summary>
    Task RequestLatestTextAsync();
}
