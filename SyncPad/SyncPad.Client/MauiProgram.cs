using Microsoft.Extensions.Logging;
using SyncPad.Client.Core.Services;
using SyncPad.Client.Services;
using SyncPad.Client.ViewModels;
using SyncPad.Client.Views;
#if WINDOWS
using SyncPad.Client.Platforms.Windows;
#elif MACCATALYST
using SyncPad.Client.Platforms.MacCatalyst;
#elif ANDROID
using SyncPad.Client.Platforms.Android;
#elif IOS
using SyncPad.Client.Platforms.iOS;
#endif

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
        builder.Services.AddHttpClient("SyncPadApi");

        // 注册 ApiClient 为单例（确保 token 共享）
        builder.Services.AddSingleton<IApiClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient("SyncPadApi");
            return new ApiClient(httpClient);
        });

        // 注册平台特定服务
        builder.Services.AddPlatformServices();

        // 注册共享服务
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
