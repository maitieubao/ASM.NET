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

        var cleanArtist = CleanInput(artist);
        var cleanTitle = CleanInput(title);

        string cacheKey = $"lyrics_{cleanArtist.ToLower().Trim()}_{cleanTitle.ToLower().Trim()}";
        if (_cache.TryGetValue(cacheKey, out string? cachedLyrics)) return cachedLyrics;

        try
        {
            // Using lyrics.ovh as a free, reliable source for basic lyrics
            var url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(cleanArtist)}/{Uri.EscapeDataString(cleanTitle)}";
            
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

    private string CleanInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // 1. Remove bracketed metadata: (Official Video), [MV], (Lyrics), etc.
        var cleaned = System.Text.RegularExpressions.Regex.Replace(input, @"\s?[\(\[].*?[\)\]]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // 2. Remove common noise
        cleaned = cleaned.Replace(" - Topic", "", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("official video", "", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("official audio", "", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("official music video", "", StringComparison.OrdinalIgnoreCase);
        
        // 3. Trim extra whitespace
        return cleaned.Trim();
    }

    private class LyricsResponse
    {
        public string? Lyrics { get; set; }
    }
}
