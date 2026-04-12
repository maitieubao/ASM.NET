namespace YoutubeMusicPlayer.Application.Interfaces;

/// <summary>
/// Music metadata service interface using Deezer public API.
/// Provides canonical metadata for tracks, artists, and albums.
/// </summary>
public class DeezerTrackInfo
{
    public string DeezerTrackId { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string DeezerArtistId { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public string DeezerAlbumId { get; set; } = string.Empty;
    public string AlbumImageUrl { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int Popularity { get; set; }
    public int DurationMs { get; set; }
    public int TrackNumber { get; set; }
    public bool IsExplicit { get; set; }
}

public class DeezerArtistInfo
{
    public string DeezerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int Popularity { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int Followers { get; set; }
}

public class DeezerAlbumInfo
{
    public string DeezerId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? ReleaseDate { get; set; }
    public string? AlbumType { get; set; } // "album", "single", "compilation"
}

public interface IDeezerService
{
    /// <summary>Search for a track on Deezer by title + artist name.</summary>
    Task<DeezerTrackInfo?> SearchTrackAsync(string title, string artist);

    /// <summary>Get full artist details (genres, followers) by Deezer artist ID.</summary>
    Task<DeezerArtistInfo?> GetArtistInfoAsync(string deezerArtistId);

    /// <summary>Get Deezer's "Related Artists" for cross-recommendation.</summary>
    Task<IEnumerable<DeezerArtistInfo>> GetRelatedArtistsAsync(string deezerArtistId);

    /// <summary>Get the top tracks of an artist for discovery.</summary>
    Task<IEnumerable<DeezerTrackInfo>> GetArtistTopTracksAsync(string deezerArtistId, string market = "VN");

    /// <summary>Search for albums by query (title or artist).</summary>
    Task<IEnumerable<DeezerAlbumInfo>> SearchAlbumsAsync(string query, int limit = 10);
    
    /// <summary>Get canonical tracks in a Deezer album.</summary>
    Task<IEnumerable<DeezerTrackInfo>> GetAlbumTracksAsync(string deezerAlbumId);

    /// <summary>Get globally trending new releases from Deezer.</summary>
    Task<IEnumerable<DeezerAlbumInfo>> GetNewReleasesAsync(int limit = 10);

    /// <summary>Get tracks from a featured playlist (e.g., Vietnam Top Hits).</summary>
    Task<IEnumerable<DeezerTrackInfo>> GetPlaylistTracksAsync(string playlistId, int limit = 12);

    /// <summary>Search for tracks on Deezer by query.</summary>
    Task<IEnumerable<DeezerTrackInfo>> SearchTracksAsync(string query, int limit = 12);

    /// <summary>Get all albums of an artist by Deezer artist ID.</summary>
    Task<IEnumerable<DeezerAlbumInfo>> GetArtistAlbumsAsync(string deezerArtistId, int limit = 20);
}
