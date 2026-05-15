using Microsoft.EntityFrameworkCore;
using SyncPad.Server.Data;
using SyncPad.Server.Data.Entities;
using System.Security.Cryptography;

namespace SyncPad.Server.Core.Services;

/// <summary>
/// 加密服务接口
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// 为用户生成盐值（首次注册时）
    /// </summary>
    Task<string> GenerateSaltForUserAsync(int userId);

    /// <summary>
    /// 获取用户的盐值
    /// </summary>
    Task<string?> GetUserSaltAsync(int userId);

    /// <summary>
    /// 验证用户是否有加密密钥
    /// </summary>
    Task<bool> HasEncryptionKeyAsync(int userId);

    /// <summary>
    /// 获取密钥版本
    /// </summary>
    Task<int> GetKeyVersionAsync(int userId);
}

/// <summary>
/// 加密服务实现
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly SyncPadDbContext _context;

    public EncryptionService(SyncPadDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateSaltForUserAsync(int userId)
    {
        // 验证用户存在
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("用户不存在");
        }

        // 检查是否已有盐值
        var existingKey = await _context.EncryptionKeys.FindAsync(userId);
        if (existingKey != null)
        {
            return existingKey.Salt;
        }

        // 生成新的盐值
        var salt = GenerateSalt();

        // 创建加密密钥记录
        var encryptionKey = new EncryptionKey
        {
            UserId = userId,
            Salt = salt,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.EncryptionKeys.Add(encryptionKey);
        await _context.SaveChangesAsync();

        return salt;
    }

    public async Task<string?> GetUserSaltAsync(int userId)
    {
        var encryptionKey = await _context.EncryptionKeys.FindAsync(userId);
        return encryptionKey?.Salt;
    }

    public async Task<bool> HasEncryptionKeyAsync(int userId)
    {
        return await _context.EncryptionKeys.AnyAsync(ek => ek.UserId == userId);
    }

    public async Task<int> GetKeyVersionAsync(int userId)
    {
        var encryptionKey = await _context.EncryptionKeys.FindAsync(userId);
        return encryptionKey?.Version ?? 0;
    }

    /// <summary>
    /// 生成随机盐值
    /// </summary>
    private static string GenerateSalt()
    {
        var salt = new byte[32]; // 256位盐值
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return Convert.ToBase64String(salt);
    }
}
