using System.Security.Cryptography;
using System.Text;
using SyncPad.Server.Data.Entities;

namespace SyncPad.Server.Data;

/// <summary>
/// 数据库初始化器
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// 初始化数据库并创建种子数据
    /// </summary>
    public static void Initialize(SyncPadDbContext context, string? adminUsername = null, string? adminPassword = null)
    {
        // 确保数据库已创建（不使用迁移）
        var databaseCreated = context.Database.EnsureCreated();

        // 检查是否需要从旧架构迁移
        if (!databaseCreated && NeedsMigration(context))
        {
            MigrateFromOldSchema(context);
        }

        // 如果已有用户数据，跳过种子（但确保有加密密钥）
        if (context.Users.Any())
        {
            EnsureEncryptionKeys(context);
            return;
        }

        // 必须提供管理员账户信息
        if (string.IsNullOrEmpty(adminUsername) || string.IsNullOrEmpty(adminPassword))
        {
            throw new InvalidOperationException("必须在配置文件中提供 DefaultAdmin:Username 和 DefaultAdmin:Password");
        }

        // 创建管理员账户
        var adminUser = new User
        {
            Username = adminUsername,
            PasswordHash = HashPassword(adminPassword),
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(adminUser);
        context.SaveChanges();

        // 为管理员创建空的文本内容（保留向后兼容）
        var textContent = new TextContent
        {
            UserId = adminUser.Id,
            Content = "",
            UpdatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        };

        context.TextContents.Add(textContent);

        // 为管理员创建加密密钥
        var encryptionKey = new EncryptionKey
        {
            UserId = adminUser.Id,
            Salt = GenerateSalt(),
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };

        context.EncryptionKeys.Add(encryptionKey);
        context.SaveChanges();
    }

    /// <summary>
    /// 检查是否需要从旧架构迁移
    /// </summary>
    private static bool NeedsMigration(SyncPadDbContext context)
    {
        // 检查是否存在旧的TextContent表但不存在ChatMessage表
        return context.TextContents.Any() && !context.ChatMessages.Any();
    }

    /// <summary>
    /// 从旧架构迁移数据
    /// </summary>
    private static void MigrateFromOldSchema(SyncPadDbContext context)
    {
        // 在实际部署时，这里会处理数据迁移
        // 目前保留旧数据，不自动迁移以避免数据丢失

        // TODO: 实现数据迁移逻辑
        // 1. 备份TextContent数据
        // 2. 转换为ChatMessage格式
        // 3. 删除旧的TextContent记录
    }

    /// <summary>
    /// 确保所有用户都有加密密钥
    /// </summary>
    private static void EnsureEncryptionKeys(SyncPadDbContext context)
    {
        var usersWithoutKeys = context.Users
            .Where(u => !context.EncryptionKeys.Any(ek => ek.UserId == u.Id))
            .ToList();

        foreach (var user in usersWithoutKeys)
        {
            var encryptionKey = new EncryptionKey
            {
                UserId = user.Id,
                Salt = GenerateSalt(),
                Version = 1,
                CreatedAt = DateTime.UtcNow
            };

            context.EncryptionKeys.Add(encryptionKey);
        }

        if (usersWithoutKeys.Any())
        {
            context.SaveChanges();
        }
    }

    /// <summary>
    /// 使用 SHA256 哈希密码
    /// </summary>
    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    public static bool VerifyPassword(string password, string passwordHash)
    {
        return HashPassword(password) == passwordHash;
    }

    /// <summary>
    /// 生成随机盐值（用于加密密钥派生）
    /// </summary>
    public static string GenerateSalt()
    {
        var salt = new byte[32]; // 256位盐值
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        return Convert.ToBase64String(salt);
    }
}
