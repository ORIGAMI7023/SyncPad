namespace SyncPad.Shared.Models;

/// <summary>
/// 登录响应 DTO
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public int UserId { get; set; }
    public string? ErrorMessage { get; set; }
}
