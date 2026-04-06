using System.Linq;
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

    public async Task<DashboardDto> GetStatsAsync()
    {
        // Optimized: Database level counts/sums instead of loading full lists into RAM
        var repoUser = _unitOfWork.Repository<User>().Query().AsNoTracking();
        var repoSong = _unitOfWork.Repository<Song>().Query().AsNoTracking();
        var repoPlaylist = _unitOfWork.Repository<Playlist>().Query().AsNoTracking();
        var repoHistory = _unitOfWork.Repository<ListeningHistory>().Query().AsNoTracking();
        var repoReport = _unitOfWork.Repository<Report>().Query().AsNoTracking();
        var repoPayment = _unitOfWork.Repository<Payment>().Query().AsNoTracking();
        var repoArtist = _unitOfWork.Repository<Artist>().Query().AsNoTracking();
        var repoSongArtist = _unitOfWork.Repository<SongArtist>().Query().AsNoTracking();

        int totalUsers = await repoUser.CountAsync();
        int totalSongs = await repoSong.Where(s => !s.IsDeleted).CountAsync();
        int totalPlaylists = await repoPlaylist.Where(p => !p.IsDeleted).CountAsync();
        int totalPlays = await repoHistory.CountAsync();
        int pendingReportsCount = await repoReport.CountAsync(r => r.Status == ReportStatus.Pending);
        
        decimal totalRevenue = await repoPayment
            .Where(p => p.Status == PaymentStatus.Success)
            .SumAsync(p => p.Amount);
        
        // Optimized: Database side OrderBy and Take for Recent Payments
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
            .ToListAsync();

        // Optimized: Database side ranking for Top Songs
        var topSongs = await repoSong
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.PlayCount)
            .Take(10)
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .Select(s => new TopSongDto
            {
                SongId = s.SongId,
                Title = s.Title,
                Artist = s.SongArtists.Any() ? s.SongArtists.FirstOrDefault().Artist.Name : "Unknown",
                PlayCount = s.PlayCount
            })
            .ToListAsync();

        // Optimized: Complex GroupBy in SQL for Top Artists
        var topArtistsData = await repoSongArtist
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
            .ToListAsync();

        // Optimized: Database side GroupBy for Play History (Last 7 days)
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7).Date;
        var playHistory = await repoHistory
            .Where(h => h.ListenedAt >= sevenDaysAgo)
            .GroupBy(h => h.ListenedAt.Date)
            .Select(g => new DailyPlayCountDto
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(h => h.Date)
            .ToListAsync();

        return new DashboardDto
        {
            TotalUsers = totalUsers,
            TotalSongs = totalSongs,
            TotalPlaylists = totalPlaylists,
            TotalPlays = totalPlays,
            PendingReports = pendingReportsCount,
            TotalRevenue = totalRevenue,
            TopSongs = topSongs,
            TopArtists = topArtistsData,
            PlayHistory = playHistory,
            RecentPayments = recentPayments
        };
    }

    public async Task<IEnumerable<ReportDto>> GetAllReportsAsync()
    {
        // Optimized: Single SQL join for reporting users to avoid N+1
        var reports = await _unitOfWork.Repository<Report>().Query()
            .AsNoTracking()
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Optimized: Batch fetch target names to reduce query count from ~400 to 4
        var songIds = reports.Where(r => r.TargetType == TargetTypes.Song).Select(r => int.Parse(r.TargetId)).Distinct().ToList();
        var playlistIds = reports.Where(r => r.TargetType == TargetTypes.Playlist).Select(r => int.Parse(r.TargetId)).Distinct().ToList();
        var userIds = reports.Where(r => r.TargetType == TargetTypes.User).Select(r => int.Parse(r.TargetId)).Distinct().ToList();

        var songNames = await _unitOfWork.Repository<Song>().Query()
            .Where(s => songIds.Contains(s.SongId)).ToDictionaryAsync(s => s.SongId.ToString(), s => s.Title);
        var playlistNames = await _unitOfWork.Repository<Playlist>().Query()
            .Where(p => playlistIds.Contains(p.PlaylistId)).ToDictionaryAsync(p => p.PlaylistId.ToString(), p => p.Title);
        var userNames = await _unitOfWork.Repository<User>().Query()
            .Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId.ToString(), u => u.Username);

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

    private async Task<string> GetTargetName(string type, string id)
    {
        if (!int.TryParse(id, out int intId)) return "ID: " + id;
        
        switch (type)
        {
            case TargetTypes.Song:
                var s = await _unitOfWork.Repository<Song>().GetByIdAsync(intId);
                return s?.Title ?? "Deleted Song";
            case TargetTypes.Playlist:
                var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(intId);
                return p?.Title ?? "Deleted Playlist";
            case TargetTypes.User:
                var u = await _unitOfWork.Repository<User>().GetByIdAsync(intId);
                return u?.Username ?? "Deleted User";
            default:
                return "Unknown";
        }
    }

    public async Task<ReportDto?> GetReportByIdAsync(int id)
    {
        var r = await _unitOfWork.Repository<Report>().Query()
            .AsNoTracking()
            .Include(r => r.User)
            .FirstOrDefaultAsync(x => x.ReportId == id);
            
        if (r == null) return null;
        
        var targetName = await GetTargetName(r.TargetType, r.TargetId);

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

    public async Task ResolveReportAsync(int reportId, bool takeAction)
    {
        var report = await _unitOfWork.Repository<Report>().GetByIdAsync(reportId);
        if (report == null) return;

        if (takeAction)
        {
            if (int.TryParse(report.TargetId, out int intId))
            {
                switch (report.TargetType)
                {
                    case TargetTypes.Song:
                        var s = await _unitOfWork.Repository<Song>().GetByIdAsync(intId);
                        if (s != null) {
                            s.IsDeleted = true; // Optimized: Soft Delete
                            _unitOfWork.Repository<Song>().Update(s);
                        }
                        break;
                    case TargetTypes.Playlist:
                        var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(intId);
                        if (p != null) {
                            p.IsDeleted = true; // Optimized: Soft Delete
                            _unitOfWork.Repository<Playlist>().Update(p);
                        }
                        break;
                    case TargetTypes.User:
                        var u = await _unitOfWork.Repository<User>().GetByIdAsync(intId);
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
        await _unitOfWork.CompleteAsync();
    }

    public async Task DismissReportAsync(int reportId)
    {
        var report = await _unitOfWork.Repository<Report>().GetByIdAsync(reportId);
        if (report != null)
        {
            report.Status = ReportStatus.Dismissed;
            report.ResolvedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Report>().Update(report);
            await _unitOfWork.CompleteAsync();
        }
    }
}
