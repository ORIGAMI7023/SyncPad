using System.Windows.Input;
using SyncPad.Client.Core.Services;
using SyncPad.Shared.Models;

namespace SyncPad.Client.ViewModels;

public class PadViewModel : BaseViewModel, IDisposable
{
    private readonly IAuthManager _authManager;
    private readonly IApiClient _apiClient;
    private readonly ITextHubClient _textHubClient;

    private string _content = string.Empty;
    private string _connectionStatus = "未连接";
    private bool _isConnected;
    private bool _isUpdatingFromServer;
    private CancellationTokenSource? _throttleCts;
    private readonly object _throttleLock = new();

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value) && !_isUpdatingFromServer)
            {
                // 节流发送更新
                ThrottleSendUpdate();
            }
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string Username => _authManager.Username ?? "未知用户";

    public ICommand LogoutCommand { get; }
    public ICommand RefreshCommand { get; }

    public event Action? LogoutRequested;

    public PadViewModel(IAuthManager authManager, IApiClient apiClient, ITextHubClient textHubClient)
    {
        _authManager = authManager;
        _apiClient = apiClient;
        _textHubClient = textHubClient;

        LogoutCommand = new Command(async () => await LogoutAsync());
        RefreshCommand = new Command(async () => await RefreshTextAsync());

        // 监听连接状态变化
        _textHubClient.ConnectionStateChanged += OnConnectionStateChanged;
        _textHubClient.TextUpdateReceived += OnTextUpdateReceived;
    }

    public async Task InitializeAsync()
    {
        // 连接 SignalR
        await ConnectToHubAsync();

        // 加载初始文本
        await RefreshTextAsync();
    }

    private async Task ConnectToHubAsync()
    {
        try
        {
            ConnectionStatus = "连接中...";
            var hubUrl = _authManager.GetHubUrl();
            var token = _authManager.Token;

            if (!string.IsNullOrEmpty(token))
            {
                await _textHubClient.ConnectAsync(hubUrl, token);
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"连接失败: {ex.Message}";
        }
    }

    private async Task RefreshTextAsync()
    {
        try
        {
            var response = await _apiClient.GetTextAsync();
            if (response.Success && response.Data != null)
            {
                _isUpdatingFromServer = true;
                Content = response.Data.Content;
                _isUpdatingFromServer = false;
            }
        }
        catch (Exception ex)
        {
            // 静默处理错误
            System.Diagnostics.Debug.WriteLine($"刷新文本失败: {ex.Message}");
        }
    }

    private void OnConnectionStateChanged(bool connected)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = connected;
            ConnectionStatus = connected ? "已连接" : "已断开";
        });
    }

    private void OnTextUpdateReceived(TextSyncMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isUpdatingFromServer = true;
            Content = message.Content;
            _isUpdatingFromServer = false;
        });
    }

    private void ThrottleSendUpdate()
    {
        lock (_throttleLock)
        {
            _throttleCts?.Cancel();
            _throttleCts = new CancellationTokenSource();
            var token = _throttleCts.Token;

            Task.Delay(300, token).ContinueWith(async _ =>
            {
                if (!token.IsCancellationRequested && _textHubClient.IsConnected)
                {
                    await _textHubClient.SendTextUpdateAsync(Content);
                }
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }

    private async Task LogoutAsync()
    {
        await _textHubClient.DisconnectAsync();
        await _authManager.LogoutAsync();
        LogoutRequested?.Invoke();
    }

    public void Dispose()
    {
        _textHubClient.ConnectionStateChanged -= OnConnectionStateChanged;
        _textHubClient.TextUpdateReceived -= OnTextUpdateReceived;
        _throttleCts?.Cancel();
        _throttleCts?.Dispose();
    }
}
