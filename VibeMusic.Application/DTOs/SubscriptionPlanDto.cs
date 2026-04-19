using System.ComponentModel.DataAnnotations;

namespace YoutubeMusicPlayer.Application.DTOs;

public class SubscriptionPlanDto
{
    public int PlanId { get; set; }

    [Required(ErrorMessage = "Tên gói không được để trống")]
    public string Name { get; set; } = string.Empty;

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Giá tiền không được âm")]
    public decimal Price { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Thời hạn phải ít nhất 1 ngày")]
    public int DurationDays { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
