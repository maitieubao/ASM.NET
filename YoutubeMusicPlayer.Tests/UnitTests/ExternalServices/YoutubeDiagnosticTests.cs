using NUnit.Framework;
using YoutubeMusicPlayer.Infrastructure.External;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Tests.UnitTests.ExternalServices;

[TestFixture]
public class YoutubeDiagnosticTests
{
    private YoutubeService _youtubeService;

    [SetUp]
    public void Setup()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var deezerMock = new Mock<IDeezerService>();
        var loggerMock = new Mock<ILogger<YoutubeService>>();
        _youtubeService = new YoutubeService(cache, deezerMock.Object, loggerMock.Object);
    }

    [TestCase("hLQl3WQQoQ0", "Adele Someone Like You")]
    [TestCase("JGwWNGJdvx8", "Ed Sheeran Shape of You")]
    [TestCase("kO_Yls2N6uM", "Son Tung M-TP Lac Troi")]
    public async Task Diagnose_SubtitleExtraction(string videoId, string description)
    {
        Console.WriteLine($"--- ID: {videoId} ({description}) ---");
        
        var captions = await _youtubeService.GetClosedCaptionsAsync(videoId);
        
        if (captions == null || string.IsNullOrEmpty(captions.Text))
        {
            Console.WriteLine("RESULT: FAILED - No captions found.");
            Assert.Fail("Could not extract captions for this video.");
        }
        else
        {
            Console.WriteLine("RESULT: SUCCESS");
            Console.WriteLine($"Language: {captions.Language}");
            Console.WriteLine($"Length: {captions.Text.Length} characters");
            Console.WriteLine($"First 100 chars: {captions.Text.Substring(0, Math.Min(100, captions.Text.Length))}");
            Assert.That(captions.Text.Length, Is.GreaterThan(0));
        }
    }
}
