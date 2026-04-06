namespace SyncPad.Client
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // 启动时清理过期缓存
            Task.Run(async () =>
            {
                try
                {
                    var cacheManager = MauiProgram.GetCacheManager();
                    if (cacheManager != null)
                    {
                        await cacheManager.CleanupExpiredCacheAsync();
                    }
                }
                catch { }
            });

            return new Window(new AppShell());
        }
    }
}