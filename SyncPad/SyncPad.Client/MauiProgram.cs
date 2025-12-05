using Microsoft.Extensions.Logging;
using SyncPad.Client.Core.Services;
using SyncPad.Client.Services;
using SyncPad.Client.ViewModels;
using SyncPad.Client.Views;

namespace SyncPad.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // 配置 HttpClient
        builder.Services.AddHttpClient("SyncPadApi")
#if DEBUG && (MACCATALYST || IOS)
            // Mac/iOS 开发环境允许不安全的 localhost SSL 连接
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // 仅允许 localhost 跳过 SSL 验证
                    if (message.RequestUri?.Host == "localhost" || message.RequestUri?.Host == "127.0.0.1")
                        return true;
                    return errors == System.Net.Security.SslPolicyErrors.None;
                };
                return handler;
            })
#endif
            ;

        // 注册 ApiClient 为单例（确保 token 共享）
        builder.Services.AddSingleton<IApiClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("SyncPadApi");
            return new ApiClient(httpClient);
        });

        // 注册服务
#if MACCATALYST
        // Mac 端使用文件存储（避免 Keychain 签名问题）
        builder.Services.AddSingleton<ITokenStorage, FileTokenStorage>();
#else
        builder.Services.AddSingleton<ITokenStorage, MauiTokenStorage>();
#endif
        builder.Services.AddSingleton<IAuthManager, AuthManager>();
        builder.Services.AddSingleton<ITextHubClient, TextHubClient>();
        builder.Services.AddSingleton<IFileClient, FileClient>();
        builder.Services.AddSingleton<IFileCacheManager, FileCacheManager>();
        builder.Services.AddSingleton<IFileOperationService, FileOperationService>();

        // 注册 ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<PadViewModel>();

        // 注册 Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<PadPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
