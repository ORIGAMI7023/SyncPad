using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SyncPad.Server.Core.Services;
using SyncPad.Server.Hubs;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;
    private readonly IHubContext<TextHub> _hubContext;
    private readonly IConfiguration _configuration;

    public FilesController(IFileService fileService, IHubContext<TextHub> hubContext, IConfiguration configuration)
    {
        _fileService = fileService;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    /// <summary>
    /// 获取文件列表
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<FileListResponse>>> GetFiles()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<FileListResponse>.Fail("未授权"));

        var files = await _fileService.GetFilesAsync(userId.Value);
        return Ok(ApiResponse<FileListResponse>.Ok(new FileListResponse { Files = files }));
    }

    /// <summary>
    /// 检查同名文件是否存在
    /// </summary>
    [HttpGet("exists")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckFileExists([FromQuery] string fileName)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<bool>.Fail("未授权"));

        var exists = await _fileService.FileExistsAsync(userId.Value, fileName);
        return Ok(ApiResponse<bool>.Ok(exists));
    }

    /// <summary>
    /// 上传文件
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(1024 * 1024 * 1024)] // 1GB 限制
    public async Task<ActionResult<FileUploadResponse>> UploadFile(
        IFormFile file,
        [FromQuery] bool overwrite = false)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new FileUploadResponse { Success = false, ErrorMessage = "未授权" });

        if (file == null || file.Length == 0)
            return BadRequest(new FileUploadResponse { Success = false, ErrorMessage = "文件为空" });

        using var stream = file.OpenReadStream();
        var result = await _fileService.UploadFileAsync(
            userId.Value,
            file.FileName,
            stream,
            file.ContentType,
            overwrite);

        if (result.Success && result.File != null)
        {
            // 通知同账号其他客户端
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveFileUpdate", new FileSyncMessage
                {
                    Action = "added",
                    File = result.File
                });
        }

        return Ok(result);
    }

    /// <summary>
    /// 下载文件（支持 Range 请求）
    /// </summary>
    [HttpGet("{fileId}")]
    [AllowAnonymous] // 允许匿名访问，因为我们会在方法内部验证token
    public async Task<IActionResult> DownloadFile(int fileId, [FromQuery] string? token = null)
    {
        int? userId;

        // 如果提供了token查询参数，使用它进行认证
        if (!string.IsNullOrEmpty(token))
        {
            // 验证token并获取userId
            var principal = await ValidateTokenAsync(token);
            if (principal == null)
                return Unauthorized();

            var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var parsedUserId))
                return Unauthorized();

            userId = parsedUserId;
        }
        else
        {
            // 使用标准Bearer认证
            userId = GetUserId();
            if (userId == null)
                return Unauthorized();
        }

        var (stream, mimeType, fileName, fileSize) = await _fileService.DownloadFileAsync(userId.Value, fileId);

        if (stream == null)
            return NotFound();

        // 支持 Range 请求（用于断点续传和分块下载）
        var rangeHeader = Request.Headers["Range"].ToString();
        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
        {
            var range = rangeHeader.Replace("bytes=", "").Split('-');
            if (range.Length == 2 && long.TryParse(range[0], out var start))
            {
                var end = string.IsNullOrEmpty(range[1]) ? fileSize - 1 : long.Parse(range[1]);
                var length = end - start + 1;

                stream.Seek(start, SeekOrigin.Begin);

                Response.StatusCode = 206; // Partial Content
                Response.Headers["Content-Range"] = $"bytes {start}-{end}/{fileSize}";
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.ContentLength = length;

                return File(stream, string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType, fileName, enableRangeProcessing: true);
            }
        }

        // 标准全量下载
        Response.Headers["Accept-Ranges"] = "bytes";
        return File(stream, string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType, fileName, enableRangeProcessing: true);
    }

    private async Task<System.Security.Claims.ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var key = System.Text.Encoding.ASCII.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key未配置"));

            var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = false, // 不验证过期时间
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return await Task.FromResult(principal);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    [HttpDelete("{fileId}")]
    public async Task<ActionResult<ApiResponse>> DeleteFile(int fileId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse.Fail("未授权"));

        var success = await _fileService.DeleteFileAsync(userId.Value, fileId);

        if (success)
        {
            // 通知同账号其他客户端
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveFileUpdate", new FileSyncMessage
                {
                    Action = "deleted",
                    FileId = fileId
                });

            return Ok(ApiResponse.Ok());
        }

        return NotFound(ApiResponse.Fail("文件不存在"));
    }

    private int? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            return userId;
        return null;
    }
}
