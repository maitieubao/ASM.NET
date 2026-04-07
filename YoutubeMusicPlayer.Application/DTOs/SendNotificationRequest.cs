using System.ComponentModel.DataAnnotations;

namespace YoutubeMusicPlayer.Application.DTOs;

public class SendNotificationRequest
{
    [Required(ErrorMessage = "Tiêu đề không được để trống.")]
    [StringLength(100, ErrorMessage = "Tiêu đề không được quá 100 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nội dung không được để trống.")]
    [StringLength(500, ErrorMessage = "Nội dung không được quá 500 ký tự.")]
    public string Message { get; set; } = string.Empty;

    public int? UserId { get; set; } // Null for system-wide notification
}
