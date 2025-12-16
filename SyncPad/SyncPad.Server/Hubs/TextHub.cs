using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SyncPad.Server.Core.Services;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Hubs;

/// <summary>
/// 文本实时同步 Hub
/// </summary>
[Authorize]
public class TextHub : Hub
{
    private readonly ITextSyncService _textSyncService;
    private readonly IFileService _fileService;

    public TextHub(ITextSyncService textSyncService, IFileService fileService)
    {
        _textSyncService = textSyncService;
        _fileService = fileService;
    }

    /// <summary>
    /// 客户端连接时加入用户组
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != null)
        {
            // 将用户加入以 UserId 命名的组，便于同账号多设备同步
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 客户端断开时离开用户组
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 接收客户端发送的文本更新
    /// </summary>
    public async Task SendTextUpdate(string content)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        // 保存到数据库
        var message = await _textSyncService.UpdateTextAsync(userId.Value, content);

        // 广播给同账号的其他客户端（排除发送者）
        await Clients.OthersInGroup($"user_{userId}").SendAsync("ReceiveTextUpdate", message);
    }

    /// <summary>
    /// 客户端请求获取最新文本
    /// </summary>
    public async Task RequestLatestText()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        var text = await _textSyncService.GetTextAsync(userId.Value);
        if (text != null)
        {
            await Clients.Caller.SendAsync("ReceiveTextUpdate", text);
        }
    }

    /// <summary>
    /// 客户端请求获取文件列表
    /// </summary>
    public async Task RequestFileList()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        var files = await _fileService.GetFilesAsync(userId.Value);
        await Clients.Caller.SendAsync("ReceiveFileList", files);
    }

    /// <summary>
    /// 更新文件位置
    /// </summary>
    public async Task UpdateFilePosition(int fileId, int positionX, int positionY)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new HubException("用户未认证或已被删除，请重新登录");
        }

        // 更新数据库
        var success = await _fileService.UpdateFilePositionAsync(userId.Value, fileId, positionX, positionY);
        if (success)
        {
            // 广播给同账号的其他客户端（排除发送者）
            await Clients.OthersInGroup($"user_{userId}").SendAsync("ReceiveFilePositionChanged", fileId, positionX, positionY);
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
