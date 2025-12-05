using SyncPad.Client.ViewModels;

namespace SyncPad.Client.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private bool _hasTriedAutoLogin = false;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        viewModel.LoginSucceeded += OnLoginSucceeded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 只在首次出现时尝试自动登录
        if (!_hasTriedAutoLogin)
        {
            _hasTriedAutoLogin = true;
            // 等待页面完全加载
            await Task.Delay(100);
            await _viewModel.TryAutoLoginAsync();
        }
    }

    private void OnLoginSucceeded()
    {
        // 在主线程上执行导航,使用 Dispatcher
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                // 使用绝对路由导航
                await Shell.Current.GoToAsync("//PadPage", true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导航失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");
            }
        });
    }
}
