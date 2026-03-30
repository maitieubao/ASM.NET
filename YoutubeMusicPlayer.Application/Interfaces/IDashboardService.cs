using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetStatsAsync();
    
    // Reports
    Task<IEnumerable<ReportDto>> GetAllReportsAsync();
    Task<ReportDto?> GetReportByIdAsync(int id);
    Task ResolveReportAsync(int reportId, bool takeAction); // takeAction: e.g., delete the content
    Task DismissReportAsync(int reportId);
}
