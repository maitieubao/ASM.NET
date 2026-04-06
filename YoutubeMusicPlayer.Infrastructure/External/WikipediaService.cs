using System;
using System.Linq;
using System.Threading.Tasks;
using WikiDotNet;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External;

public class WikipediaService : IWikipediaService
{
    private readonly WikiSearcher _wikiSearcher = new WikiSearcher();
    private readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();

    public WikipediaService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "YoutubeMusicPlayer/1.0 (maiti@example.com)");
    }

    public async Task<string?> GetArtistBioAsync(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName)) return null;
        string searchName = NormalizeArtistName(artistName);
        
        // Strategy: Try multiple variations to find the artist
        var queries = new[] { 
            searchName, 
            $"{searchName} singer", 
            $"{searchName} musical artist",
            $"{searchName} band"
        };

        foreach (var query in queries)
        {
            // Try English first
            var bio = await Task.Run(() => FetchBio(query, "en"));
            if (!string.IsNullOrWhiteSpace(bio)) return bio;

            // Then try Vietnamese
            bio = await Task.Run(() => FetchBio(query, "vi"));
            if (!string.IsNullOrWhiteSpace(bio)) return bio;
        }

        return null;
    }

    private string? FetchBio(string artistName, string language)
    {
        try
        {
            var settings = new WikiSearchSettings
            {
                Language = language,
                ResultLimit = 2 // Check top 2 to find a better match
            };

            var response = _wikiSearcher.Search(artistName, settings);

            if (response?.Query?.SearchResults != null && response.Query.SearchResults.Any())
            {
                // Find the best match that actually has a preview
                var results = response.Query.SearchResults
                    .Where(r => !string.IsNullOrEmpty(r.Preview) && r.Preview.Length > 100);

                var bestMatch = results.FirstOrDefault() ?? response.Query.SearchResults.First();
                var bio = bestMatch.Preview;

                if (!string.IsNullOrWhiteSpace(bio) && bio.Length > 50)
                {
                    // Clean up HTML tags if any (Wiki.Net usually returns text, but just in case)
                    bio = System.Text.RegularExpressions.Regex.Replace(bio, "<.*?>", string.Empty);
                    return bio;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wiki.Net Error for {artistName} ({language}): {ex.Message}");
        }

        return null;
    }

    public async Task<string?> GetArtistImageAsync(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName)) return null;
        string searchName = NormalizeArtistName(artistName);

        try
        {
            // API: https://en.wikipedia.org/w/api.php?action=query&prop=pageimages&format=json&piprop=original&titles=ArtistName
            string url = $"https://en.wikipedia.org/w/api.php?action=query&prop=pageimages&format=json&piprop=original&titles={Uri.EscapeDataString(searchName)}&origin=*";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var pages = doc.RootElement.GetProperty("query").GetProperty("pages");
                
                foreach (var page in pages.EnumerateObject())
                {
                    if (page.Value.TryGetProperty("original", out var original))
                    {
                        return original.GetProperty("source").GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Wiki Image Fetch Error for {artistName}: {ex.Message}");
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
}

