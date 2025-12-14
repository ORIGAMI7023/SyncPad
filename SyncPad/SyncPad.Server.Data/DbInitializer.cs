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
        context.Database.EnsureCreated();

        // 如果已有用户数据，跳过种子
        if (context.Users.Any())
        {
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

        // 为管理员创建空的文本内容
        var textContent = new TextContent
        {
            UserId = adminUser.Id,
            Content = "",
            UpdatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        };

        context.TextContents.Add(textContent);
        context.SaveChanges();
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
}
