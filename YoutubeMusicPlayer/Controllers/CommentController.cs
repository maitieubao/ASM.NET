using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> GetSongComments(int songId)
    {
        var comments = await _commentService.GetSongCommentsAsync(songId, CurrentUserId);
        return SuccessResponse(comments);
    }

    [HttpPost]
    public async Task<IActionResult> AddComment(int songId, string content, int? parentId = null)
    {
        if (CurrentUserId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(content)) return BadRequestResponse("Nội dung không được để trống");
        
        var comment = await _commentService.CreateCommentAsync(CurrentUserId.Value, songId, content, parentId);
        return SuccessResponse(comment);
    }

    [HttpPost]
    public async Task<IActionResult> EditComment(int commentId, string content)
    {
        if (CurrentUserId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(content)) return BadRequestResponse("Nội dung không được để trống");
        
        await _commentService.UpdateCommentAsync(commentId, CurrentUserId.Value, content);
        return SuccessResponse(new { success = true });
    }

    [HttpPost]
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
        
        await _commentService.ToggleCommentLikeAsync(CurrentUserId.Value, commentId);
        
        var count = await _commentService.GetCommentLikeCountAsync(commentId);
        var isLiked = await _commentService.IsCommentLikedAsync(CurrentUserId.Value, commentId);
        
        return SuccessResponse(new { count, isLiked });
    }
}
