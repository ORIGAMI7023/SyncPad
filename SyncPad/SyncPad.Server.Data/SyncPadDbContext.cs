using Microsoft.EntityFrameworkCore;
using SyncPad.Server.Data.Entities;

namespace SyncPad.Server.Data;

public class SyncPadDbContext : DbContext
{
    public SyncPadDbContext(DbContextOptions<SyncPadDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TextContent> TextContents => Set<TextContent>(); // 保留用于向后兼容
    public DbSet<FileItem> FileItems => Set<FileItem>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<EncryptionKey> EncryptionKeys => Set<EncryptionKey>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User 配置
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
        });

        // TextContent 配置
        modelBuilder.Entity<TextContent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique(); // 每个用户只有一个文本
            entity.HasOne(e => e.User)
                  .WithOne(u => u.TextContent)
                  .HasForeignKey<TextContent>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // FileItem 配置
        modelBuilder.Entity<FileItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Hash).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Hash).HasMaxLength(16).IsRequired();
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(10).HasDefaultValue("active");
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Files)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessage 配置
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt); // 用于时间范围查询
            entity.HasIndex(e => new { e.UserId, e.IsDeleted }); // 用于过滤已删除消息
            entity.Property(e => e.EncryptedContent).HasMaxLength(10000);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.ChatMessages)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FileItem)
                  .WithMany()
                  .HasForeignKey(e => e.FileItemId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // EncryptionKey 配置
        modelBuilder.Entity<EncryptionKey>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Salt).HasMaxLength(64).IsRequired(); // 固定长度
            entity.Property(e => e.Version).HasDefaultValue(1);
            entity.HasOne(e => e.User)
                  .WithOne()
                  .HasForeignKey<EncryptionKey>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Device 配置
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique(); // 每个用户的设备ID唯一
            entity.HasIndex(e => e.LastActiveAt); // 用于清理不活跃设备
            entity.Property(e => e.DeviceName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DeviceType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DeviceId).HasMaxLength(100).IsRequired();
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Devices)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
