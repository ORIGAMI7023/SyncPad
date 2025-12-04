using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyncPad.Server.Core.Services;
using SyncPad.Shared.Models;

namespace SyncPad.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TextController : ControllerBase
{
    private readonly ITextSyncService _textSyncService;

    public TextController(ITextSyncService textSyncService)
    {
        _textSyncService = textSyncService;
    }

    /// <summary>
    /// 获取当前用户的文本内容
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<TextSyncMessage>>> GetText()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<TextSyncMessage>.Fail("未授权"));
        }

        var text = await _textSyncService.GetTextAsync(userId.Value);
        if (text == null)
        {
            return Ok(ApiResponse<TextSyncMessage>.Ok(new TextSyncMessage
            {
                Content = "",
                UpdatedAt = DateTime.UtcNow,
                SenderId = userId.Value
            }));
        }

        return Ok(ApiResponse<TextSyncMessage>.Ok(text));
    }

    /// <summary>
    /// 更新当前用户的文本内容
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TextSyncMessage>>> UpdateText([FromBody] TextSyncMessage message)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<TextSyncMessage>.Fail("未授权"));
        }

        var result = await _textSyncService.UpdateTextAsync(userId.Value, message.Content);
        return Ok(ApiResponse<TextSyncMessage>.Ok(result));
    }

    private int? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}
