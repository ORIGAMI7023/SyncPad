using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SyncPad.Server.Core.Services;
using SyncPad.Server.Data;
using SyncPad.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// 配置 Kestrel 服务器选项（文件上传大小限制）
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

// 配置数据库
builder.Services.AddDbContext<SyncPadDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 配置 JWT 认证
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SyncPadDefaultSecretKey12345678";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SyncPad";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SyncPadUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = false, // 不验证过期时间
            ClockSkew = TimeSpan.Zero
        };

        // 添加认证失败日志
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"[JWT] 认证失败: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                var usernameClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Name);

                if (userIdClaim != null && usernameClaim != null &&
                    int.TryParse(userIdClaim.Value, out var userId))
                {
                    // 验证用户是否存在且用户名匹配
                    var dbContext = context.HttpContext.RequestServices.GetRequiredService<SyncPadDbContext>();
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

                    if (user == null)
                    {
                        Console.WriteLine($"[JWT] 用户ID {userId} 不存在，拒绝访问");
                        context.Fail("用户不存在");
                        return;
                    }

                    if (user.Username != usernameClaim.Value)
                    {
                        Console.WriteLine($"[JWT] Token用户名 {usernameClaim.Value} 与数据库用户名 {user.Username} 不匹配，拒绝访问");
                        context.Fail("用户名不匹配");
                        return;
                    }
                }

                Console.WriteLine($"[JWT] Token 验证成功: {context.Principal?.Identity?.Name}");
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                // 打印收到的 Token（前20个字符）
                var authHeader = context.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    Console.WriteLine($"[JWT] 收到 Authorization 头: {authHeader[..Math.Min(50, authHeader.Length)]}...");
                }

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                    Console.WriteLine($"[JWT] SignalR Token: {accessToken[..Math.Min(20, accessToken.Length)]}...");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 注册服务
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITextSyncService, TextSyncService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddHostedService<SyncPad.Server.Services.FileCleanupService>();
builder.Services.AddHostedService<SyncPad.Server.Services.TextCleanupService>();

// 配置 CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // SignalR 需要允许凭据
    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 配置 SignalR
builder.Services.AddSignalR();

// 配置控制器
builder.Services.AddControllers();

// 配置文件上传大小限制
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 1024; // 1GB
});

// 配置 Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 初始化数据库
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SyncPadDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // 从配置中读取管理员账户（如果有）
    var adminUsername = config["DefaultAdmin:Username"];
    var adminPassword = config["DefaultAdmin:Password"];

    DbInitializer.Initialize(context, adminUsername, adminPassword);
}

// 配置 HTTP 管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 添加请求日志中间件（调试用）
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var method = context.Request.Method;
    var authHeader = context.Request.Headers["Authorization"].ToString();

    Console.WriteLine($"[Request] {method} {path}");
    Console.WriteLine($"[Request] Authorization: {(string.IsNullOrEmpty(authHeader) ? "null" : authHeader[..Math.Min(60, authHeader.Length)])}...");

    await next();

    Console.WriteLine($"[Response] {context.Response.StatusCode}");
});

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TextHub>("/hubs/text").RequireCors("SignalR");

app.Run();
