using System.Collections.Generic;
using VibeMusic.Application.DTOs;

namespace VibeMusic.Models.Admin;

public class AdminUserListViewModel
{
    public IEnumerable<UserDto> Users { get; set; } = new List<UserDto>();
    public IEnumerable<SubscriptionPlanDto> AvailablePlans { get; set; } = new List<SubscriptionPlanDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public string? SearchTerm { get; set; }
}
