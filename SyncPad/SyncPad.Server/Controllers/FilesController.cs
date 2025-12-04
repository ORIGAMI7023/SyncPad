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

    public FilesController(IFileService fileService, IHubContext<TextHub> hubContext)
    {
        _fileService = fileService;
        _hubContext = hubContext;
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
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB 限制
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
    /// 下载文件
    /// </summary>
    [HttpGet("{fileId}")]
    public async Task<IActionResult> DownloadFile(int fileId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var (stream, mimeType, fileName) = await _fileService.DownloadFileAsync(userId.Value, fileId);

        if (stream == null)
            return NotFound();

        return File(stream, mimeType ?? "application/octet-stream", fileName);
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
