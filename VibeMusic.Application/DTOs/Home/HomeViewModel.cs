using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.DTOs;

public class HomeViewModel
{
    public string Greeting { get; set; } = "Chào mừng bạn";
    
    // B. Made For You (Carousel)
    public IEnumerable<YoutubeVideoDetails> DailyMix { get; set; } = new List<YoutubeVideoDetails>();
    
    // A. Recently Played (Grid 2x3)
    public IEnumerable<YoutubeVideoDetails> RecentListened { get; set; } = new List<YoutubeVideoDetails>();
    
    // C. Trending / Top Hits (Ranking List)
    public IEnumerable<SongDto> TopHits { get; set; } = new List<SongDto>();
    
    // D. Genre Explorations (Tiles)
    public IEnumerable<GenreDto> Genres { get; set; } = new List<GenreDto>();
    
    // E. Artists You Follow (Circles)
    public IEnumerable<ArtistDto> FollowedArtists { get; set; } = new List<ArtistDto>();
    
    public IEnumerable<ArtistDto> TopArtists { get; set; } = new List<ArtistDto>();
    
    public MusicSection? ContextualSection { get; set; }
    
    // Other Dynamic Sections
    public List<MusicSection> Sections { get; set; } = new List<MusicSection>();
    
    public IEnumerable<string> RecentSearches { get; set; } = new List<string>();
    
    public IEnumerable<AlbumDto> FeaturedAlbums { get; set; } = new List<AlbumDto>();
}

public class MusicSection
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Layout { get; set; } = "Square"; // "Square", "List3", "Wide"
    public IEnumerable<YoutubeVideoDetails> Songs { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<AlbumDto> Albums { get; set; } = new List<AlbumDto>();
}
