using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetStatsAsync(CancellationToken ct = default);
    
    // Reports
    Task<IEnumerable<ReportDto>> GetAllReportsAsync(CancellationToken ct = default);
    Task<(IEnumerable<ReportDto> Reports, int TotalCount)> GetPaginatedReportsAsync(int page, int pageSize, string? status = null, CancellationToken ct = default);
    Task<ReportDto?> GetReportByIdAsync(int id, CancellationToken ct = default);
    Task ResolveReportAsync(int reportId, bool takeAction, CancellationToken ct = default); // takeAction: e.g., delete the content
    Task DismissReportAsync(int reportId, CancellationToken ct = default);
}
