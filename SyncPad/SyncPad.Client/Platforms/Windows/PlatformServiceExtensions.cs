using Microsoft.Extensions.DependencyInjection;
using SyncPad.Client.Core.Services;
using SyncPad.Client.Services;

namespace SyncPad.Client.Platforms.Windows;

/// <summary>
/// Windows 平台服务注册扩展
/// </summary>
public static class PlatformServiceExtensions
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        // Windows 使用 SecureStorage
        services.AddSingleton<ITokenStorage, MauiTokenStorage>();
        return services;
    }
}
