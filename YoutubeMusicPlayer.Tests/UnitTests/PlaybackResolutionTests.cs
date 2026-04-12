using NUnit.Framework;
using YoutubeMusicPlayer.Application.Services;
using Moq;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace YoutubeMusicPlayer.Tests.UnitTests
{
    [TestFixture]
    public class PlaybackResolutionTests
    {
        private PlaybackFacade _facade;

        [SetUp]
        public void Setup()
        {
            var youtubeMock = new Mock<IYoutubeService>();
            var songMock = new Mock<ISongService>();
            var lyricsMock = new Mock<ILyricsService>();
            var interactionMock = new Mock<IInteractionService>();
            var subscriptionMock = new Mock<ISubscriptionService>();
            var authMock = new Mock<IAuthService>();
            var queueMock = new Mock<IBackgroundQueue>();
            var loggerMock = new Mock<ILogger<PlaybackFacade>>();

            _facade = new PlaybackFacade(
                youtubeMock.Object,
                songMock.Object,
                lyricsMock.Object,
                interactionMock.Object,
                subscriptionMock.Object,
                authMock.Object,
                queueMock.Object,
                loggerMock.Object
            );
        }

        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [TestCase("invalid_url_too_long_but_not_id_format", "")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void ExtractYoutubeId_ShouldHandleVariousFormats(string? input, string expected)
        {
            // Use reflection to access private method for unit testing
            var method = typeof(PlaybackFacade).GetMethod("ExtractYoutubeId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (string)method.Invoke(_facade, new object[] { input });

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
