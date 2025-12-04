using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SyncPad.Server.Core.Services;
using SyncPad.Server.Data;
using SyncPad.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

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
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // 允许 SignalR 通过查询字符串传递 Token
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
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
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100MB
});

// 配置 Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 初始化数据库
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SyncPadDbContext>();
    DbInitializer.Initialize(context);
}

// 配置 HTTP 管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TextHub>("/hubs/text").RequireCors("SignalR");

app.Run();
