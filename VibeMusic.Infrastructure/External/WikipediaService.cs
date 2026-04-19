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
        string baseName = NormalizeArtistName(artistName);

        // Languages to try in order
        var languages = new[] { "vi", "en" };
        
        foreach (var lang in languages)
        {
            // Stage 1: Try multiple search variations
            var searchQueries = new List<string> { baseName };
            if (lang == "vi") searchQueries.Add($"{baseName} (ca sĩ)");
            else searchQueries.Add($"{baseName} (musician)");
            searchQueries.Add($"{baseName} music");

            foreach (var query in searchQueries)
            {
                string searchUrl = $"https://{lang}.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&format=json&origin=*&srlimit=10";
                
                try
                {
                    var searchResponse = await _httpClient.GetAsync(searchUrl);
                    if (!searchResponse.IsSuccessStatusCode) continue;

                    var searchJson = await searchResponse.Content.ReadAsStringAsync();
                    using var searchDoc = JsonDocument.Parse(searchJson);
                    
                    if (!searchDoc.RootElement.TryGetProperty("query", out var queryElement) || 
                        !queryElement.TryGetProperty("search", out var searchResults) || 
                        searchResults.GetArrayLength() == 0) continue;

                    // Pick the best candidate (first one that isn't a disambiguation page or has music keywords)
                    string? matchedTitle = null;
                    for (int i = 0; i < searchResults.GetArrayLength(); i++)
                    {
                        var snippet = searchResults[i].GetProperty("snippet").GetString()?.ToLower() ?? "";
                        var title = searchResults[i].GetProperty("title").GetString()!;

                        // Skip disambiguation pages
                        if (title.Contains("(định hướng)") || title.Contains("(disambiguation)")) continue;

                        // Priority match if snippet contains music terms
                        if (snippet.Contains("ca sĩ") || snippet.Contains("nghệ sĩ") || snippet.Contains("nhạc") || 
                            snippet.Contains("singer") || snippet.Contains("musician") || snippet.Contains("band") || snippet.Contains("album"))
                        {
                            matchedTitle = title;
                            break;
                        }
                    }

                    // Fallback to the very first result if no "perfect" match found
                    matchedTitle ??= searchResults[0].GetProperty("title").GetString();

                    if (!string.IsNullOrEmpty(matchedTitle))
                    {
                        var summary = await FetchSummaryByTitleAsync(lang, matchedTitle);
                        if (summary != null && IsValidBio(summary.Extract)) return summary;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WikipediaService] Search error for {query} in {lang}: {ex.Message}");
                }
            }
        }

        return null;
    }

    private async Task<WikiSummary?> FetchSummaryByTitleAsync(string lang, string title)
    {
        // 1. Try REST API (Modern, clean JSON)
        string summaryUrl = $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}";
        try {
            var response = await _httpClient.GetAsync(summaryUrl);
            if (response.IsSuccessStatusCode) {
                var json = await response.Content.ReadAsStringAsync();
                var summary = JsonSerializer.Deserialize<WikiSummary>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (summary != null && IsValidBio(summary.Extract)) return summary;
            }
        } catch { /* Fallback to standard Query API */ }

        // 2. Fallback to Standard Query API (More robust for redirects/complex pages)
        string queryUrl = $"https://{lang}.wikipedia.org/w/api.php?action=query&prop=extracts|pageimages&exintro&explaintext&titles={Uri.EscapeDataString(title)}&format=json&origin=*&pithumbsize=1000";
        try {
            var response = await _httpClient.GetAsync(queryUrl);
            if (response.IsSuccessStatusCode) {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
                var page = pages.EnumerateObject().First().Value;

                if (page.TryGetProperty("extract", out var extract) && !string.IsNullOrEmpty(extract.GetString())) {
                    var summary = new WikiSummary {
                        Title = page.GetProperty("title").GetString(),
                        Extract = extract.GetString()
                    };
                    if (page.TryGetProperty("thumbnail", out var thumb)) {
                        summary.Thumbnail = new WikiImage { Source = thumb.GetProperty("source").GetString() };
                    }
                    return summary;
                }
            }
        } catch { }

        return null;
    }

    private bool IsValidBio(string? extract)
    {
        if (string.IsNullOrEmpty(extract)) return false;
        if (extract.Length < 50) return false;
        // Check for disambiguation stubs
        if (extract.Contains("có thể đề cập đến") || extract.Contains("may refer to")) return false;
        return true;
    }

    private string NormalizeArtistName(string artistName)
    {
        if (string.IsNullOrEmpty(artistName)) return "";
        string name = artistName;
        // Remove common YouTube/Streaming noise
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\s?-?\s?Topic$", "");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\s?official.*$", "");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\s?music.*$", "");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\s?vevo.*$", "");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\s?lyrics.*$", "");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"(?i)\[.*?\]|\(.*?\)", ""); // Remove [MV], (Audio)
        return name.Trim();
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
