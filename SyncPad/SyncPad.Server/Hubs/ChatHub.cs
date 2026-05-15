using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SyncPad.Server.Core.Services;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Hubs;

/// <summary>
/// 聊天实时同步 Hub
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IEncryptionService _encryptionService;
    private readonly IDeviceSyncService _deviceSyncService;
    private readonly IFileService _fileService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatService chatService,
        IEncryptionService encryptionService,
        IDeviceSyncService deviceSyncService,
        IFileService fileService,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _encryptionService = encryptionService;
        _deviceSyncService = deviceSyncService;
        _fileService = fileService;
        _logger = logger;
    }

    /// <summary>
    /// 客户端连接时处理设备注册
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != null)
        {
            // 将用户加入以 UserId 命名的组，便于多设备同步
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // 获取设备信息（从查询字符串中）
            var deviceName = Context.GetHttpContext()?.Request.Query["deviceName"].FirstOrDefault() ?? "Unknown Device";
            var deviceType = Context.GetHttpContext()?.Request.Query["deviceType"].FirstOrDefault() ?? "Web";
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].FirstOrDefault() ?? Context.ConnectionId;

            // 注册或更新设备
            try
            {
                await _deviceSyncService.RegisterOrUpdateDeviceAsync(userId.Value, deviceName, deviceType, deviceId);
                _logger.LogInformation("用户 {UserId} 的设备 {DeviceId} ({DeviceName}) 已连接", userId, deviceId, deviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设备注册失败: {ErrorMessage}", ex.Message);
            }
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开时处理设备状态
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");

            // 更新设备离线状态
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].FirstOrDefault() ?? Context.ConnectionId;
            await _deviceSyncService.SetDeviceOnlineStatusAsync(userId.Value, deviceId, false);

            _logger.LogInformation("用户 {UserId} 的设备 {DeviceId} 已断开连接", userId, deviceId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    public async Task SendMessage(SendMessageRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        try
        {
            // 保存消息到数据库
            var messageDto = await _chatService.SendMessageAsync(userId.Value, request);

            // 广播给同账号的所有设备（包括发送者）
            await Clients.Group($"user_{userId}").SendAsync("ReceiveMessage", messageDto);

            // 更新设备的最后同步消息ID
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].FirstOrDefault() ?? Context.ConnectionId;
            await _deviceSyncService.UpdateLastSyncMessageIdAsync(userId.Value, deviceId, messageDto.Id);

            _logger.LogInformation("用户 {UserId} 发送了消息 {MessageId}", userId, messageDto.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息失败: {ErrorMessage}", ex.Message);
            throw new HubException($"发送消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 请求历史消息
    /// </summary>
    public async Task RequestMessages(GetMessagesRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        try
        {
            var response = await _chatService.GetMessagesAsync(userId.Value, request);
            await Clients.Caller.SendAsync("ReceiveMessages", response);

            _logger.LogInformation("用户 {UserId} 请求了历史消息，获取到 {Count} 条", userId, response.Messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取历史消息失败: {ErrorMessage}", ex.Message);
            throw new HubException($"获取历史消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 请求离线消息
    /// </summary>
    public async Task RequestOfflineMessages()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        try
        {
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].FirstOrDefault() ?? Context.ConnectionId;
            var offlineMessages = await _deviceSyncService.GetOfflineMessagesAsync(userId.Value, deviceId);

            await Clients.Caller.SendAsync("ReceiveOfflineMessages", offlineMessages);

            _logger.LogInformation("用户 {UserId} 的设备 {DeviceId} 获取到 {Count} 条离线消息", userId, deviceId, offlineMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取离线消息失败: {ErrorMessage}", ex.Message);
            throw new HubException($"获取离线消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 删除消息
    /// </summary>
    public async Task DeleteMessage(long messageId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        try
        {
            var success = await _chatService.DeleteMessageAsync(userId.Value, messageId);
            if (success)
            {
                // 通知所有设备消息已删除
                await Clients.Group($"user_{userId}").SendAsync("MessageDeleted", messageId);

                _logger.LogInformation("用户 {UserId} 删除了消息 {MessageId}", userId, messageId);
            }
            else
            {
                throw new HubException("消息不存在或无权删除");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除消息失败: {ErrorMessage}", ex.Message);
            throw new HubException($"删除消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 请求文件列表
    /// </summary>
    public async Task RequestFileList()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        try
        {
            var files = await _fileService.GetFilesAsync(userId.Value);
            await Clients.Caller.SendAsync("ReceiveFileList", files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件列表失败: {ErrorMessage}", ex.Message);
            throw new HubException($"获取文件列表失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 标记消息为已读
    /// </summary>
    public async Task MarkAsRead(long messageId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        try
        {
            var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].FirstOrDefault() ?? Context.ConnectionId;
            await _deviceSyncService.UpdateLastSyncMessageIdAsync(userId.Value, deviceId, messageId);

            // 通知其他设备消息已读
            await Clients.OthersInGroup($"user_{userId}").SendAsync("MessageRead", messageId);

            _logger.LogInformation("用户 {UserId} 的设备 {DeviceId} 标记消息 {MessageId} 为已读", userId, deviceId, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记消息已读失败: {ErrorMessage}", ex.Message);
            throw new HubException($"标记消息已读失败: {ex.Message}");
        }
    }

    private int? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}
