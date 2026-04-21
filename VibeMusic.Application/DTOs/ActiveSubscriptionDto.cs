using System;

namespace VibeMusic.Application.DTOs;

public class ActiveSubscriptionDto
{
    public string PlanName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsLifetime { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
}
