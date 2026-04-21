using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Controllers;

[Authorize(Roles = "Admin")]
public class AdminSupportController : Controller
{
    private readonly ICommentService _commentService;

    public AdminSupportController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    [HttpGet]
    public IActionResult Reports()
    {
        return RedirectToAction("Index", "AdminReport");
    }

    [HttpGet]
    public async Task<IActionResult> Comments()
    {
        var comments = await _commentService.GetAllCommentsAsync();
        return View(comments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id)
    {
        await _commentService.DeleteCommentAsync(id, null);
        TempData["Success"] = "Comment deleted.";
        return RedirectToAction(nameof(Comments));
    }
}
