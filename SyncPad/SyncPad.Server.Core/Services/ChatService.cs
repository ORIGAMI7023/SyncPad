using Microsoft.EntityFrameworkCore;
using SyncPad.Server.Data;
using SyncPad.Server.Data.Entities;
using SyncPad.Server.Core.Utils;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

/// <summary>
/// 聊天服务接口
/// </summary>
public interface IChatService
{
    /// <summary>
    /// 发送消息
    /// </summary>
    Task<ChatMessageDto> SendMessageAsync(int userId, SendMessageRequest request);

    /// <summary>
    /// 获取消息列表
    /// </summary>
    Task<MessageListResponse> GetMessagesAsync(int userId, GetMessagesRequest request);

    /// <summary>
    /// 删除消息（软删除）
    /// </summary>
    Task<bool> DeleteMessageAsync(int userId, long messageId);

    /// <summary>
    /// 归档旧消息
    /// </summary>
    Task<int> ArchiveOldMessagesAsync(int retentionDays = 30);
}

/// <summary>
/// 聊天服务实现
/// </summary>
public class ChatService : IChatService
{
    private readonly SyncPadDbContext _context;
    private readonly SnowflakeIdGenerator _idGenerator;

    public ChatService(SyncPadDbContext context, SnowflakeIdGenerator idGenerator)
    {
        _context = context;
        _idGenerator = idGenerator;
    }

    public async Task<ChatMessageDto> SendMessageAsync(int userId, SendMessageRequest request)
    {
        // 验证用户存在
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("用户不存在");
        }

        // 如果是文件消息，验证文件存在
        if (request.Type == MessageType.File && request.FileItemId.HasValue)
        {
            var file = await _context.FileItems.FindAsync(request.FileItemId.Value);
            if (file == null || file.UserId != userId)
            {
                throw new InvalidOperationException("文件不存在或无权访问");
            }
        }

        // 创建新消息
        var message = new ChatMessage
        {
            Id = _idGenerator.NextId(),
            UserId = userId,
            Type = request.Type,
            EncryptedContent = request.EncryptedContent,
            FileItemId = request.FileItemId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // 返回完整的消息DTO
        return await MapToDtoAsync(message);
    }

    public async Task<MessageListResponse> GetMessagesAsync(int userId, GetMessagesRequest request)
    {
        var query = _context.ChatMessages
            .Include(m => m.User)
            .Include(m => m.FileItem)
            .Where(m => m.UserId == userId && !m.IsDeleted);

        // 如果指定了BeforeId，获取该ID之前的消息
        if (request.BeforeId.HasValue)
        {
            query = query.Where(m => m.Id < request.BeforeId.Value);
        }

        // 按时间倒序排列，获取指定数量的消息
        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(request.Count)
            .ToListAsync();

        // 转换为DTO并按时间正序排列
        var messageDtos = new List<ChatMessageDto>();
        foreach (var message in messages.OrderBy(m => m.CreatedAt))
        {
            messageDtos.Add(await MapToDtoAsync(message));
        }

        // 检查是否还有更多消息
        bool hasMore = false;
        long? oldestMessageId = null;
        if (messageDtos.Any())
        {
            oldestMessageId = messageDtos.First().Id;
            hasMore = await _context.ChatMessages
                .AnyAsync(m => m.UserId == userId && !m.IsDeleted && m.Id < oldestMessageId.Value);
        }

        return new MessageListResponse
        {
            Messages = messageDtos,
            HasMore = hasMore,
            OldestMessageId = oldestMessageId
        };
    }

    public async Task<bool> DeleteMessageAsync(int userId, long messageId)
    {
        var message = await _context.ChatMessages.FindAsync(messageId);
        if (message == null || message.UserId != userId)
        {
            return false;
        }

        // 软删除
        message.IsDeleted = true;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<int> ArchiveOldMessagesAsync(int retentionDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        // 查找需要归档的消息
        var oldMessages = await _context.ChatMessages
            .Where(m => m.CreatedAt < cutoffDate && !m.IsDeleted)
            .ToListAsync();

        if (!oldMessages.Any())
        {
            return 0;
        }

        // 软删除旧消息
        foreach (var message in oldMessages)
        {
            message.IsDeleted = true;
        }

        await _context.SaveChangesAsync();
        return oldMessages.Count;
    }

    /// <summary>
    /// 将实体映射为DTO
    /// </summary>
    private async Task<ChatMessageDto> MapToDtoAsync(ChatMessage message)
    {
        var dto = new ChatMessageDto
        {
            Id = message.Id,
            UserId = message.UserId,
            Username = message.User?.Username ?? "Unknown",
            Type = message.Type,
            EncryptedContent = message.EncryptedContent,
            FileItemId = message.FileItemId,
            CreatedAt = message.CreatedAt,
            IsDeleted = message.IsDeleted,
            EditedAt = message.EditedAt
        };

        // 如果有文件，包含文件信息
        if (message.FileItem != null)
        {
            dto.FileInfo = new FileItemDto
            {
                Id = message.FileItem.Id,
                FileName = message.FileItem.FileName,
                FileSize = message.FileItem.FileSize,
                MimeType = message.FileItem.MimeType,
                UploadedAt = message.FileItem.UploadedAt,
                Hash = message.FileItem.Hash
            };
        }

        return dto;
    }
}
