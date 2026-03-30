using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ICommentService
{
    Task<IEnumerable<CommentDto>> GetSongCommentsAsync(int songId);
    Task<CommentDto> CreateCommentAsync(int userId, int songId, string content);
    Task DeleteCommentAsync(int commentId, int? userId = null); // Admin skips userId check
    
    // Song Interaction
    Task ToggleLikeAsync(int userId, int songId);
    Task<bool> IsLikedByUserAsync(long songId, int userId);
    Task<int> GetLikeCountAsync(int songId);
    
    // Admin management
    Task<IEnumerable<CommentDto>> GetAllCommentsAsync();
}
