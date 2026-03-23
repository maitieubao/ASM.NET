using System;
using System.Linq;
using System.Threading.Tasks;
using WikiDotNet;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Infrastructure.External;

public class WikipediaService : IWikipediaService
{
    private readonly WikiSearcher _wikiSearcher = new WikiSearcher();

    public async Task<string?> GetArtistBioAsync(string artistName)
    {
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

    private string NormalizeArtistName(string artistName)
    {
        string searchName = artistName;
        if (searchName.EndsWith(" - Topic")) searchName = searchName.Replace(" - Topic", "");
        if (searchName.EndsWith("- Topic")) searchName = searchName.Replace("- Topic", "");
        return searchName.Trim();
    }
}

