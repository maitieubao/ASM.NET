using Moq;
using NUnit.Framework;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YoutubeMusicPlayer.Tests.UnitTests
{
    [TestFixture]
    public class LyricsLogicTests
    {
        private Mock<IYoutubeService> _youtubeServiceMock;
        private Mock<ISongService> _songServiceMock;
        private Mock<IInteractionService> _interactionServiceMock;
        private Mock<ISubscriptionService> _subscriptionServiceMock;
        private Mock<IAuthService> _authServiceMock;
        private Mock<IBackgroundQueue> _backgroundQueueMock;
        private Mock<ILyricsService> _lyricsServiceMock;
        private Mock<ILogger<PlaybackFacade>> _loggerMock;
        private PlaybackFacade _playbackFacade;

        [SetUp]
        public void SetUp()
        {
            _youtubeServiceMock = new Mock<IYoutubeService>();
            _songServiceMock = new Mock<ISongService>();
            _lyricsServiceMock = new Mock<ILyricsService>();
            _interactionServiceMock = new Mock<IInteractionService>();
            _subscriptionServiceMock = new Mock<ISubscriptionService>();
            _authServiceMock = new Mock<IAuthService>();
            _backgroundQueueMock = new Mock<IBackgroundQueue>();
            _loggerMock = new Mock<ILogger<PlaybackFacade>>();
            
            _playbackFacade = new PlaybackFacade(
                _youtubeServiceMock.Object,
                _songServiceMock.Object,
                _lyricsServiceMock.Object,
                _interactionServiceMock.Object,
                _subscriptionServiceMock.Object,
                _authServiceMock.Object,
                _backgroundQueueMock.Object,
                _loggerMock.Object
            );
        }

        [Test]
        public async Task GetRichMetadataAsync_ShouldReturnLyricsFromSongService()
        {
            // Arrange
            var videoId = "test_video_id";
            var mockSong = new SongDto { LyricsText = "Line 1\nLine 2", AuthorBio = "Test Bio" };
            
            _songServiceMock.Setup(x => x.GetOrCreateByYoutubeIdAsync(videoId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockSong);

            // Act
            var result = await _playbackFacade.GetRichMetadataAsync(videoId);

            // Assert
            Assert.That(result.Lyrics, Is.EqualTo(mockSong.LyricsText));
            Assert.That(result.Bio, Is.EqualTo(mockSong.AuthorBio));
        }
    }
}
