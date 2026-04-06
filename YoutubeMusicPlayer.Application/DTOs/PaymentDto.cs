using System;

namespace YoutubeMusicPlayer.Application.DTOs;

public class PaymentDto
{
    public int PaymentId { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public long OrderCode { get; set; }
}
