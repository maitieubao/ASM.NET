using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace YoutubeMusicPlayer.Infrastructure.External;

public class LyricsService : ILyricsService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public LyricsService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<string?> GetLyricsAsync(string artist, string title)
    {
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title)) return null;

        string cacheKey = $"lyrics_{artist.ToLower().Trim()}_{title.ToLower().Trim()}";
        if (_cache.TryGetValue(cacheKey, out string? cachedLyrics)) return cachedLyrics;

        try
        {
            // Using lyrics.ovh as a free, reliable source for basic lyrics
            var url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";
            
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var data = await response.Content.ReadFromJsonAsync<LyricsResponse>();
            
            if (!string.IsNullOrWhiteSpace(data?.Lyrics))
            {
                _cache.Set(cacheKey, data.Lyrics, TimeSpan.FromHours(24));
                return data.Lyrics;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LyricsService] Error: {ex.Message}");
        }

        return null;
    }

    private class LyricsResponse
    {
        public string? Lyrics { get; set; }
    }
}
