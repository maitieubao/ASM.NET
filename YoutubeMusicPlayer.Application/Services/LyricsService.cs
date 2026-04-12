using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Net.Http;

namespace YoutubeMusicPlayer.Application.Services;

public class LyricsService : ILyricsService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly IYoutubeService _youtubeService;

    public LyricsService(HttpClient http, IMemoryCache cache, IYoutubeService youtubeService)
    {
        _http = http;
        _cache = cache;
        _youtubeService = youtubeService;
    }

    public async Task<LyricsResult> GetLyricsAsync(string artist, string title, string? videoId = null)
    {
        var result = new LyricsResult();
        
        if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(videoId))
        {
            result.Status = "ERROR";
            result.ErrorMessage = "Thiếu thông tin tra cứu (nghệ sĩ, tên bài hoặc videoId).";
            return result;
        }

        try
        {
            // 1. Try provided videoId first
            if (!string.IsNullOrEmpty(videoId))
            {
                Console.WriteLine($"[LyricsService] Attempting direct fetch for VideoId: {videoId}");
                var track = await _youtubeService.GetClosedCaptionsAsync(videoId);
                if (track != null && !string.IsNullOrWhiteSpace(track.Text))
                {
                    Console.WriteLine($"[LyricsService] SUCCESS: Found lyrics for {videoId}");
                    return new LyricsResult
                    {
                        Status = "SUCCESS",
                        Lyrics = track.Text,
                        TimedLines = track.Lines,
                        VideoId = videoId,
                        Language = track.Language
                    };
                }
                Console.WriteLine($"[LyricsService] Direct fetch failed for {videoId}, attempting search fallback...");
            }

            // 2. Search Fallback: Search for specifically a version with "lyrics"
            var cleanArtist = CleanInput(artist);
            var cleanTitle = CleanInput(title);
            string searchQuery = $"{cleanArtist} {cleanTitle} lyrics";
            
            Console.WriteLine($"[LyricsService] Searching YouTube for: {searchQuery}");
            var searchResults = await _youtubeService.SearchVideosAsync(searchQuery, limit: 5);
            var fallbackVideoIds = searchResults
                .Where(v => v.YoutubeVideoId != videoId)
                .Select(v => v.YoutubeVideoId)
                .Take(3)
                .ToList();

            if (!fallbackVideoIds.Any())
            {
                Console.WriteLine("[LyricsService] NOT_FOUND: No fallback videos found.");
                result.Status = "NOT_FOUND";
                return result;
            }

            foreach (var fId in fallbackVideoIds)
            {
                Console.WriteLine($"[LyricsService] Attempting fallback fetch for VideoId: {fId}");
                var track = await _youtubeService.GetClosedCaptionsAsync(fId);
                if (track != null && !string.IsNullOrWhiteSpace(track.Text))
                {
                    Console.WriteLine($"[LyricsService] SUCCESS: Found lyrics in fallback {fId}");
                    return new LyricsResult
                    {
                        Status = "SUCCESS",
                        Lyrics = track.Text,
                        TimedLines = track.Lines,
                        VideoId = fId,
                        Language = track.Language
                    };
                }
            }

            Console.WriteLine("[LyricsService] NOT_FOUND: No lyrics found in any target videos.");
            result.Status = "NOT_FOUND";
            return result;
        }
        catch (Exception ex)
        {
            result.Status = "ERROR";
            result.ErrorMessage = ex.Message;
            return result;
        }
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
        cleaned = cleaned.Replace("lyric video", "", StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("audio only", "", StringComparison.OrdinalIgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s?ft\..*?(\s|$)", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s?feat\..*?(\s|$)", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // 3. Trim extra whitespace and double spaces
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s{2,}", " ");
        return cleaned.Trim();
    }
}
