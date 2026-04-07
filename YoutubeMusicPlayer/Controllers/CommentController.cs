using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

[Authorize]
public class CommentController : BaseController
{
    private readonly ICommentService _commentService;

    public CommentController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetSongComments(int songId, int page = 1, int pageSize = 20)
    {
        var (comments, totalCount) = await _commentService.GetSongCommentsPaginatedAsync(songId, CurrentUserId, page, pageSize);
        return SuccessResponse(new { comments, totalCount, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> AddComment(int songId, string content, int? parentId = null)
    {
        if (CurrentUserId == null) return Unauthorized();
        
        if (string.IsNullOrWhiteSpace(content)) 
            return BadRequestResponse("Nội dung không được để trống");
            
        if (content.Length > 1000)
            return BadRequestResponse("Bình luận tối đa 1000 ký tự");

        // Basic Encode to prevent simple XSS
        var encodedContent = WebUtility.HtmlEncode(content);
        
        var comment = await _commentService.CreateCommentAsync(CurrentUserId.Value, songId, encodedContent, parentId);
        return SuccessResponse(comment);
    }

    [HttpPut]
    public async Task<IActionResult> EditComment(int commentId, string content)
    {
        if (CurrentUserId == null) return Unauthorized();
        
        if (string.IsNullOrWhiteSpace(content)) 
            return BadRequestResponse("Nội dung không được để trống");
            
        if (content.Length > 1000)
            return BadRequestResponse("Bình luận tối đa 1000 ký tự");

        var encodedContent = WebUtility.HtmlEncode(content);
        
        await _commentService.UpdateCommentAsync(commentId, CurrentUserId.Value, encodedContent);
        return SuccessResponse(new { success = true });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteComment(int commentId)
    {
        if (CurrentUserId == null) return Unauthorized();
        await _commentService.DeleteCommentAsync(commentId, CurrentUserId.Value);
        return SuccessResponse(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLike(int commentId)
    {
        if (CurrentUserId == null) return Unauthorized();
        
        // Combined call to reduce database round-trips
        var status = await _commentService.ToggleCommentLikeAndGetStatusAsync(CurrentUserId.Value, commentId);
        
        return SuccessResponse(status);
    }
}
