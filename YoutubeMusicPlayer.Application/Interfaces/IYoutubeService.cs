using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public class YoutubeVideoDetails
{
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorChannelId { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string YoutubeVideoId { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public TimeSpan? Duration { get; set; }
    public string Genre { get; set; } = string.Empty;
    public List<string> Hashtags { get; set; } = new List<string>();
    
    // Data Enrichment Properties
    public List<string> Tags { get; set; } = new List<string>(); // Fake AI Tags
    public string TrackType { get; set; } = "Official"; // Official, Remix, Live, Cover, Lyrics, Mix
    public string CleanedTitle { get; set; } = string.Empty;
    public string CleanedArtist { get; set; } = string.Empty;
    public long ViewCount { get; set; } = 0;
    public long LikeCount { get; set; } = 0;
    public bool IsPersonalized { get; set; } = false; // Flag for special user-matched songs
    public string? Lyrics { get; set; }
    public string? ArtistBio { get; set; }
    public string? SectionTitle { get; set; } // Used for "Because you listened to..." dynamic text
}

public interface IYoutubeService
{
    Task<string> GetAudioStreamUrlAsync(string videoUrl, string? title = null, string? artist = null, bool isPremium = false);
    Task<YoutubeVideoDetails> GetVideoDetailsAsync(string videoUrl);
    Task<IEnumerable<YoutubeVideoDetails>> GetChannelVideosAsync(string channelId);
    Task<IEnumerable<YoutubeVideoDetails>> SearchVideosAsync(string query, int limit = 30, bool searchCompilations = false);
    Task<IEnumerable<YoutubeAlbumDetails>> SearchPlaylistsAsync(string query, int limit = 5);
    Task<IEnumerable<YoutubeVideoDetails>> GetPlaylistVideosAsync(string playlistId);
    Task<IEnumerable<YoutubeVideoDetails>> GetTrendingMusicAsync(int limit = 15, bool forceRefresh = false);
    bool IsMusic(YoutubeVideoDetails details);
    bool IsCompilation(YoutubeVideoDetails details);
    bool IsKaraoke(YoutubeVideoDetails details);
}
