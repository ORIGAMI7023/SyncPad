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
    }
}
