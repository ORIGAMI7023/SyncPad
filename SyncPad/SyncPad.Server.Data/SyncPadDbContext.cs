using Microsoft.EntityFrameworkCore;
using SyncPad.Server.Data.Entities;

namespace SyncPad.Server.Data;

public class SyncPadDbContext : DbContext
{
    public SyncPadDbContext(DbContextOptions<SyncPadDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TextContent> TextContents => Set<TextContent>();
    public DbSet<FileItem> FileItems => Set<FileItem>();

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
    }
}
