namespace SyncPad.Shared.Models;

/// <summary>
/// 登录请求 DTO
/// </summary>
public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
