using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ICommentService
{
    Task<IEnumerable<CommentDto>> GetSongCommentsAsync(int songId, int? currentUserId = null);
    
    // Pagination supported for high-volume songs
    Task<(IEnumerable<CommentDto> Comments, int TotalCount)> GetSongCommentsPaginatedAsync(int songId, int? userId, int page, int pageSize);
    
    Task<CommentDto> CreateCommentAsync(int userId, int songId, string content, int? parentId = null);
    Task UpdateCommentAsync(int commentId, int userId, string content);
    Task DeleteCommentAsync(int commentId, int? userId = null); 
    
    // Comment Likes - Optimized to reduce round-trips
    Task<CommentLikeStatusDto> ToggleCommentLikeAndGetStatusAsync(int userId, int commentId);
    
    Task ToggleCommentLikeAsync(int userId, int commentId);
    Task<int> GetCommentLikeCountAsync(int commentId);
    Task<bool> IsCommentLikedAsync(int userId, int commentId);
    
    // Admin management
    Task<IEnumerable<CommentDto>> GetAllCommentsAsync();
}
