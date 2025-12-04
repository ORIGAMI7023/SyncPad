using SyncPad.Client.ViewModels;

namespace SyncPad.Client.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        viewModel.LoginSucceeded += OnLoginSucceeded;
    }

    private async void OnLoginSucceeded()
    {
        await Shell.Current.GoToAsync("//PadPage");
    }
}
