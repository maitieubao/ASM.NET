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

/// <summary>
/// Deezer Public API — no API key required.
/// Provides music metadata (tracks, artists, albums) previously named SpotifyService.
/// </summary>
public class DeezerService : IDeezerService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private const string BaseUrl = "https://api.deezer.com";

    // Real Deezer editorial playlist IDs (verified working 2024-2026)
    private const long VietnamChartId    = 1362528705L; // Vietnam Top Hits
    private const long GlobalChartId     = 3155776842L; // Deezer Global Top 100
    private const long VPopChartId       = 1282977921L; // V-Pop Hot
    private const long KpopChartId       = 1313622735L; // K-Pop Global
    private const long RemixChartId      = 1267701615L; // EDM / Electronic
    private const int  DeezerVNGenreId   = 122;         // Genre: V-Pop / Nhạc Trẻ

    public DeezerService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<DeezerTrackInfo?> SearchTrackAsync(string title, string artist)
    {
        string query = string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
        string cacheKey = $"deezer_track_{query}".ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out DeezerTrackInfo? cached)) return cached;

        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/search?q={Uri.EscapeDataString(query)}&limit=5");
            if (doc == null) return null;

            var data = doc.Value.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;

            var titleTokens = Tokenize(title);
            JsonElement best = data[0];
            int bestScore = -1;
            foreach (var item in data.EnumerateArray())
            {
                var t = item.GetProperty("title").GetString() ?? string.Empty;
                int score = Tokenize(t).Intersect(titleTokens, StringComparer.OrdinalIgnoreCase).Count();
                if (score > bestScore) { bestScore = score; best = item; }
            }

            var info = MapTrack(best);
            _cache.Set(cacheKey, info, TimeSpan.FromHours(24));
            return info;
        }
        catch { return null; }
    }

    public async Task<DeezerArtistInfo?> GetArtistInfoAsync(string artistId)
    {
        if (string.IsNullOrWhiteSpace(artistId)) return null;
        string cacheKey = $"deezer_artist_{artistId}";
        if (_cache.TryGetValue(cacheKey, out DeezerArtistInfo? cached)) return cached;

        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/artist/{artistId}");
            if (doc == null) return null;
            var item = doc.Value;

            var info = new DeezerArtistInfo
            {
                DeezerId    = SafeGetId(item),
                Name        = item.TryGetProperty("name",       out var n)  ? (n.GetString()  ?? string.Empty) : string.Empty,
                ImageUrl    = item.TryGetProperty("picture_xl", out var px) ? (px.GetString() ?? string.Empty) : string.Empty,
                Followers   = item.TryGetProperty("nb_fan",    out var f)  ? f.GetInt32() : 0,
                Genres      = new List<string>()
            };

            try
            {
                var topDoc = await GetJsonAsync($"{BaseUrl}/artist/{artistId}/top?limit=1");
                if (topDoc != null)
                {
                    var topData = topDoc.Value.GetProperty("data");
                    if (topData.GetArrayLength() > 0)
                    {
                        var track = topData[0];
                        if (track.TryGetProperty("album", out var alb) && alb.TryGetProperty("id", out var albId))
                        {
                            var albumDoc = await GetJsonAsync($"{BaseUrl}/album/{albId.GetInt64()}");
                            if (albumDoc != null && albumDoc.Value.TryGetProperty("genres", out var genresNode)
                                && genresNode.TryGetProperty("data", out var genreData))
                            {
                                info.Genres = genreData.EnumerateArray()
                                    .Where(g => g.TryGetProperty("name", out var gn) && gn.ValueKind != JsonValueKind.Null)
                                    .Select(g => g.GetProperty("name").GetString() ?? string.Empty)
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .ToList();
                            }
                        }
                    }
                }
            } catch { }

            _cache.Set(cacheKey, info, TimeSpan.FromHours(6));
            return info;
        }
        catch { return null; }
    }

    public async Task<IEnumerable<DeezerArtistInfo>> GetRelatedArtistsAsync(string artistId)
    {
        string cacheKey = $"deezer_related_{artistId}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<DeezerArtistInfo>? cached)) return cached!;

        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/artist/{artistId}/related?limit=10");
            if (doc == null) return Enumerable.Empty<DeezerArtistInfo>();

            var result = doc.Value.GetProperty("data").EnumerateArray().Select(item => new DeezerArtistInfo
            {
                DeezerId  = SafeGetId(item),
                Name      = item.TryGetProperty("name",       out var n)  ? (n.GetString()  ?? string.Empty) : string.Empty,
                ImageUrl  = item.TryGetProperty("picture_xl", out var px) ? (px.GetString() ?? string.Empty) : string.Empty,
                Followers = item.TryGetProperty("nb_fan",    out var f)  ? f.GetInt32() : 0
            }).ToList();

            _cache.Set(cacheKey, result, TimeSpan.FromHours(6));
            return result;
        }
        catch { return Enumerable.Empty<DeezerArtistInfo>(); }
    }

    public async Task<IEnumerable<DeezerTrackInfo>> GetArtistTopTracksAsync(string artistId, string market = "VN")
    {
        string cacheKey = $"deezer_artist_top_{artistId}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<DeezerTrackInfo>? cached)) return cached!;

        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/artist/{artistId}/top?limit=10");
            if (doc == null) return Enumerable.Empty<DeezerTrackInfo>();

            var result = doc.Value.GetProperty("data").EnumerateArray()
                .Select(MapTrack)
                .Where(t => !string.IsNullOrEmpty(t.TrackName))
                .ToList();

            _cache.Set(cacheKey, result, TimeSpan.FromHours(3));
            return result;
        }
        catch { return Enumerable.Empty<DeezerTrackInfo>(); }
    }

    public async Task<IEnumerable<DeezerAlbumInfo>> GetArtistAlbumsAsync(string artistId, int limit = 20)
    {
        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/artist/{artistId}/albums?limit={limit}");
            if (doc == null) return Enumerable.Empty<DeezerAlbumInfo>();

            if (!doc.Value.TryGetProperty("data", out var data)) return Enumerable.Empty<DeezerAlbumInfo>();
            return data.EnumerateArray().Select(MapAlbum)
                .Where(a => !string.IsNullOrEmpty(a.Title) && a.CoverImageUrl != null)
                .ToList();
        }
        catch { return Enumerable.Empty<DeezerAlbumInfo>(); }
    }

    public async Task<IEnumerable<DeezerTrackInfo>> GetAlbumTracksAsync(string albumId)
    {
        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/album/{albumId}/tracks");
            if (doc == null) return Enumerable.Empty<DeezerTrackInfo>();

            if (!doc.Value.TryGetProperty("data", out var data)) return Enumerable.Empty<DeezerTrackInfo>();
            return data.EnumerateArray()
                .Select(MapTrack)
                .Where(t => !string.IsNullOrEmpty(t.TrackName))
                .ToList();
        }
        catch { return Enumerable.Empty<DeezerTrackInfo>(); }
    }

    public async Task<IEnumerable<DeezerAlbumInfo>> SearchAlbumsAsync(string query, int limit = 10)
    {
        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/search/album?q={Uri.EscapeDataString(query)}&limit={limit}");
            if (doc == null) return Enumerable.Empty<DeezerAlbumInfo>();

            return doc.Value.GetProperty("data").EnumerateArray()
                .Select(MapAlbum)
                .Where(a => !string.IsNullOrEmpty(a.Title) && !string.IsNullOrEmpty(a.ArtistName))
                .ToList();
        }
        catch { return Enumerable.Empty<DeezerAlbumInfo>(); }
    }

    public async Task<IEnumerable<DeezerAlbumInfo>> GetNewReleasesAsync(int limit = 10)
    {
        const string cacheKey = "deezer_new_releases";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<DeezerAlbumInfo>? cached)) return cached!;

        var results = new List<DeezerAlbumInfo>();
        try
        {
            var vnGenre = await GetJsonAsync($"{BaseUrl}/genre/{DeezerVNGenreId}/artists?limit=20");
            if (vnGenre != null)
            {
                foreach (var artist in vnGenre.Value.GetProperty("data").EnumerateArray().Take(6))
                {
                    try {
                        var albums = await GetArtistAlbumsAsync(SafeGetId(artist), 3);
                        results.AddRange(albums.Take(2));
                    } catch { }
                }
            }

            var editorial = await GetJsonAsync($"{BaseUrl}/editorial/0/releases?limit=20");
            if (editorial != null && editorial.Value.TryGetProperty("data", out var editData))
            {
                results.AddRange(editData.EnumerateArray().Select(MapAlbum));
            }

            if (results.Count < limit)
            {
                var chartTracks = await GetPlaylistTracksAsync(VietnamChartId.ToString(), 20);
                var chartAlbums = chartTracks
                    .Where(t => !string.IsNullOrEmpty(t.DeezerAlbumId))
                    .GroupBy(t => t.DeezerAlbumId)
                    .Select(g => new DeezerAlbumInfo
                    {
                        DeezerId      = g.Key,
                        Title         = g.First().AlbumName,
                        ArtistName    = g.First().ArtistName,
                        CoverImageUrl  = g.First().AlbumImageUrl,
                        AlbumType     = "album"
                    })
                    .Where(a => !string.IsNullOrEmpty(a.Title));
                results.AddRange(chartAlbums);
            }

            var final = results
                .Where(a => !string.IsNullOrEmpty(a.Title) && !string.IsNullOrEmpty(a.ArtistName) && !string.IsNullOrEmpty(a.CoverImageUrl))
                .GroupBy(a => $"{a.Title.ToLowerInvariant()}|{a.ArtistName.ToLowerInvariant()}")
                .Select(g => g.First())
                .Take(limit)
                .ToList();

            if (final.Any()) {
                _cache.Set(cacheKey, (IEnumerable<DeezerAlbumInfo>)final, TimeSpan.FromMinutes(30));
                return final;
            }
        } catch { }

        var fallback = (await SearchAlbumsAsync("nhạc mới nhất", limit)).ToList();
        return fallback;
    }

    public async Task<IEnumerable<DeezerTrackInfo>> GetPlaylistTracksAsync(string playlistId, int limit = 12)
    {
        string cacheKey = $"deezer_playlist_{playlistId}_{limit}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<DeezerTrackInfo>? cached)) return cached!;

        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/playlist/{playlistId}/tracks?limit={limit}");
            if (doc == null) return Enumerable.Empty<DeezerTrackInfo>();

            if (!doc.Value.TryGetProperty("data", out var data)) return Enumerable.Empty<DeezerTrackInfo>();
            
            var result = data.EnumerateArray()
                .Select(MapTrack)
                .Where(t => !string.IsNullOrEmpty(t.TrackName) && !string.IsNullOrEmpty(t.ArtistName))
                .ToList();

            if (result.Any()) {
                _cache.Set(cacheKey, (IEnumerable<DeezerTrackInfo>)result, TimeSpan.FromMinutes(30));
            }
            return result;
        }
        catch { return Enumerable.Empty<DeezerTrackInfo>(); }
    }

    public async Task<IEnumerable<DeezerTrackInfo>> SearchTracksAsync(string query, int limit = 12)
    {
        string cacheKey = $"deezer_search_{query}_{limit}".ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out IEnumerable<DeezerTrackInfo>? cached)) return cached!;

        try
        {
            var doc = await GetJsonAsync($"{BaseUrl}/search?q={Uri.EscapeDataString(query)}&limit={limit}");
            if (doc == null) return Enumerable.Empty<DeezerTrackInfo>();

            if (!doc.Value.TryGetProperty("data", out var data)) return Enumerable.Empty<DeezerTrackInfo>();

            var result = data.EnumerateArray()
                .Select(MapTrack)
                .Where(t => !string.IsNullOrEmpty(t.TrackName) && !string.IsNullOrEmpty(t.ArtistName))
                .ToList();

            _cache.Set(cacheKey, (IEnumerable<DeezerTrackInfo>)result, TimeSpan.FromHours(1));
            return result;
        }
        catch { return Enumerable.Empty<DeezerTrackInfo>(); }
    }

    private async Task<JsonElement?> GetJsonAsync(string url)
    {
        try {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        } catch { return null; }
    }

    private static string SafeGetId(JsonElement item)
    {
        if (item.TryGetProperty("id", out var idProp)) {
            if (idProp.ValueKind == JsonValueKind.Number) return idProp.GetInt64().ToString();
            if (idProp.ValueKind == JsonValueKind.String) return idProp.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static DeezerTrackInfo MapTrack(JsonElement item)
    {
        string imageUrl = string.Empty;
        if (item.TryGetProperty("album", out var album)) {
            if (album.TryGetProperty("cover_xl",  out var cxl) && cxl.ValueKind != JsonValueKind.Null) imageUrl = cxl.GetString() ?? string.Empty;
            if (string.IsNullOrEmpty(imageUrl) && album.TryGetProperty("cover_big", out var cb) && cb.ValueKind != JsonValueKind.Null) imageUrl = cb.GetString() ?? string.Empty;
        }
        if (string.IsNullOrEmpty(imageUrl) && item.TryGetProperty("md5_image", out var md5) && md5.ValueKind != JsonValueKind.Null)
            imageUrl = $"https://e-cdns-images.dzcdn.net/images/cover/{md5.GetString()}/1000x1000-000000-80-0-0.jpg";

        string albumName = string.Empty;
        string albumId   = string.Empty;
        if (item.TryGetProperty("album", out var alb2)) {
            albumName = alb2.TryGetProperty("title", out var at) ? (at.GetString() ?? string.Empty) : string.Empty;
            albumId   = alb2.TryGetProperty("id",    out var ai) ? ai.GetInt64().ToString() : string.Empty;
        }

        int rank = 0;
        if (item.TryGetProperty("rank", out var rankProp) && rankProp.ValueKind == JsonValueKind.Number)
            rank = (int)Math.Min(100, rankProp.GetInt64() / 10000);

        bool isExplicit = false;
        if (item.TryGetProperty("explicit_lyrics", out var exp)) {
            if (exp.ValueKind == JsonValueKind.True || exp.ValueKind == JsonValueKind.False) isExplicit = exp.GetBoolean();
            else if (exp.ValueKind == JsonValueKind.Number) isExplicit = exp.GetInt32() == 1;
        }

        return new DeezerTrackInfo {
            DeezerTrackId = SafeGetId(item),
            TrackName     = item.TryGetProperty("title",           out var tt) ? (tt.GetString()  ?? string.Empty) : string.Empty,
            ArtistName    = item.TryGetProperty("artist",          out var ar) && ar.TryGetProperty("name", out var an) ? (an.GetString() ?? string.Empty) : string.Empty,
            DeezerArtistId = item.TryGetProperty("artist",         out var ar2) ? SafeGetId(ar2) : string.Empty,
            AlbumName     = albumName,
            DeezerAlbumId = albumId,
            AlbumImageUrl = imageUrl,
            DurationMs    = item.TryGetProperty("duration",        out var dur) && dur.ValueKind == JsonValueKind.Number ? dur.GetInt32() * 1000 : 0,
            IsExplicit    = isExplicit,
            Popularity    = rank
        };
    }

    private static DeezerAlbumInfo MapAlbum(JsonElement item)
    {
        string imageUrl = string.Empty;
        if (item.TryGetProperty("cover_xl",  out var cxl) && cxl.ValueKind != JsonValueKind.Null) imageUrl = cxl.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(imageUrl) && item.TryGetProperty("cover_big", out var cb) && cb.ValueKind != JsonValueKind.Null) imageUrl = cb.GetString() ?? string.Empty;

        string artistName = "Unknown";
        if (item.TryGetProperty("artist", out var artistNode))
            artistName = artistNode.TryGetProperty("name", out var an) ? (an.GetString() ?? "Unknown") : "Unknown";

        return new DeezerAlbumInfo {
            DeezerId     = SafeGetId(item),
            Title        = item.TryGetProperty("title",        out var t) ? (t.GetString()  ?? string.Empty) : string.Empty,
            ArtistName   = artistName,
            CoverImageUrl = !string.IsNullOrEmpty(imageUrl) ? imageUrl : null,
            ReleaseDate  = item.TryGetProperty("release_date", out var rd) ? rd.GetString() : null,
            AlbumType    = item.TryGetProperty("record_type",  out var rt) ? (rt.GetString() ?? "album") : "album"
        };
    }

    private static IEnumerable<string> Tokenize(string input) =>
        input.ToLowerInvariant().Split(new[] { ' ', '-', '_', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries).Where(s => s.Length > 1);
}
