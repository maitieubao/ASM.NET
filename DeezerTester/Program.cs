using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

Console.WriteLine("--- Deezer API Standalone Tester ---");

using var client = new HttpClient();

try {
    // 1. Test Track Search
    string trackQuery = "Shape of You";
    string artistQuery = "Ed Sheeran";
    Console.WriteLine($"\n[1] Searching for track: {trackQuery} by {artistQuery}...");
    
    var searchResponse = await client.GetAsync($"https://api.deezer.com/search?q=track:\"{trackQuery}\" artist:\"{artistQuery}\"");
    var searchContent = await searchResponse.Content.ReadAsStringAsync();
    
    if (searchResponse.IsSuccessStatusCode) {
        var doc = JsonDocument.Parse(searchContent);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() > 0) {
            var track = data[0];
            string artistId = track.GetProperty("artist").GetProperty("id").GetInt64().ToString();
            string albumTitle = track.GetProperty("album").GetProperty("title").GetString()!;
            
            Console.WriteLine($"SUCCESS: Found '{track.GetProperty("title").GetString()}' by {track.GetProperty("artist").GetProperty("name").GetString()}");
            Console.WriteLine($" Album: {albumTitle}");
            Console.WriteLine($" Deezer Artist ID: {artistId}");

            // 2. Test Artist Albums
            Console.WriteLine($"\n[2] Fetching albums for artist ID: {artistId}...");
            var albumsResponse = await client.GetAsync($"https://api.deezer.com/artist/{artistId}/albums");
            var albumsContent = await albumsResponse.Content.ReadAsStringAsync();
            
            if (albumsResponse.IsSuccessStatusCode) {
                var albumsDoc = JsonDocument.Parse(albumsContent);
                var albumsData = albumsDoc.RootElement.GetProperty("data");
                Console.WriteLine($"SUCCESS: Found {albumsData.GetArrayLength()} albums.");
                foreach (var album in albumsData.EnumerateArray().Take(5)) {
                    Console.WriteLine($" - {album.GetProperty("title").GetString()} ({album.GetProperty("release_date").GetString()})");
                }
            } else {
                Console.WriteLine("FAILED to catch albums.");
            }
            
            // 3. Test Top Tracks of Artist
            Console.WriteLine($"\n[3] Fetching top tracks for artist ID: {artistId}...");
            var topResponse = await client.GetAsync($"https://api.deezer.com/artist/{artistId}/top");
            var topContent = await topResponse.Content.ReadAsStringAsync();
            if (topResponse.IsSuccessStatusCode) {
                var topDoc = JsonDocument.Parse(topContent);
                var topData = topDoc.RootElement.GetProperty("data");
                Console.WriteLine($"SUCCESS: Found {topData.GetArrayLength()} top tracks.");
                foreach (var t in topData.EnumerateArray().Take(3)) {
                    Console.WriteLine($" - {t.GetProperty("title").GetString()}");
                }
            }

            // 4. Test Global Charts (Trending)
            Console.WriteLine("\n[4] Fetching Global Charts (Trending)...");
            var chartResponse = await client.GetAsync("https://api.deezer.com/chart");
            var chartContent = await chartResponse.Content.ReadAsStringAsync();
            if (chartResponse.IsSuccessStatusCode) {
                var chartDoc = JsonDocument.Parse(chartContent);
                var tracks = chartDoc.RootElement.GetProperty("tracks").GetProperty("data");
                var albums = chartDoc.RootElement.GetProperty("albums").GetProperty("data");
                Console.WriteLine($"SUCCESS: Found {tracks.GetArrayLength()} trending tracks and {albums.GetArrayLength()} trending albums.");
                foreach (var a in albums.EnumerateArray().Take(3)) {
                    Console.WriteLine($" Album: {a.GetProperty("title").GetString()}");
                }
            }
        } else {
            Console.WriteLine("NOT FOUND: Search returned no results.");
        }
    } else {
        Console.WriteLine($"ERROR: {searchResponse.StatusCode}");
    }

    // 5. Test New Releases
    Console.WriteLine("\n[5] Fetching New Releases...");
    var releasesResponse = await client.GetAsync("https://api.deezer.com/editorial/0/releases?limit=50");
    var releasesContent = await releasesResponse.Content.ReadAsStringAsync();
    if (releasesResponse.IsSuccessStatusCode) {
        var releasesDoc = JsonDocument.Parse(releasesContent);
        var resData = releasesDoc.RootElement.GetProperty("data");
        Console.WriteLine($"SUCCESS: Found {resData.GetArrayLength()} new releases.");
        foreach (var r in resData.EnumerateArray().Take(5)) {
            Console.WriteLine($" Album: {r.GetProperty("title").GetString()} by {r.GetProperty("artist").GetProperty("name").GetString()}");
        }
    } else {
        Console.WriteLine($"FAILED: {releasesResponse.StatusCode}");
    }

} catch (Exception ex) {
    Console.WriteLine($"FATAL: {ex.Message}");
}

Console.WriteLine("\n--- Deezer Test Finished ---");
