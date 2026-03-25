namespace YoutubeMusicPlayer.Application.DTOs;

public class SubscriptionPlanDto
{
    public int PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public string? Description { get; set; }
}
