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

        // 注册 HttpClient
        builder.Services.AddHttpClient<IApiClient, ApiClient>();

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
