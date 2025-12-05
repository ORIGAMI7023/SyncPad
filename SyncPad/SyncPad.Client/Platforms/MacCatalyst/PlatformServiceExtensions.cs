using Microsoft.Extensions.DependencyInjection;
using SyncPad.Client.Core.Services;
using SyncPad.Client.Services;

namespace SyncPad.Client.Platforms.MacCatalyst;

/// <summary>
/// Mac Catalyst 平台服务注册扩展
/// </summary>
public static class PlatformServiceExtensions
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        // Mac 使用文件存储（避免 Keychain 签名问题）
        services.AddSingleton<ITokenStorage, FileTokenStorage>();
        return services;
    }
}
