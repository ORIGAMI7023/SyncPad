using Microsoft.EntityFrameworkCore;
using SyncPad.Server.Data;
using SyncPad.Server.Data.Entities;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

/// <summary>
/// 设备同步服务接口
/// </summary>
public interface IDeviceSyncService
{
    /// <summary>
    /// 注册或更新设备
    /// </summary>
    Task<Device> RegisterOrUpdateDeviceAsync(int userId, string deviceName, string deviceType, string deviceId);

    /// <summary>
    /// 更新设备活跃状态
    /// </summary>
    Task UpdateDeviceActivityAsync(int userId, string deviceId);

    /// <summary>
    /// 设置设备在线状态
    /// </summary>
    Task SetDeviceOnlineStatusAsync(int userId, string deviceId, bool isOnline);

    /// <summary>
    /// 获取用户的所有设备
    /// </summary>
    Task<List<Device>> GetUserDevicesAsync(int userId);

    /// <summary>
    /// 更新设备的最后同步消息ID
    /// </summary>
    Task UpdateLastSyncMessageIdAsync(int userId, string deviceId, long messageId);

    /// <summary>
    /// 获取设备的离线消息（自上次同步以来的新消息）
    /// </summary>
    Task<List<ChatMessageDto>> GetOfflineMessagesAsync(int userId, string deviceId);

    /// <summary>
    /// 清理不活跃的设备
    /// </summary>
    Task<int> CleanupInactiveDevicesAsync(int inactiveDays = 30);
}

/// <summary>
/// 设备同步服务实现
/// </summary>
public class DeviceSyncService : IDeviceSyncService
{
    private readonly SyncPadDbContext _context;
    private readonly IChatService _chatService;

    public DeviceSyncService(SyncPadDbContext context, IChatService chatService)
    {
        _context = context;
        _chatService = chatService;
    }

    public async Task<Device> RegisterOrUpdateDeviceAsync(int userId, string deviceName, string deviceType, string deviceId)
    {
        // 验证用户存在
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("用户不存在");
        }

        // 查找现有设备
        var existingDevice = await _context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

        var now = DateTime.UtcNow;

        if (existingDevice != null)
        {
            // 更新现有设备信息
            existingDevice.DeviceName = deviceName;
            existingDevice.DeviceType = deviceType;
            existingDevice.LastActiveAt = now;
            existingDevice.IsOnline = true;

            await _context.SaveChangesAsync();
            return existingDevice;
        }
        else
        {
            // 创建新设备
            var newDevice = new Device
            {
                UserId = userId,
                DeviceName = deviceName,
                DeviceType = deviceType,
                DeviceId = deviceId,
                LastActiveAt = now,
                IsOnline = true,
                LastSyncMessageId = null
            };

            _context.Devices.Add(newDevice);
            await _context.SaveChangesAsync();

            return newDevice;
        }
    }

    public async Task UpdateDeviceActivityAsync(int userId, string deviceId)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

        if (device != null)
        {
            device.LastActiveAt = DateTime.UtcNow;
            device.IsOnline = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetDeviceOnlineStatusAsync(int userId, string deviceId, bool isOnline)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

        if (device != null)
        {
            device.IsOnline = isOnline;
            if (isOnline)
            {
                device.LastActiveAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Device>> GetUserDevicesAsync(int userId)
    {
        return await _context.Devices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastActiveAt)
            .ToListAsync();
    }

    public async Task UpdateLastSyncMessageIdAsync(int userId, string deviceId, long messageId)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

        if (device != null)
        {
            device.LastSyncMessageId = messageId;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ChatMessageDto>> GetOfflineMessagesAsync(int userId, string deviceId)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

        if (device == null)
        {
            return new List<ChatMessageDto>();
        }

        // 获取自上次同步以来的新消息
        var query = _context.ChatMessages
            .Include(m => m.User)
            .Include(m => m.FileItem)
            .Where(m => m.UserId == userId && !m.IsDeleted);

        if (device.LastSyncMessageId.HasValue)
        {
            query = query.Where(m => m.Id > device.LastSyncMessageId.Value);
        }

        var messages = await query
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // 转换为DTO
        var messageDtos = new List<ChatMessageDto>();
        foreach (var message in messages)
        {
            messageDtos.Add(await MapToDtoAsync(message));
        }

        // 更新最后同步消息ID
        if (messages.Any())
        {
            device.LastSyncMessageId = messages.Last().Id;
            await _context.SaveChangesAsync();
        }

        return messageDtos;
    }

    public async Task<int> CleanupInactiveDevicesAsync(int inactiveDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDays);

        var inactiveDevices = await _context.Devices
            .Where(d => d.LastActiveAt < cutoffDate && !d.IsOnline)
            .ToListAsync();

        if (!inactiveDevices.Any())
        {
            return 0;
        }

        _context.Devices.RemoveRange(inactiveDevices);
        await _context.SaveChangesAsync();

        return inactiveDevices.Count;
    }

    /// <summary>
    /// 将实体映射为DTO（简化版本）
    /// </summary>
    private async Task<ChatMessageDto> MapToDtoAsync(ChatMessage message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            UserId = message.UserId,
            Username = message.User?.Username ?? "Unknown",
            Type = message.Type,
            EncryptedContent = message.EncryptedContent,
            FileItemId = message.FileItemId,
            CreatedAt = message.CreatedAt,
            IsDeleted = message.IsDeleted,
            EditedAt = message.EditedAt,
            FileInfo = message.FileItem != null ? new FileItemDto
            {
                Id = message.FileItem.Id,
                FileName = message.FileItem.FileName,
                FileSize = message.FileItem.FileSize,
                MimeType = message.FileItem.MimeType,
                UploadedAt = message.FileItem.UploadedAt,
                Hash = message.FileItem.Hash
            } : null
        };
    }
}
