using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardDto> GetStatsAsync(CancellationToken ct = default)
    {
        // Optimized: Database level counts/sums instead of loading full lists into RAM
        var repoUser = _unitOfWork.Repository<User>().Query().AsNoTracking();
        var repoSong = _unitOfWork.Repository<Song>().Query().AsNoTracking();
        var repoPlaylist = _unitOfWork.Repository<Playlist>().Query().AsNoTracking();
        var repoHistory = _unitOfWork.Repository<ListeningHistory>().Query().AsNoTracking();
        var repoReport = _unitOfWork.Repository<Report>().Query().AsNoTracking();
        var repoPayment = _unitOfWork.Repository<Payment>().Query().AsNoTracking();
        var repoSongArtist = _unitOfWork.Repository<SongArtist>().Query().AsNoTracking();

        var now = DateTime.UtcNow;
        var twentyFourHoursAgo = now.AddDays(-1);
        var sevenDaysAgo = now.AddDays(-7).Date;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Execute parallel-unsafe queries sequentially to avoid DbContext concurrency issues
        var totalUsers = await repoUser.CountAsync(ct);
        var newUsers24h = await repoUser.CountAsync(u => u.CreatedAt >= twentyFourHoursAgo, ct);
        var premiumUsers = await repoUser.CountAsync(u => u.IsPremium, ct);
        var totalSongs = await repoSong.CountAsync(s => !s.IsDeleted, ct);
        var totalPlaylists = await repoPlaylist.CountAsync(p => !p.IsDeleted, ct);
        var totalPlays = await repoHistory.CountAsync(ct);
        var pendingReports = await repoReport.CountAsync(r => r.Status == ReportStatus.Pending, ct);
        var totalRevenue = await repoPayment.Where(p => p.Status == PaymentStatus.Success).SumAsync(p => p.Amount, ct);
        var monthlyRevenue = await repoPayment.Where(p => p.Status == PaymentStatus.Success && p.PaymentDate >= startOfMonth).SumAsync(p => p.Amount, ct);

        var recentPayments = await repoPayment
            .Where(p => p.Status == PaymentStatus.Success)
            .OrderByDescending(p => p.PaymentDate)
            .Take(10)
            .Include(p => p.Plan)
            .Select(p => new PaymentDto
            {
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                Status = p.Status,
                OrderCode = p.OrderCode,
                PlanName = p.Plan != null ? p.Plan.Name : "Gói đã xóa"
            })
            .ToListAsync(ct);

        var topSongs = await repoSong
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.PlayCount)
            .Take(10)
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .Select(s => new TopSongDto
            {
                SongId = s.SongId,
                Title = s.Title,
                Artist = s.SongArtists.Select(sa => sa.Artist.Name).FirstOrDefault() ?? "Unknown",
                PlayCount = s.PlayCount
            })
            .ToListAsync(ct);

        var topArtists = await repoSongArtist
            .Include(sa => sa.Song)
            .Include(sa => sa.Artist)
            .GroupBy(sa => new { sa.ArtistId, sa.Artist.Name })
            .Select(g => new TopArtistDto
            {
                Name = g.Key.Name,
                SongCount = g.Count(),
                TotalPlays = g.Sum(sa => (long)sa.Song.PlayCount)
            })
            .OrderByDescending(a => a.TotalPlays)
            .Take(10)
            .ToListAsync(ct);

        var playHistory = await repoHistory
            .Where(h => h.ListenedAt >= sevenDaysAgo)
            .GroupBy(h => h.ListenedAt.Date)
            .Select(g => new DailyPlayCountDto
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(h => h.Date)
            .ToListAsync(ct);

        var registrationHistory = await repoUser
            .Where(u => u.CreatedAt >= sevenDaysAgo)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new DailyPlayCountDto
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(u => u.Date)
            .ToListAsync(ct);

        return new DashboardDto
        {
            TotalUsers = totalUsers,
            NewUsers24h = newUsers24h,
            PremiumUsersCount = premiumUsers,
            TotalSongs = totalSongs,
            TotalPlaylists = totalPlaylists,
            TotalPlays = totalPlays,
            PendingReports = pendingReports,
            TotalRevenue = totalRevenue,
            MonthlyRevenue = monthlyRevenue,
            RecentPayments = recentPayments,
            TopSongs = topSongs,
            TopArtists = topArtists,
            PlayHistory = playHistory,
            RegistrationHistory = registrationHistory
        };
    }

    public async Task<IEnumerable<ReportDto>> GetAllReportsAsync(CancellationToken ct = default)
    {
        // Optimized: Single SQL join for reporting users to avoid N+1
        var reports = await _unitOfWork.Repository<Report>().Query()
            .AsNoTracking()
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return await MapReportsWithNames(reports, ct);
    }

    public async Task<(IEnumerable<ReportDto> Reports, int TotalCount)> GetPaginatedReportsAsync(int page, int pageSize, string? status = null, CancellationToken ct = default)
    {
        IQueryable<Report> query = _unitOfWork.Repository<Report>().Query()
            .AsNoTracking()
            .Include(r => r.User);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        int totalCount = await query.CountAsync(ct);
        var reports = await query.OrderByDescending(r => r.CreatedAt)
                                 .Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToListAsync(ct);

        var mappedReports = (await MapReportsWithNames(reports, ct)).ToList();
        return (mappedReports, totalCount);
    }

    private async Task<IEnumerable<ReportDto>> MapReportsWithNames(List<Report> reports, CancellationToken ct)
    {
        // Optimized: Batch fetch target names to reduce query count
        var songIds = reports.Where(r => r.TargetType == TargetTypes.Song).Select(r => int.Parse(r.TargetId)).Distinct().ToList();
        var playlistIds = reports.Where(r => r.TargetType == TargetTypes.Playlist).Select(r => int.Parse(r.TargetId)).Distinct().ToList();
        var userIds = reports.Where(r => r.TargetType == TargetTypes.User).Select(r => int.Parse(r.TargetId)).Distinct().ToList();

        var songNames = await _unitOfWork.Repository<Song>().Query()
            .Where(s => songIds.Contains(s.SongId)).ToDictionaryAsync(s => s.SongId.ToString(), s => s.Title, ct);
        var playlistNames = await _unitOfWork.Repository<Playlist>().Query()
            .Where(p => playlistIds.Contains(p.PlaylistId)).ToDictionaryAsync(p => p.PlaylistId.ToString(), p => p.Title, ct);
        var userNames = await _unitOfWork.Repository<User>().Query()
            .Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId.ToString(), u => u.Username, ct);

        return reports.Select(r => new ReportDto
        {
            ReportId = r.ReportId,
            UserId = r.UserId,
            UserName = r.User?.Username ?? "Unknown",
            TargetType = r.TargetType,
            TargetId = r.TargetId,
            TargetName = r.TargetType switch {
                TargetTypes.Song => songNames.GetValueOrDefault(r.TargetId, "Deleted Song"),
                TargetTypes.Playlist => playlistNames.GetValueOrDefault(r.TargetId, "Deleted Playlist"),
                TargetTypes.User => userNames.GetValueOrDefault(r.TargetId, "Deleted User"),
                _ => "Unknown [" + r.TargetId + "]"
            },
            Reason = r.Reason,
            Details = r.Details,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ResolvedAt = r.ResolvedAt
        });
    }

    private async Task<string> GetTargetName(string type, string id, CancellationToken ct = default)
    {
        if (!int.TryParse(id, out int intId)) return "ID: " + id;
        
        switch (type)
        {
            case TargetTypes.Song:
                var s = await _unitOfWork.Repository<Song>().GetByIdAsync(intId, ct);
                return s?.Title ?? "Deleted Song";
            case TargetTypes.Playlist:
                var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(intId, ct);
                return p?.Title ?? "Deleted Playlist";
            case TargetTypes.User:
                var u = await _unitOfWork.Repository<User>().GetByIdAsync(intId, ct);
                return u?.Username ?? "Deleted User";
            default:
                return "Unknown";
        }
    }

    public async Task<ReportDto?> GetReportByIdAsync(int id, CancellationToken ct = default)
    {
        var r = await _unitOfWork.Repository<Report>().Query()
            .AsNoTracking()
            .Include(r => r.User)
            .FirstOrDefaultAsync(x => x.ReportId == id, ct);
            
        if (r == null) return null;
        
        var targetName = await GetTargetName(r.TargetType, r.TargetId, ct);

        return new ReportDto
        {
            ReportId = r.ReportId,
            UserId = r.UserId,
            UserName = r.User?.Username ?? "Unknown",
            TargetType = r.TargetType,
            TargetId = r.TargetId,
            TargetName = targetName,
            Reason = r.Reason,
            Details = r.Details,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ResolvedAt = r.ResolvedAt
        };
    }

    public async Task ResolveReportAsync(int reportId, bool takeAction, CancellationToken ct = default)
    {
        var report = await _unitOfWork.Repository<Report>().GetByIdAsync(reportId, ct);
        if (report == null) return;

        if (takeAction)
        {
            if (int.TryParse(report.TargetId, out int intId))
            {
                switch (report.TargetType)
                {
                    case TargetTypes.Song:
                        var s = await _unitOfWork.Repository<Song>().GetByIdAsync(intId, ct);
                        if (s != null) {
                            s.IsDeleted = true; // Optimized: Soft Delete
                            _unitOfWork.Repository<Song>().Update(s);
                        }
                        break;
                    case TargetTypes.Playlist:
                        var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(intId, ct);
                        if (p != null) {
                            p.IsDeleted = true; // Optimized: Soft Delete
                            _unitOfWork.Repository<Playlist>().Update(p);
                        }
                        break;
                    case TargetTypes.User:
                        var u = await _unitOfWork.Repository<User>().GetByIdAsync(intId, ct);
                        if (u != null)
                        {
                            u.IsLocked = true;
                            _unitOfWork.Repository<User>().Update(u);
                        }
                        break;
                }
            }
        }

        report.Status = ReportStatus.Resolved;
        report.ResolvedAt = DateTime.UtcNow;
        _unitOfWork.Repository<Report>().Update(report);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task DismissReportAsync(int reportId, CancellationToken ct = default)
    {
        var report = await _unitOfWork.Repository<Report>().GetByIdAsync(reportId, ct);
        if (report != null)
        {
            report.Status = ReportStatus.Dismissed;
            report.ResolvedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Report>().Update(report);
            await _unitOfWork.CompleteAsync(ct);
        }
    }
}
