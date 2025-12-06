using SyncPad.Client.Core.Services;

namespace SyncPad.Client
{
    public partial class AppShell : Shell
    {
        private readonly IAuthManager _authManager;

        public AppShell()
        {
            InitializeComponent();

            // 从依赖注入容器获取 AuthManager
            _authManager = Handler?.MauiContext?.Services.GetRequiredService<IAuthManager>()
                ?? throw new InvalidOperationException("无法获取 IAuthManager 服务");
        }

        protected override async void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler != null)
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
