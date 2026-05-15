using Microsoft.JSInterop;
using System.Text.Json;

namespace SyncPad.Web.Services;

/// <summary>
/// 端到端加密服务（Blazor封装）
/// </summary>
public class E2EECryptoService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _cryptoModule;
    private IJSObjectReference? _cryptoInstance;

    public E2EECryptoService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// 初始化加密实例
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_cryptoInstance == null)
        {
            // 加载JavaScript加密库
            _cryptoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/crypto-e2ee.js");

            // 创建加密实例
            _cryptoInstance = await _cryptoModule.InvokeAsync<IJSObjectReference>("new", "E2EECrypto");
        }
    }

    /// <summary>
    /// 从密码和盐值派生密钥
    /// </summary>
    public async Task DeriveKeyAsync(string password, string saltBase64)
    {
        await EnsureInitializedAsync();
        await _cryptoInstance!.InvokeVoidAsync("deriveKey", password, saltBase64);
    }

    /// <summary>
    /// 加密文本
    /// </summary>
    public async Task<EncryptionResult> EncryptAsync(string plaintext)
    {
        await EnsureInitializedAsync();
        var result = await _cryptoInstance!.InvokeAsync<JsonElement>("encrypt", plaintext);

        return new EncryptionResult
        {
            EncryptedData = result.GetProperty("encryptedData").GetString() ?? string.Empty,
            IV = result.GetProperty("iv").GetString() ?? string.Empty
        };
    }

    /// <summary>
    /// 解密文本
    /// </summary>
    public async Task<string> DecryptAsync(string encryptedDataBase64, string ivBase64)
    {
        await EnsureInitializedAsync();
        return await _cryptoInstance!.InvokeAsync<string>("decrypt", encryptedDataBase64, ivBase64);
    }

    /// <summary>
    /// 保存密钥到IndexedDB
    /// </summary>
    public async Task SaveKeyToIndexedDBAsync(string sessionToken)
    {
        await EnsureInitializedAsync();
        await _cryptoInstance!.InvokeVoidAsync("saveKeyToIndexedDB", sessionToken);
    }

    /// <summary>
    /// 从IndexedDB加载密钥
    /// </summary>
    public async Task LoadKeyFromIndexedDBAsync(string sessionToken)
    {
        await EnsureInitializedAsync();
        await _cryptoInstance!.InvokeVoidAsync("loadKeyFromIndexedDB", sessionToken);
    }

    /// <summary>
    /// 清除IndexedDB中的密钥
    /// </summary>
    public async Task ClearKeyFromIndexedDBAsync()
    {
        await EnsureInitializedAsync();
        await _cryptoInstance!.InvokeVoidAsync("clearKeyFromIndexedDB");
    }

    /// <summary>
    /// 检查密钥是否已初始化
    /// </summary>
    public async Task<bool> IsKeyInitializedAsync()
    {
        await EnsureInitializedAsync();
        return await _cryptoInstance!.InvokeAsync<bool>("isKeyInitialized");
    }

    /// <summary>
    /// 确保已初始化
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_cryptoInstance == null)
        {
            await InitializeAsync();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_cryptoInstance != null)
        {
            try
            {
                await _cryptoInstance.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // 忽略连接断开异常
            }
        }

        if (_cryptoModule != null)
        {
            try
            {
                await _cryptoModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // 忽略连接断开异常
            }
        }
    }
}

/// <summary>
/// 加密结果
/// </summary>
public class EncryptionResult
{
    public string EncryptedData { get; set; } = string.Empty;
    public string IV { get; set; } = string.Empty;
}
