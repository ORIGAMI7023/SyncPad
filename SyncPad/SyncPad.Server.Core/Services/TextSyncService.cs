using Microsoft.EntityFrameworkCore;
using SyncPad.Server.Data;
using SyncPad.Server.Data.Entities;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

/// <summary>
/// 文本同步服务实现
/// </summary>
public class TextSyncService : ITextSyncService
{
    private readonly SyncPadDbContext _context;

    public TextSyncService(SyncPadDbContext context)
    {
        _context = context;
    }

    public async Task<TextSyncMessage?> GetTextAsync(int userId)
    {
        var textContent = await _context.TextContents
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (textContent == null)
        {
            return null;
        }

        // 更新最后访问时间
        textContent.LastAccessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new TextSyncMessage
        {
            Content = textContent.Content,
            UpdatedAt = textContent.UpdatedAt,
            SenderId = userId
        };
    }

    public async Task<TextSyncMessage> UpdateTextAsync(int userId, string content)
    {
        var textContent = await _context.TextContents
            .FirstOrDefaultAsync(t => t.UserId == userId);

        var now = DateTime.UtcNow;

        if (textContent == null)
        {
            // 如果不存在，创建新的
            textContent = new TextContent
            {
                UserId = userId,
                Content = content,
                UpdatedAt = now,
                LastAccessedAt = now
            };
            _context.TextContents.Add(textContent);
        }
        else
        {
            // 更新现有的
            textContent.Content = content;
            textContent.UpdatedAt = now;
            textContent.LastAccessedAt = now;
        }

        await _context.SaveChangesAsync();

        return new TextSyncMessage
        {
            Content = content,
            UpdatedAt = now,
            SenderId = userId
        };
    }
}
