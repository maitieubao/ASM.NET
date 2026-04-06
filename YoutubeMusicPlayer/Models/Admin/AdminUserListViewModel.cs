using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Models.Admin;

public class AdminUserListViewModel
{
    public IEnumerable<UserDto> Users { get; set; } = new List<UserDto>();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public string? SearchTerm { get; set; }
}
