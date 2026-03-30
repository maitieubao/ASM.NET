using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        var users = await _unitOfWork.Repository<User>().GetAllAsync();
        var songs = await _unitOfWork.Repository<Song>().GetAllAsync();
        var playlists = await _unitOfWork.Repository<Playlist>().GetAllAsync();
        var history = await _unitOfWork.Repository<ListeningHistory>().GetAllAsync();
        var pendingReports = await _unitOfWork.Repository<Report>().FindAsync(r => r.Status == "Pending");
        
        var allArtists = await _unitOfWork.Repository<Artist>().GetAllAsync();
        var songArtists = await _unitOfWork.Repository<SongArtist>().GetAllAsync();

        // Helper to get artist name
        string GetMainArtistName(int songId) {
            var sa = songArtists.FirstOrDefault(x => x.SongId == songId);
            if (sa == null) return "Unknown";
            var a = allArtists.FirstOrDefault(x => x.ArtistId == sa.ArtistId);
            return a?.Name ?? "Unknown";
        }

        // Top Songs
        var topSongs = songs.OrderByDescending(s => s.PlayCount).Take(10).Select(s => new TopSongDto
        {
            SongId = s.SongId,
            Title = s.Title,
            Artist = GetMainArtistName(s.SongId),
            PlayCount = s.PlayCount
        }).ToList();

        // Top Artists
        var topArtists = songArtists
            .GroupBy(sa => sa.ArtistId)
            .Select(g => {
                var artist = allArtists.FirstOrDefault(a => a.ArtistId == g.Key);
                var artistSongs = g.Select(x => x.SongId).Distinct();
                var plays = songs.Where(s => artistSongs.Contains(s.SongId)).Sum(s => (long)s.PlayCount);
                return new TopArtistDto
                {
                    Name = artist?.Name ?? "Unknown",
                    SongCount = artistSongs.Count(),
                    TotalPlays = plays
                };
            })
            .OrderByDescending(a => a.TotalPlays)
            .Take(10)
            .ToList();

        // Play History (Last 7 days)
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7).Date;
        var playHistory = history.Where(h => h.ListenedAt.Date >= sevenDaysAgo)
            .GroupBy(h => h.ListenedAt.Date)
            .Select(g => new DailyPlayCountDto
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(h => h.Date)
            .ToList();

        return new DashboardDto
        {
            TotalUsers = users.Count(),
            TotalSongs = songs.Count(),
            TotalPlaylists = playlists.Count(),
            TotalPlays = history.Count(),
            PendingReports = pendingReports.Count(),
            TopSongs = topSongs,
            TopArtists = topArtists,
            PlayHistory = playHistory
        };
    }

    public async Task<IEnumerable<ReportDto>> GetAllReportsAsync()
    {
        var reports = await _unitOfWork.Repository<Report>().GetAllAsync();
        var result = new List<ReportDto>();

        foreach (var r in reports)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(r.UserId);
            var targetName = await GetTargetName(r.TargetType, r.TargetId);
            
            result.Add(new ReportDto
            {
                ReportId = r.ReportId,
                UserId = r.UserId,
                UserName = user?.Username ?? "Unknown",
                TargetType = r.TargetType,
                TargetId = r.TargetId,
                TargetName = targetName,
                Reason = r.Reason,
                Details = r.Details,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ResolvedAt = r.ResolvedAt
            });
        }
        return result.OrderByDescending(r => r.CreatedAt);
    }

    private async Task<string> GetTargetName(string type, string id)
    {
        if (!int.TryParse(id, out int intId)) return "ID: " + id;
        
        switch (type)
        {
            case "Song":
                var s = await _unitOfWork.Repository<Song>().GetByIdAsync(intId);
                return s?.Title ?? "Deleted Song";
            case "Playlist":
                var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(intId);
                return p?.Title ?? "Deleted Playlist";
            case "User":
                var u = await _unitOfWork.Repository<User>().GetByIdAsync(intId);
                return u?.Username ?? "Deleted User";
            default:
                return "Unknown";
        }
    }

    public async Task<ReportDto?> GetReportByIdAsync(int id)
    {
        var r = await _unitOfWork.Repository<Report>().GetByIdAsync(id);
        if (r == null) return null;
        
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(r.UserId);
        var targetName = await GetTargetName(r.TargetType, r.TargetId);

        return new ReportDto
        {
            ReportId = r.ReportId,
            UserId = r.UserId,
            UserName = user?.Username ?? "Unknown",
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
                    case "Song":
                        var s = await _unitOfWork.Repository<Song>().GetByIdAsync(intId);
                        if (s != null) _unitOfWork.Repository<Song>().Remove(s);
                        break;
                    case "Playlist":
                        var p = await _unitOfWork.Repository<Playlist>().GetByIdAsync(intId);
                        if (p != null) _unitOfWork.Repository<Playlist>().Remove(p);
                        break;
                    case "User":
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

        report.Status = "Resolved";
        report.ResolvedAt = DateTime.UtcNow;
        _unitOfWork.Repository<Report>().Update(report);
        await _unitOfWork.CompleteAsync();
    }

    public async Task DismissReportAsync(int reportId)
    {
        var report = await _unitOfWork.Repository<Report>().GetByIdAsync(reportId);
        if (report != null)
        {
            report.Status = "Dismissed";
            report.ResolvedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Report>().Update(report);
            await _unitOfWork.CompleteAsync();
        }
    }
}
