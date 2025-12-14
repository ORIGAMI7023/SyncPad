using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncPad.Server.Data;

namespace SyncPad.Server.Services;

/// <summary>
/// 文本内容清理服务 - 定期删除7天未访问的文本内容
/// </summary>
public class TextCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TextCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24); // 每天执行一次
    private readonly TimeSpan _textTtl = TimeSpan.FromDays(7); // 7天未访问则删除

    public TextCleanupService(IServiceProvider serviceProvider, ILogger<TextCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("文本清理服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SyncPadDbContext>();

                var cutoffTime = DateTime.UtcNow - _textTtl;
                var expiredTexts = dbContext.TextContents
                    .Where(t => t.LastAccessedAt < cutoffTime)
                    .ToList();

                if (expiredTexts.Any())
                {
                    foreach (var text in expiredTexts)
                    {
                        // 清空文本内容，但保留记录
                        text.Content = "";
                        text.UpdatedAt = DateTime.UtcNow;
                        _logger.LogInformation($"已清除用户 {text.UserId} 的过期文本内容（最后访问: {text.LastAccessedAt}）");
                    }

                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation($"文本清理任务完成，共清理 {expiredTexts.Count} 条记录");
                }
                else
                {
                    _logger.LogInformation("文本清理任务完成，无过期内容");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文本清理任务失败");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("文本清理服务已停止");
    }
}
