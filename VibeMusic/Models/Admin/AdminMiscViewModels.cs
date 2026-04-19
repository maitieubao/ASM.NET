using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Models.Admin;

public class AdminTaxonomyViewModel
{
    public IEnumerable<GenreDto> Genres { get; set; } = new List<GenreDto>();
    public IEnumerable<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
}

public class AdminSupportViewModel
{
    public IEnumerable<ReportDto>? Reports { get; set; }
    public IEnumerable<NotificationDto>? Notifications { get; set; }
    public IEnumerable<SubscriptionPlanDto>? SubscriptionPlans { get; set; }
}
