using SyncPad.Client.Core.Services;

namespace SyncPad.Client
{
    public partial class AppShell : Shell
    {
        private IAuthManager? _authManager;

        public AppShell()
        {
            InitializeComponent();
        }

        protected override async void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler != null && _authManager == null)
            {
                // 从依赖注入容器获取 AuthManager
                _authManager = Handler.MauiContext?.Services.GetService<IAuthManager>();

                if (_authManager != null)
                {
                    // 尝试恢复会话
                    var restored = await _authManager.TryRestoreSessionAsync();
                    if (restored)
                    {
                        // 会话恢复成功，导航到主页面
                        await GoToAsync("//PadPage");
                    }
                }
            }
        }
    }
}
