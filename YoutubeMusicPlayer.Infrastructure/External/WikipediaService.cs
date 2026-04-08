using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External;

public class WikipediaService : IWikipediaService
{
    private readonly HttpClient _httpClient;

    public WikipediaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // User-Agent is required by Wikipedia API
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "YoutubeMusicPlayer/1.0 (maitieubao@example.com)");
        }
    }

    public async Task<string?> GetArtistBioAsync(string artistName)
    {
        var summary = await GetSummaryAsync(artistName);
        return summary?.Extract;
    }

    public async Task<string?> GetArtistImageAsync(string artistName)
    {
        var summary = await GetSummaryAsync(artistName);
        return summary?.ThumbnailUrl ?? summary?.OriginalImageUrl;
    }

    public async Task<string?> GetWikipediaUrlAsync(string artistName)
    {
        var summary = await GetSummaryAsync(artistName);
        return summary?.ContentUrls?.Desktop?.Page;
    }

    private async Task<WikiSummary?> GetSummaryAsync(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName)) return null;
        string searchName = NormalizeArtistName(artistName);

        // Languages to try in order
        var languages = new[] { "vi", "en" };
        
        foreach (var lang in languages)
        {
            // Wikipedia Summary API: https://en.wikipedia.org/api/rest_v1/page/summary/{title}
            // We search for the page first to get the correct title (handling redirects and disambiguation)
            string searchUrl = $"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchName)}&format=json&origin=*";
            
            try
            {
                var searchResponse = await _httpClient.GetAsync(searchUrl);
                if (!searchResponse.IsSuccessStatusCode) continue;

                var searchJson = await searchResponse.Content.ReadAsStringAsync();
                using var searchDoc = JsonDocument.Parse(searchJson);
                var searchResults = searchDoc.RootElement.GetProperty("query").GetProperty("search");

                if (searchResults.GetArrayLength() > 0)
                {
                    string matchedTitle = searchResults[0].GetProperty("title").GetString()!;
                    
                    string summaryUrl = $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(matchedTitle)}";
                    var summaryResponse = await _httpClient.GetAsync(summaryUrl);
                    
                    if (summaryResponse.IsSuccessStatusCode)
                    {
                        var summaryJson = await summaryResponse.Content.ReadAsStringAsync();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var summary = JsonSerializer.Deserialize<WikiSummary>(summaryJson, options);
                        
                        if (summary != null && !string.IsNullOrEmpty(summary.Extract))
                        {
                            return summary;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WikipediaService] Error fetching for {artistName} in {lang}: {ex.Message}");
            }
        }

        return null;
    }

    private string NormalizeArtistName(string artistName)
    {
        string searchName = artistName;
        if (searchName.EndsWith(" - Topic")) searchName = searchName.Replace(" - Topic", "");
        if (searchName.EndsWith("- Topic")) searchName = searchName.Replace("- Topic", "");
        return searchName.Trim();
    }

    private class WikiSummary
    {
        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string? Title { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("displaytitle")]
        public string? DisplayTitle { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("extract")]
        public string? Extract { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("extract_html")]
        public string? ExtractHtml { get; set; }

        public string? ThumbnailUrl => Thumbnail?.Source;
        public string? OriginalImageUrl => OriginalImage?.Source;

        [System.Text.Json.Serialization.JsonPropertyName("thumbnail")]
        public WikiImage? Thumbnail { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("originalimage")]
        public WikiImage? OriginalImage { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("content_urls")]
        public WikiContentUrls? ContentUrls { get; set; }
    }

    private class WikiImage
    {
        [System.Text.Json.Serialization.JsonPropertyName("source")]
        public string? Source { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private class WikiContentUrls
    {
        [System.Text.Json.Serialization.JsonPropertyName("desktop")]
        public WikiUrlSet? Desktop { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("mobile")]
        public WikiUrlSet? Mobile { get; set; }
    }

    private class WikiUrlSet
    {
        [System.Text.Json.Serialization.JsonPropertyName("page")]
        public string? Page { get; set; }
    }
}
