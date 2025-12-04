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

        // 配置 API 基础地址
        builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:7167");
        });

        // 注册服务
        builder.Services.AddSingleton<ITokenStorage, MauiTokenStorage>();
        builder.Services.AddSingleton<IAuthManager, AuthManager>();
        builder.Services.AddSingleton<ITextHubClient, TextHubClient>();

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
