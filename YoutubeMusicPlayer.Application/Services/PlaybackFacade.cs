using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YoutubeMusicPlayer.Application.Services;

public class PlaybackFacade : IPlaybackFacade
{
    private readonly IYoutubeService _youtubeService;
    private readonly ISongService _songService;
    private readonly IInteractionService _interactionService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IAuthService _authService;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly ILogger<PlaybackFacade> _logger;

    public PlaybackFacade(
        IYoutubeService youtubeService,
        ISongService songService,
        IInteractionService interactionService,
        ISubscriptionService subscriptionService,
        IAuthService authService,
        IBackgroundQueue backgroundQueue,
        ILogger<PlaybackFacade> logger)
    {
        _youtubeService = youtubeService;
        _songService = songService;
        _interactionService = interactionService;
        _subscriptionService = subscriptionService;
        _authService = authService;
        _backgroundQueue = backgroundQueue;
        _logger = logger;
    }

    public async Task<PlaybackStreamDto> GetStreamAsync(string videoUrl, string? title, string? artist, int? userId)
    {
        string youtubeId = ExtractYoutubeId(videoUrl);
        if (string.IsNullOrEmpty(youtubeId)) return new PlaybackStreamDto { Error = "InvalidURL", Message = "Đường dẫn không hợp lệ." };

        // Parallel: Stream extraction, Premium check, and DB song retrieval
        var premiumTask = userId.HasValue ? _subscriptionService.IsUserPremiumAsync(userId.Value) : Task.FromResult(false);
        var isPremium = await premiumTask;
        var streamTask = _youtubeService.GetAudioStreamUrlAsync(videoUrl, title, artist, isPremium);
        var songTask = _songService.GetOrCreateByYoutubeIdAsync(youtubeId);

        // Await minimal necessary components
        var song = await songTask;
        var streamUrl = await streamTask;

        var result = new PlaybackStreamDto { StreamUrl = streamUrl, SongId = song?.SongId, ShowAd = !isPremium };

        if (song != null)
        {
            if (song.IsPremiumOnly && !isPremium)
            {
                return new PlaybackStreamDto { Error = "PremiumRequired", Message = "Đây là bài hát dành cho hội viên Premium." };
            }

            if (song.IsExplicit && userId.HasValue)
            {
                var user = await _authService.GetUserByIdAsync(userId.Value);
                if (user?.DateOfBirth.HasValue == true)
                {
                    int age = CalculateAge(user.DateOfBirth.Value);
                    if (age < 18) return new PlaybackStreamDto { Error = "AgeRestricted", Message = "Nội dung này không phù hợp với lứa tuổi của bạn." };
                }
            }

            if (userId.HasValue)
            {
                result.IsLiked = await _interactionService.IsSongLikedAsync(userId.Value, song.SongId);
                
                // Record history in background with error handling
                await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp) =>
                {
                    try {
                        var interactionSvc = sp.GetRequiredService<IInteractionService>();
                        await interactionSvc.RecordListeningHistoryAsync(userId.Value, song.SongId);
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to record listening history in background.");
                    }
                });
            }
        }

        return result;
    }

    public async Task<RichMetadataDto> GetRichMetadataAsync(string videoId)
    {
        var song = await _songService.GetOrCreateByYoutubeIdAsync(videoId);
        if (song != null)
        {
            if (string.IsNullOrEmpty(song.LyricsText) || 
                (song.AuthorBio != null && (song.AuthorBio.Contains("automatically imported") || song.AuthorBio.Contains("đang được cập nhật"))))
            {
                await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp) =>
                {
                    try {
                        var songSvc = sp.GetRequiredService<ISongService>();
                        await songSvc.EnrichSongAsync(song.SongId);
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to enrich song metadata in background.");
                    }
                });
            }
        }

        return new RichMetadataDto
        {
            Lyrics = song?.LyricsText ?? "Lời bài hát hiện chưa khả dụng.",
            Bio = song?.AuthorBio ?? "Thông tin nghệ sĩ đang được cập nhật..."
        };
    }

    private string ExtractYoutubeId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        var regex = new System.Text.RegularExpressions.Regex(@"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var match = regex.Match(url);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private int CalculateAge(DateTime dob)
    {
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dob.Year;
        // Normalize timezones by comparing UTC dates
        if (dob.Date > today.AddYears(-age)) age--;
        return age;
    }
}
