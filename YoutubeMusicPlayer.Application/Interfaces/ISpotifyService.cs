namespace YoutubeMusicPlayer.Application.Interfaces;

/// <summary>
/// Music metadata service interface — implemented by DeezerService (no API key required).
/// Named ISpotifyService for historical reasons; data comes from Deezer Public API.
/// </summary>
public class SpotifyTrackInfo
{
    public string SpotifyTrackId { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string SpotifyArtistId { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public string SpotifyAlbumId { get; set; } = string.Empty;
    public string AlbumImageUrl { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int Popularity { get; set; }
    public int DurationMs { get; set; }
    public bool IsExplicit { get; set; }
}

public class SpotifyArtistInfo
{
    public string SpotifyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public int Popularity { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int Followers { get; set; }
}

public class SpotifyAlbumInfo
{
    public string SpotifyId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? ReleaseDate { get; set; }
    public string? AlbumType { get; set; } // "album", "single", "compilation"
}

public interface ISpotifyService
{
    /// <summary>Search for a track on Spotify by title + artist name.</summary>
    Task<SpotifyTrackInfo?> SearchTrackAsync(string title, string artist);

    /// <summary>Get full artist details (genres, followers) by Spotify artist ID.</summary>
    Task<SpotifyArtistInfo?> GetArtistInfoAsync(string spotifyArtistId);

    /// <summary>Get Spotify's "Related Artists" for cross-recommendation.</summary>
    Task<IEnumerable<SpotifyArtistInfo>> GetRelatedArtistsAsync(string spotifyArtistId);

    /// <summary>Get the top tracks of an artist for discovery.</summary>
    Task<IEnumerable<SpotifyTrackInfo>> GetArtistTopTracksAsync(string spotifyArtistId, string market = "VN");

    /// <summary>Search for albums by query (title or artist).</summary>
    Task<IEnumerable<SpotifyAlbumInfo>> SearchAlbumsAsync(string query, int limit = 10);
    
    /// <summary>Get canonical tracks in a Spotify album.</summary>
    Task<IEnumerable<SpotifyTrackInfo>> GetAlbumTracksAsync(string spotifyAlbumId);

    /// <summary>Get globally trending new releases from Spotify.</summary>
    Task<IEnumerable<SpotifyAlbumInfo>> GetNewReleasesAsync(int limit = 10);

    /// <summary>Get tracks from a featured playlist (e.g., Vietnam Top 50).</summary>
    Task<IEnumerable<SpotifyTrackInfo>> GetPlaylistTracksAsync(string playlistId, int limit = 12);

    /// <summary>Search for tracks on Spotify by query.</summary>
    Task<IEnumerable<SpotifyTrackInfo>> SearchTracksAsync(string query, int limit = 12);

    /// <summary>Get all albums of an artist by Spotify artist ID.</summary>
    Task<IEnumerable<SpotifyAlbumInfo>> GetArtistAlbumsAsync(string spotifyArtistId, int limit = 20);
}
