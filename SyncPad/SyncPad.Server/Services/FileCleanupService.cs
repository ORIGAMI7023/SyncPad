using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncPad.Server.Core.Services;

namespace SyncPad.Server.Services;

public class FileCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FileCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(30);

    public FileCleanupService(IServiceProvider serviceProvider, ILogger<FileCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("文件清理服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
                await fileService.CleanupExpiredFilesAsync();
                _logger.LogInformation("文件清理任务完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件清理任务失败");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("文件清理服务已停止");
    }
}
