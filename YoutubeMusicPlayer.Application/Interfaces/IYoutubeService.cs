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
    public int PopularityScore { get; set; } = 0;
}

public interface IYoutubeService
{
    Task<string> GetAudioStreamUrlAsync(string videoUrl);
    Task<YoutubeVideoDetails> GetVideoDetailsAsync(string videoUrl);
    Task<IEnumerable<YoutubeVideoDetails>> GetChannelVideosAsync(string channelId);
    Task<IEnumerable<YoutubeVideoDetails>> SearchVideosAsync(string query);
    Task<IEnumerable<YoutubeVideoDetails>> GetRelatedVideosAsync(string videoId);
    bool IsMusic(YoutubeVideoDetails details);
}
