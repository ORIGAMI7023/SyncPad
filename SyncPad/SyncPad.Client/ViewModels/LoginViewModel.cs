using System.Windows.Input;
using SyncPad.Client.Core.Services;

namespace SyncPad.Client.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly IAuthManager _authManager;

    private string _username = "";
    private string _password = "";
    private string _errorMessage = string.Empty;
    private bool _isLoading;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand LoginCommand { get; }

    public event Action? LoginSucceeded;

    public LoginViewModel(IAuthManager authManager)
    {
        _authManager = authManager;
        LoginCommand = new Command(async () => await LoginAsync(), () => !IsLoading);
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            var restored = await _authManager.TryRestoreSessionAsync();
            if (restored && _authManager.IsLoggedIn)
            {
                LoginSucceeded?.Invoke();
                return true;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"自动登录失败: {ex.Message}";
        }

        return false;
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入用户名和密码";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var response = await _authManager.LoginAsync(Username, Password);

            if (response.Success)
            {
                LoginSucceeded?.Invoke();
            }
            else
            {
                ErrorMessage = response.ErrorMessage ?? "登录失败";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"登录出错: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
