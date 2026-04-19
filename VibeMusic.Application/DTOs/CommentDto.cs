using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class CommentDto
{
    public int CommentId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatarUrl { get; set; }
    public int SongId { get; set; }
    public string SongTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? ParentCommentId { get; set; }
    public int LikeCount { get; set; }
    public bool IsLiked { get; set; }
    
    public IEnumerable<CommentDto> Replies { get; set; } = new List<CommentDto>();
}
