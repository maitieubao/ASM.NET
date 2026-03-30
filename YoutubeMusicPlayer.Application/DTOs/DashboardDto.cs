using System;
using System.Collections.Generic;

namespace YoutubeMusicPlayer.Application.DTOs;

public class DashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalSongs { get; set; }
    public int TotalPlaylists { get; set; }
    public long TotalPlays { get; set; }
    public int PendingReports { get; set; }
    
    public IEnumerable<TopSongDto> TopSongs { get; set; } = new List<TopSongDto>();
    public IEnumerable<TopArtistDto> TopArtists { get; set; } = new List<TopArtistDto>();
    public IEnumerable<DailyPlayCountDto> PlayHistory { get; set; } = new List<DailyPlayCountDto>();
}

public class TopSongDto
{
    public int SongId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public long PlayCount { get; set; }
}

public class TopArtistDto
{
    public string Name { get; set; } = string.Empty;
    public int SongCount { get; set; }
    public long TotalPlays { get; set; }
}

public class DailyPlayCountDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}
