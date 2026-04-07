using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Models.Admin;

public class AdminReportListViewModel
{
    public IEnumerable<ReportDto> Reports { get; set; } = new List<ReportDto>();
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public string? Status { get; set; }
}
