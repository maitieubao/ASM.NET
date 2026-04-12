using System.Collections.Generic;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public class ITunesAlbumInfo
{
    public string CollectionId { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string PrimaryGenreName { get; set; } = string.Empty;
}

public class ITunesTrackInfo
{
    public string TrackId { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public int DurationMs { get; set; }
}

public interface IITunesService
{
    /// <summary>Search for albums on iTunes.</summary>
    Task<IEnumerable<ITunesAlbumInfo>> SearchAlbumsAsync(string query, int limit = 10);

    /// <summary>Get tracks for a specific iTunes album collection.</summary>
    Task<IEnumerable<ITunesTrackInfo>> GetAlbumTracksAsync(string collectionId);

    /// <summary>Get basic album metadata by collection ID.</summary>
    Task<ITunesAlbumInfo?> GetAlbumDetailsAsync(string collectionId);
}
