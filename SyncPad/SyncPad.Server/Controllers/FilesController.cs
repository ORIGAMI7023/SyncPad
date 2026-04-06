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
    /// 检查 hash 是否已存在
    /// </summary>
    [HttpGet("check-hash")]
    public async Task<ActionResult<ApiResponse<CheckHashResult>>> CheckHash([FromQuery] string hash)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<CheckHashResult>.Fail("未授权"));

        if (string.IsNullOrEmpty(hash) || hash.Length != 16)
            return BadRequest(ApiResponse<CheckHashResult>.Fail("hash 格式无效，需要16字符的十六进制"));

        var result = await _fileService.CheckHashAsync(hash);
        return Ok(ApiResponse<CheckHashResult>.Ok(result));
    }

    /// <summary>
    /// 上传文件（两步上传：先检查 hash，再传文件体）
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(1024 * 1024 * 1024)] // 1GB 限制
    public async Task<ActionResult<FileUploadResponse>> UploadFile(
        IFormFile file,
        [FromForm] string hash,
        [FromQuery] bool overwrite = false)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new FileUploadResponse { Success = false, ErrorMessage = "未授权" });

        if (file == null || file.Length == 0)
            return BadRequest(new FileUploadResponse { Success = false, ErrorMessage = "文件为空" });

        if (string.IsNullOrEmpty(hash) || hash.Length != 16)
            return BadRequest(new FileUploadResponse { Success = false, ErrorMessage = "hash 格式无效" });

        using var stream = file.OpenReadStream();
        var result = await _fileService.UploadFileAsync(
            userId.Value,
            file.FileName,
            stream,
            file.ContentType,
            hash);

        if (result.Success && result.File != null)
        {
            await NotifyFileUpdateAsync(userId.Value, "added", result.File);
        }

        return Ok(result);
    }

    /// <summary>
    /// 秒传：仅通过 hash 激活已有文件
    /// </summary>
    [HttpPost("instant-upload")]
    public async Task<ActionResult<FileUploadResponse>> InstantUpload([FromBody] InstantUploadRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new FileUploadResponse { Success = false, ErrorMessage = "未授权" });

        if (string.IsNullOrEmpty(request.Hash) || request.Hash.Length != 16)
            return BadRequest(new FileUploadResponse { Success = false, ErrorMessage = "hash 格式无效" });

        var result = await _fileService.InstantUploadAsync(userId.Value, request.FileName, request.Hash);

        if (result.Success && result.File != null)
        {
            await NotifyFileUpdateAsync(userId.Value, "added", result.File);
        }

        return Ok(result);
    }

    /// <summary>
    /// 下载文件（支持 Range 请求）
    /// </summary>
    [HttpGet("{fileId}")]
    [AllowAnonymous] // 允许匿名访问，通过 token 参数验证
    public async Task<IActionResult> DownloadFile(int fileId, [FromQuery] string? token = null)
    {
        int? userId;

        if (!string.IsNullOrEmpty(token))
        {
            var principal = await ValidateTokenAsync(token);
            if (principal == null)
                return Unauthorized();

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var parsedUserId))
                return Unauthorized();

            userId = parsedUserId;
        }
        else
        {
            userId = GetUserId();
            if (userId == null)
                return Unauthorized();
        }

        var (stream, mimeType, fileName, fileSize) = await _fileService.DownloadFileAsync(userId.Value, fileId);

        if (stream == null)
            return NotFound();

        // 支持 Range 请求
        var rangeHeader = Request.Headers["Range"].ToString();
        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
        {
            var range = rangeHeader.Replace("bytes=", "").Split('-');
            if (range.Length == 2 && long.TryParse(range[0], out var start))
            {
                var end = string.IsNullOrEmpty(range[1]) ? fileSize - 1 : long.Parse(range[1]);
                var length = end - start + 1;

                stream.Seek(start, SeekOrigin.Begin);

                Response.StatusCode = 206;
                Response.Headers["Content-Range"] = $"bytes {start}-{end}/{fileSize}";
                Response.Headers["Accept-Ranges"] = "bytes";
                Response.ContentLength = length;

                return File(stream, string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType, fileName, enableRangeProcessing: true);
            }
        }

        Response.Headers["Accept-Ranges"] = "bytes";
        return File(stream, string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType, fileName, enableRangeProcessing: true);
    }

    /// <summary>
    /// 删除文件（软删除）
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

    /// <summary>
    /// 重命名文件
    /// </summary>
    [HttpPut("{fileId}/rename")]
    public async Task<ActionResult<ApiResponse<FileItemDto>>> RenameFile(int fileId, [FromBody] RenameFileRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse<FileItemDto>.Fail("未授权"));

        if (string.IsNullOrWhiteSpace(request.NewFileName))
            BadRequest(ApiResponse<FileItemDto>.Fail("文件名不能为空"));

        var result = await _fileService.RenameFileAsync(userId.Value, fileId, request.NewFileName);

        if (result != null)
        {
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveFileUpdate", new FileSyncMessage
                {
                    Action = "renamed",
                    FileId = fileId,
                    NewFileName = request.NewFileName
                });

            return Ok(ApiResponse<FileItemDto>.Ok(result));
        }

        return NotFound(ApiResponse<FileItemDto>.Fail("文件不存在"));
    }

    private async Task NotifyFileUpdateAsync(int userId, string action, FileItemDto file)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("ReceiveFileUpdate", new FileSyncMessage
            {
                Action = action,
                File = file
            });
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
                ValidateLifetime = false,
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

    private int? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            return userId;
        return null;
    }
}

/// <summary>
/// 秒传请求
/// </summary>
public class InstantUploadRequest
{
    public required string FileName { get; set; }
    public required string Hash { get; set; }
}
