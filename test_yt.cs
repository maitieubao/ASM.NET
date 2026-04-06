using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

class Program
{
    static async Task Main()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");
        
        var youtube = new YoutubeClient(httpClient);
        var videoId = "abPmZCZZrFA"; // Son Tung M-TP
        
        try
        {
            Console.WriteLine($"Testing Video: {videoId}");
            var video = await youtube.Videos.GetAsync(videoId);
            Console.WriteLine($"Title: {video.Title}");
            
            var manifest = await youtube.Videos.Streams.GetManifestAsync(videoId);
            var audioStream = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            
            Console.WriteLine($"SUCCESS! Stream URL: {audioStream.Url.Substring(0, 50)}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            Console.WriteLine($"Stack: {ex.StackTrace}");
        }
    }
}
