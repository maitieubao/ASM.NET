using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

    public async Task<(IEnumerable<CommentDto> Comments, int TotalCount)> GetSongCommentsPaginatedAsync(int songId, int? userId, int page, int pageSize)
    {
        var query = _unitOfWork.Repository<Comment>().Query()
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.SongId == songId);

        var totalCount = await query.CountAsync();
        
        var comments = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var commentIds = comments.Select(c => c.CommentId).ToList();
        
        var likes = await _unitOfWork.Repository<CommentLike>().Query()
            .AsNoTracking()
            .Where(l => commentIds.Contains(l.CommentId))
            .ToListAsync();

        // Since it's paginated, we build a flat list or partial tree for the current page
        var dtos = comments.Select(c => new CommentDto
        {
            CommentId = c.CommentId,
            UserId = c.UserId,
            UserName = c.User?.Username ?? "Unknown",
            UserAvatarUrl = c.User?.AvatarUrl,
            SongId = c.SongId,
            Content = c.Content,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            ParentCommentId = c.ParentCommentId,
            LikeCount = likes.Count(l => l.CommentId == c.CommentId),
            IsLiked = userId.HasValue && likes.Any(l => l.CommentId == c.CommentId && l.UserId == userId.Value)
        }).ToList();

        return (dtos, totalCount);
    }

    public async Task<IEnumerable<CommentDto>> GetSongCommentsAsync(int songId, int? currentUserId = null)
    {
        // ... giữ nguyên cũ nếu cần, hoặc refactor về dùng paginated ...
        var (comments, _) = await GetSongCommentsPaginatedAsync(songId, currentUserId, 1, 100);
        return comments;
    }

    private IEnumerable<CommentDto> BuildCommentTree(List<Comment> allComments, List<CommentLike> allLikes, int? currentUserId)
    {
        var flatDtos = allComments.Select(c => new CommentDto
        {
            CommentId = c.CommentId,
            UserId = c.UserId,
            UserName = c.User?.Username ?? "Unknown",
            UserAvatarUrl = c.User?.AvatarUrl,
            SongId = c.SongId,
            Content = c.Content,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            ParentCommentId = c.ParentCommentId,
            LikeCount = allLikes.Count(l => l.CommentId == c.CommentId),
            IsLiked = currentUserId.HasValue && allLikes.Any(l => l.CommentId == c.CommentId && l.UserId == currentUserId.Value),
            Replies = new List<CommentDto>()
        }).ToList();

        var tree = new List<CommentDto>();
        var dict = flatDtos.ToDictionary(d => d.CommentId);

        foreach (var dto in flatDtos)
        {
            if (dto.ParentCommentId == null)
            {
                tree.Add(dto);
            }
            else if (dict.TryGetValue(dto.ParentCommentId.Value, out var parent))
            {
                ((List<CommentDto>)parent.Replies).Add(dto);
            }
        }

        // Sort top level and each level of replies
        return OrderTree(tree);
    }

    private IEnumerable<CommentDto> OrderTree(IEnumerable<CommentDto> nodes)
    {
        var sorted = nodes.OrderByDescending(n => n.CreatedAt).ToList();
        foreach (var node in sorted)
        {
            if (node.Replies.Any())
                node.Replies = OrderTree(node.Replies).ToList();
        }
        return sorted;
    }

    public async Task<CommentDto> CreateCommentAsync(int userId, int songId, string content, int? parentId = null)
    {
        var comment = new Comment
        {
            UserId = userId,
            SongId = songId,
            Content = content,
            ParentCommentId = parentId,
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
            CreatedAt = comment.CreatedAt,
            ParentCommentId = parentId
        };
    }

    public async Task UpdateCommentAsync(int commentId, int userId, string content)
    {
        var comment = await _unitOfWork.Repository<Comment>().GetByIdAsync(commentId);
        if (comment != null && comment.UserId == userId)
        {
            comment.Content = content;
            comment.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Comment>().Update(comment);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task<CommentLikeStatusDto> ToggleCommentLikeAndGetStatusAsync(int userId, int commentId)
    {
        var existing = await _unitOfWork.Repository<CommentLike>()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CommentId == commentId);
            
        bool isLiked;
        if (existing != null)
        {
            _unitOfWork.Repository<CommentLike>().Remove(existing);
            isLiked = false;
        }
        else
        {
            await _unitOfWork.Repository<CommentLike>().AddAsync(new CommentLike 
            { 
                UserId = userId, 
                CommentId = commentId 
            });
            isLiked = true;
        }
        
        await _unitOfWork.CompleteAsync();
        
        // Fetch updated count
        var count = await _unitOfWork.Repository<CommentLike>().Query()
            .CountAsync(l => l.CommentId == commentId);
            
        return new CommentLikeStatusDto
        {
            IsLiked = isLiked,
            LikeCount = count
        };
    }

    public async Task ToggleCommentLikeAsync(int userId, int commentId)
    {
        var existing = await _unitOfWork.Repository<CommentLike>().FirstOrDefaultAsync(l => l.UserId == userId && l.CommentId == commentId);
        if (existing != null)
        {
            _unitOfWork.Repository<CommentLike>().Remove(existing);
        }
        else
        {
            await _unitOfWork.Repository<CommentLike>().AddAsync(new CommentLike { UserId = userId, CommentId = commentId });
        }
        await _unitOfWork.CompleteAsync();
    }

    public async Task<int> GetCommentLikeCountAsync(int commentId)
    {
        // Optimized: Database level count
        return await _unitOfWork.Repository<CommentLike>().Query()
            .CountAsync(l => l.CommentId == commentId);
    }

    public async Task<bool> IsCommentLikedAsync(int userId, int commentId)
    {
        var l = await _unitOfWork.Repository<CommentLike>().FirstOrDefaultAsync(x => x.CommentId == commentId && x.UserId == userId);
        return l != null;
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

    // Song Interaction logic moved to InteractionService to adhere to SRP

    public async Task<IEnumerable<CommentDto>> GetAllCommentsAsync()
    {
        // Optimized: Single SQL join to avoid N+1 query problem
        var comments = await _unitOfWork.Repository<Comment>().Query()
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Song)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return comments.Select(c => new CommentDto
        {
            CommentId = c.CommentId,
            UserId = c.UserId,
            UserName = c.User?.Username ?? "Unknown",
            SongId = c.SongId,
            SongTitle = c.Song?.Title ?? "Unknown Song",
            Content = c.Content,
            CreatedAt = c.CreatedAt
        }).ToList();
    }
}
