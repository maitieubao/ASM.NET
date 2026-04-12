using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External;

public class ITunesService : IITunesService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private const string BaseUrl = "https://itunes.apple.com";

    public ITunesService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<IEnumerable<ITunesAlbumInfo>> SearchAlbumsAsync(string query, int limit = 10)
    {
        string cacheKey = $"itunes_search_albums_{query}_{limit}".ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out IEnumerable<ITunesAlbumInfo>? cached)) return cached!;

        try
        {
            string url = $"{BaseUrl}/search?term={Uri.EscapeDataString(query)}&media=music&entity=album&limit={limit}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<ITunesAlbumInfo>();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("results", out var results)) return Enumerable.Empty<ITunesAlbumInfo>();

            var albums = results.EnumerateArray().Select(MapAlbum).ToList();
            _cache.Set(cacheKey, albums, TimeSpan.FromHours(1));
            return albums;
        }
        catch { return Enumerable.Empty<ITunesAlbumInfo>(); }
    }

    public async Task<IEnumerable<ITunesTrackInfo>> GetAlbumTracksAsync(string collectionId)
    {
        string cacheKey = $"itunes_album_tracks_{collectionId}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<ITunesTrackInfo>? cached)) return cached!;

        try
        {
            string url = $"{BaseUrl}/lookup?id={collectionId}&entity=song";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<ITunesTrackInfo>();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("results", out var results)) return Enumerable.Empty<ITunesTrackInfo>();

            // The first result is the album metadata, subsequent are tracks
            var tracks = results.EnumerateArray()
                .Where(x => x.TryGetProperty("wrapperType", out var type) && type.GetString() == "track")
                .Select(MapTrack)
                .OrderBy(t => t.TrackNumber)
                .ToList();

            _cache.Set(cacheKey, tracks, TimeSpan.FromHours(1));
            return tracks;
        }
        catch { return Enumerable.Empty<ITunesTrackInfo>(); }
    }

    public async Task<ITunesAlbumInfo?> GetAlbumDetailsAsync(string collectionId)
    {
        string cacheKey = $"itunes_album_details_{collectionId}";
        if (_cache.TryGetValue(cacheKey, out ITunesAlbumInfo? cached)) return cached;

        try
        {
            string url = $"{BaseUrl}/lookup?id={collectionId}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (!json.TryGetProperty("results", out var results)) return null;

            var first = results.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Undefined) return null;

            var album = MapAlbum(first);
            _cache.Set(cacheKey, album, TimeSpan.FromHours(24));
            return album;
        }
        catch { return null; }
    }

    private static ITunesAlbumInfo MapAlbum(JsonElement item)
    {
        return new ITunesAlbumInfo
        {
            CollectionId = item.TryGetProperty("collectionId", out var id) ? id.GetRawText() : string.Empty,
            CollectionName = item.TryGetProperty("collectionName", out var cn) ? (cn.GetString() ?? string.Empty) : string.Empty,
            ArtistName = item.TryGetProperty("artistName", out var an) ? (an.GetString() ?? string.Empty) : string.Empty,
            ArtworkUrl = item.TryGetProperty("artworkUrl100", out var art) ? (art.GetString()?.Replace("100x100", "1000x1000") ?? string.Empty) : string.Empty,
            ReleaseDate = item.TryGetProperty("releaseDate", out var rd) ? (rd.GetString() ?? string.Empty) : string.Empty,
            PrimaryGenreName = item.TryGetProperty("primaryGenreName", out var pg) ? (pg.GetString() ?? string.Empty) : string.Empty
        };
    }

    private static ITunesTrackInfo MapTrack(JsonElement item)
    {
        return new ITunesTrackInfo
        {
            TrackId = item.TryGetProperty("trackId", out var id) ? id.GetRawText() : string.Empty,
            TrackName = item.TryGetProperty("trackName", out var tn) ? (tn.GetString() ?? string.Empty) : string.Empty,
            ArtistName = item.TryGetProperty("artistName", out var an) ? (an.GetString() ?? string.Empty) : string.Empty,
            CollectionName = item.TryGetProperty("collectionName", out var cn) ? (cn.GetString() ?? string.Empty) : string.Empty,
            TrackNumber = item.TryGetProperty("trackNumber", out var tnum) ? tnum.GetInt32() : 0,
            DurationMs = item.TryGetProperty("trackTimeMillis", out var dur) ? dur.GetInt32() : 0
        };
    }
}
