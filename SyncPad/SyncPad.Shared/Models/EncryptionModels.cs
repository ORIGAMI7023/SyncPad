namespace SyncPad.Shared.Models;

/// <summary>
/// 密钥派生请求
/// </summary>
public class KeyDerivationRequest
{
    public string Password { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
}

/// <summary>
/// 密钥派生响应
/// </summary>
public class KeyDerivationResponse
{
    public string Salt { get; set; } = string.Empty;
    public int Version { get; set; }
}

/// <summary>
/// 加密数据请求
/// </summary>
public class EncryptionRequest
{
    public string PlainData { get; set; } = string.Empty;
    public string KeyBase64 { get; set; } = string.Empty;
}

/// <summary>
/// 加密数据响应
/// </summary>
public class EncryptionResponse
{
    public string EncryptedDataBase64 { get; set; } = string.Empty;
    public string IVBase64 { get; set; } = string.Empty;
}

/// <summary>
/// 解密数据请求
/// </summary>
public class DecryptionRequest
{
    public string EncryptedDataBase64 { get; set; } = string.Empty;
    public string IVBase64 { get; set; } = string.Empty;
    public string KeyBase64 { get; set; } = string.Empty;
}

/// <summary>
/// 用户密钥信息
/// </summary>
public class UserKeyInfo
{
    public string Salt { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool HasKey { get; set; }
}
