using SyncPad.Client.Core.Services;
using SyncPad.Web.Components;
using SyncPad.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 注册 HttpClient
builder.Services.AddHttpClient<IApiClient, ApiClient>();

// 注册服务
builder.Services.AddScoped<ITokenStorage, BrowserTokenStorage>();
builder.Services.AddScoped<IAuthManager, WebAuthManager>();
builder.Services.AddScoped<ITextHubClient, TextHubClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
