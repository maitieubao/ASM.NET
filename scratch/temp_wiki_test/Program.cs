using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IO;

namespace WikiTest;

class Program
{
    static async Task Main(string[] args)
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "WikiTest/1.0 (maitieubao@example.com)");
        
        var wikiService = new MockWikipediaService(httpClient);
        var artists = new[] { "Sơn Tùng M-TP", "Hoàng Thùy Linh", "Taylor Swift", "Imagine Dragons", "Vũ Thanh Vân", "Chi Pu" };
        
        using var writer = new StreamWriter(@"C:\Users\maiti\OneDrive\Desktop\ASM.NET\wiki_test_results.txt");
        await writer.WriteLineAsync($"Wiki Test Results - {DateTime.Now}");
        await writer.WriteLineAsync("========================================");

        foreach (var artist in artists)
        {
            Console.WriteLine($"Testing: {artist}...");
            var bio = await wikiService.GetArtistBioAsync(artist);
            var image = await wikiService.GetArtistImageAsync(artist);
            
            await writer.WriteLineAsync($"Artist: {artist}");
            await writer.WriteLineAsync($"Image: {image ?? "None"}");
            await writer.WriteLineAsync($"Full Bio: {(bio?.Length > 2000 ? bio.Substring(0, 2000) + "..." : bio ?? "FAILED")}");
            await writer.WriteLineAsync("----------------------------------------");
        }

        Console.WriteLine("Test completed. See wiki_test_results.txt");
    }
}

public class MockWikipediaService
{
    private readonly HttpClient _httpClient;
    public MockWikipediaService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string?> GetArtistBioAsync(string artistName)
    {
        var summary = await GetSummaryAsync(artistName);
        return summary?.Extract;
    }

    public async Task<string?> GetArtistImageAsync(string artistName)
    {
        var summary = await GetSummaryAsync(artistName);
        return summary?.ThumbnailUrl;
    }

    private async Task<WikiSummary?> GetSummaryAsync(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName)) return null;
        string baseName = NormalizeArtistName(artistName);
        var languages = new[] { "vi", "en" };
        
        foreach (var lang in languages)
        {
            var searchQueries = new List<string> { baseName };
            if (lang == "vi") searchQueries.Add($"{baseName} (ca sĩ)");
            else searchQueries.Add($"{baseName} (musician)");

            foreach (var query in searchQueries)
            {
                string searchUrl = $"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&origin=*&srlimit=5";
                try
                {
                    var searchResponse = await _httpClient.GetAsync(searchUrl);
                    if (!searchResponse.IsSuccessStatusCode) continue;
                    var searchJson = await searchResponse.Content.ReadAsStringAsync();
                    using var searchDoc = JsonDocument.Parse(searchJson);
                    if (!searchDoc.RootElement.TryGetProperty("query", out var queryElement)) continue;
                    var searchResults = queryElement.GetProperty("search");
                    if (searchResults.GetArrayLength() == 0) continue;

                    string? matchedTitle = null;
                    for (int i = 0; i < searchResults.GetArrayLength(); i++)
                    {
                        var snippet = searchResults[i].GetProperty("snippet").GetString()?.ToLower() ?? "";
                        var title = searchResults[i].GetProperty("title").GetString()!;
                        if (title.Contains("(định hướng)") || title.Contains("(disambiguation)")) continue;
                        if (snippet.Contains("ca sĩ") || snippet.Contains("nghệ sĩ") || snippet.Contains("singer") || snippet.Contains("musician"))
                        {
                            matchedTitle = title;
                            break;
                        }
                    }
                    matchedTitle ??= searchResults[0].GetProperty("title").GetString();
                    if (!string.IsNullOrEmpty(matchedTitle))
                    {
                        var summary = await FetchSummaryByTitleAsync(lang, matchedTitle);
                        if (summary != null && !string.IsNullOrEmpty(summary.Extract)) return summary;
                    }
                } catch { }
            }
        }
        return null;
    }

    private async Task<WikiSummary?> FetchSummaryByTitleAsync(string lang, string title)
    {
        string summaryUrl = $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}";
        try {
            var response = await _httpClient.GetAsync(summaryUrl);
            if (response.IsSuccessStatusCode) {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<WikiSummary>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        } catch { }
        return null;
    }

    private string NormalizeArtistName(string artistName)
    {
        string name = artistName;
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\s?-?\s?Topic$", "");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\[.*?\]|\(.*?\)", "");
        return name.Trim();
    }

    public class WikiSummary
    {
        public string? Title { get; set; }
        public string? Extract { get; set; }
        public string? ThumbnailUrl => Thumbnail?.Source;
        public WikiImage? Thumbnail { get; set; }
    }

    public class WikiImage
    {
        public string? Source { get; set; }
    }
}
