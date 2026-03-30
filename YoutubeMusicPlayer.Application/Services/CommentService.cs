using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class CommentService : ICommentService
{
    private readonly IUnitOfWork _unitOfWork;

    public CommentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<CommentDto>> GetSongCommentsAsync(int songId)
    {
        var comments = await _unitOfWork.Repository<Comment>().FindAsync(c => c.SongId == songId);
        var result = new List<CommentDto>();

        foreach (var c in comments.OrderByDescending(x => x.CreatedAt))
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(c.UserId);
            result.Add(new CommentDto
            {
                CommentId = c.CommentId,
                UserId = c.UserId,
                UserName = user?.Username ?? "Unknown",
                UserAvatarUrl = user?.AvatarUrl,
                SongId = c.SongId,
                Content = c.Content,
                CreatedAt = c.CreatedAt
            });
        }
        return result;
    }

    public async Task<CommentDto> CreateCommentAsync(int userId, int songId, string content)
    {
        var comment = new Comment
        {
            UserId = userId,
            SongId = songId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Comment>().AddAsync(comment);
        await _unitOfWork.CompleteAsync();

        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
        return new CommentDto
        {
            CommentId = comment.CommentId,
            UserId = userId,
            UserName = user?.Username ?? "Unknown",
            UserAvatarUrl = user?.AvatarUrl,
            SongId = songId,
            Content = content,
            CreatedAt = comment.CreatedAt
        };
    }

    public async Task DeleteCommentAsync(int commentId, int? userId = null)
    {
        var comment = await _unitOfWork.Repository<Comment>().GetByIdAsync(commentId);
        if (comment == null) return;

        // Nếu cung cấp userId, phải là chính chủ (hoặc admin bỏ qua check)
        if (userId != null && comment.UserId != userId) return;

        _unitOfWork.Repository<Comment>().Remove(comment);
        await _unitOfWork.CompleteAsync();
    }

    public async Task ToggleLikeAsync(int userId, int songId)
    {
        var existing = await _unitOfWork.Repository<SongLike>().FirstOrDefaultAsync(l => l.UserId == userId && l.SongId == songId);
        if (existing != null)
        {
            _unitOfWork.Repository<SongLike>().Remove(existing);
        }
        else
        {
            await _unitOfWork.Repository<SongLike>().AddAsync(new SongLike
            {
                UserId = userId,
                SongId = songId,
                LikedAt = DateTime.UtcNow
            });
        }
        await _unitOfWork.CompleteAsync();
    }

    public async Task<bool> IsLikedByUserAsync(long songId, int userId)
    {
        var l = await _unitOfWork.Repository<SongLike>().FirstOrDefaultAsync(x => x.SongId == (int)songId && x.UserId == userId);
        return l != null;
    }

    public async Task<int> GetLikeCountAsync(int songId)
    {
        var likes = await _unitOfWork.Repository<SongLike>().FindAsync(l => l.SongId == songId);
        return likes.Count();
    }

    public async Task<IEnumerable<CommentDto>> GetAllCommentsAsync()
    {
        var comments = await _unitOfWork.Repository<Comment>().GetAllAsync();
        var result = new List<CommentDto>();

        foreach (var c in comments.OrderByDescending(x => x.CreatedAt))
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(c.UserId);
            var song = await _unitOfWork.Repository<Song>().GetByIdAsync(c.SongId);
            result.Add(new CommentDto
            {
                CommentId = c.CommentId,
                UserId = c.UserId,
                UserName = user?.Username ?? "Unknown",
                SongId = c.SongId,
                SongTitle = song?.Title ?? "Unknown Song",
                Content = c.Content,
                CreatedAt = c.CreatedAt
            });
        }
        return result;
    }
}
