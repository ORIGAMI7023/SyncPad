using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SyncPad.Server.Data;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Core.Services;

/// <summary>
/// 认证服务实现
/// </summary>
public class AuthService : IAuthService
{
    private readonly SyncPadDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(SyncPadDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            return new LoginResponse
            {
                Success = false,
                ErrorMessage = "用户不存在"
            };
        }

        if (!DbInitializer.VerifyPassword(request.Password, user.PasswordHash))
        {
            return new LoginResponse
            {
                Success = false,
                ErrorMessage = "密码错误"
            };
        }

        var token = GenerateJwtToken(user.Id, user.Username);

        return new LoginResponse
        {
            Success = true,
            Token = token,
            Username = user.Username,
            UserId = user.Id
        };
    }

    public int? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "SyncPadDefaultSecretKey12345678");

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "SyncPad",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "SyncPadUsers",
                ValidateLifetime = false, // 不验证过期时间
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GenerateJwtToken(int userId, string username)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "SyncPadDefaultSecretKey12345678");
        var securityKey = new SymmetricSecurityKey(key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "SyncPad",
            audience: _configuration["Jwt:Audience"] ?? "SyncPadUsers",
            claims: claims,
            expires: null, // Token 永不过期
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
