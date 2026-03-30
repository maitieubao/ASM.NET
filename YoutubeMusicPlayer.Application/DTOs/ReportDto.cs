using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class ReportDto
{
    public int ReportId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty; // Title of song, playlist or user name
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
