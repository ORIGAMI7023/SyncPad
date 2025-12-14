using SyncPad.Client.Core.Services;

namespace SyncPad.Client
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
        }

        protected override async void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler?.MauiContext?.Services != null)
            {
                try
                {
                    var authManager = Handler.MauiContext.Services.GetService(typeof(IAuthManager)) as IAuthManager;
                    if (authManager != null)
                    {
                        // 尝试恢复会话
                        var restored = await authManager.TryRestoreSessionAsync();
                        if (restored)
                        {
                            // 会话恢复成功，导航到主页面
                            await GoToAsync("//PadPage");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"自动登录失败: {ex.Message}");
                }
            }
        }
    }
}
