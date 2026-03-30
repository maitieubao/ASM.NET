using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

Console.WriteLine("--- Spotify Raw HTTP Debugger ---");

string clientId = "b64ff67e12304c5481aa94a38d226ea0";
string clientSecret = "50bd5165be3b43e3b0414cf4b0764e18";

using var client = new HttpClient();

try {
    // 1. Get Token
    Console.WriteLine("\n[1] Requesting Token...");
    var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
    var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
    tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    tokenRequest.Content = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("grant_type", "client_credentials")
    });

    var tokenResponse = await client.SendAsync(tokenRequest);
    var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"Token Status: {tokenResponse.StatusCode}");
    
    if (!tokenResponse.IsSuccessStatusCode) {
        Console.WriteLine($"Token Error Content: {tokenContent}");
        return;
    }

    var tokenDoc = JsonDocument.Parse(tokenContent);
    string accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;
    Console.WriteLine("Token obtained successfully.");

    // 2. Try Search (Raw)
    Console.WriteLine("\n[2] Testing Raw Search (Ed Sheeran)...");
    var searchRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/search?q=Ed+Sheeran&type=track&limit=1");
    searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    
    var searchResponse = await client.SendAsync(searchRequest);
    var searchContent = await searchResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"Search Status: {searchResponse.StatusCode}");
    Console.WriteLine($"Search Content: {searchContent}");

    // 3. Try Artist Info (Raw)
    Console.WriteLine("\n[3] Testing Raw Artist Get (Ed Sheeran)...");
    var artistRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/artists/6eWpP8J7JbeKst0ZfQ9D7T");
    artistRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    
    var artistResponse = await client.SendAsync(artistRequest);
    var artistContent = await artistResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"Artist Status: {artistResponse.StatusCode}");
    Console.WriteLine($"Artist Content: {artistContent}");

} catch (Exception ex) {
    Console.WriteLine($"FATAL ERROR: {ex.Message}");
}
