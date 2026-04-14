using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Services;

public class PlaybackFacade : IPlaybackFacade
{
    private readonly IYoutubeService _youtubeService;
    private readonly ISongService _songService;
    private readonly IInteractionService _interactionService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IAuthService _authService;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly ILyricsService _lyricsService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PlaybackFacade> _logger;

    public PlaybackFacade(
        IYoutubeService youtubeService,
        ISongService songService,
        ILyricsService lyricsService,
        IInteractionService interactionService,
        ISubscriptionService subscriptionService,
        IAuthService authService,
        IBackgroundQueue backgroundQueue,
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<PlaybackFacade> logger)
    {
        _youtubeService = youtubeService;
        _songService = songService;
        _lyricsService = lyricsService;
        _interactionService = interactionService;
        _subscriptionService = subscriptionService;
        _authService = authService;
        _backgroundQueue = backgroundQueue;
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PlaybackStreamDto> ResolveAndGetStreamAsync(string query, string? title, string? artist, int? userId)
    {
        _logger.LogInformation("[PlaybackFacade] Resolving external track for query: {Query}", query);

        string cacheKey = $"yt_search_{query.ToLower().Replace(" ", "_")}";
        if (_cache.TryGetValue(cacheKey, out List<YoutubeVideoDetails>? cachedResults) && cachedResults != null)
        {
            _logger.LogInformation("[PlaybackFacade] Cache HIT for search: {Query}", query);
            return await ProcessSearchResultsAsync(cachedResults, title, artist, userId, query);
        }

        // 1. Fetch top 10 results (increase from 5 for better chance of finding a match)
        var results = (await _youtubeService.SearchVideosAsync(query, 10)).ToList();
        
        _cache.Set(cacheKey, results, TimeSpan.FromMinutes(30));

        return await ProcessSearchResultsAsync(results, title, artist, userId, query);
    }

    private async Task<PlaybackStreamDto> ProcessSearchResultsAsync(List<YoutubeVideoDetails> results, string? title, string? artist, int? userId, string query)
    {
        YoutubeVideoDetails? bestMatch = null;
        
        if (!string.IsNullOrEmpty(title))
        {
            _logger.LogInformation("[PlaybackFacade] Verifying results for: {Title} by {Artist}", title, artist ?? "Unknown");
            
            foreach (var v in results)
            {
                // Check 1: Direct title similarity match (Lower threshold for resilience)
                if (_youtubeService.IsTooSimilar(v.Title, title, 0.45))
                {
                    _logger.LogInformation("[PlaybackFacade] Found match by similarity (threshold 0.45): {YtTitle}", v.Title);
                    bestMatch = v;
                    break;
                }
                
                // Check 2: Combined Artist + Title match
                if (!string.IsNullOrEmpty(artist) && _youtubeService.IsTooSimilar(v.Title, $"{artist} {title}", 0.55))
                {
                    _logger.LogInformation("[PlaybackFacade] Found match by artist+title similarity: {YtTitle}", v.Title);
                    bestMatch = v;
                    break;
                }
            }
        }
        
        // 2. Fuzzy fallback: If no similarity match, check if first keyword of title and artist exist in the search result title
        if (bestMatch == null && !string.IsNullOrEmpty(title))
        {
            var firstTitleWord = title.Split(' ').FirstOrDefault()?.ToLower();
            var firstArtistWord = artist?.Split(' ').FirstOrDefault()?.ToLower();

            bestMatch = results.FirstOrDefault(v => 
                v.Title.ToLower().Contains(firstTitleWord ?? "---") && 
                (string.IsNullOrEmpty(firstArtistWord) || v.Title.ToLower().Contains(firstArtistWord)));
            
            if (bestMatch != null) _logger.LogInformation("[PlaybackFacade] Found match by fuzzy keyword check: {YtTitle}", bestMatch.Title);
        }

        // 3. Absolute Fallback: Use the very first search result if it exists
        bestMatch ??= results.FirstOrDefault();
        
        if (bestMatch == null)
        {
            _logger.LogWarning("[PlaybackFacade] Exhausted all search results for: {Query}", query);
            return new PlaybackStreamDto { Error = "NotFound", Message = "Không tìm thấy bài hát này trên YouTube." };
        }

        _logger.LogInformation("[PlaybackFacade] Selected best match for playback: {Title} ({Id})", bestMatch.Title, bestMatch.YoutubeVideoId);

        var streamResult = await GetStreamAsync(bestMatch.YoutubeVideoId, title ?? bestMatch.Title, artist ?? bestMatch.AuthorName, userId);
        streamResult.VideoId = bestMatch.YoutubeVideoId;
        return streamResult;
    }

    public async Task<PlaybackStreamDto> GetStreamAsync(string videoUrl, string? title, string? artist, int? userId)
    {
        string youtubeId = ExtractYoutubeId(videoUrl);
        if (string.IsNullOrEmpty(youtubeId)) return new PlaybackStreamDto { Error = "InvalidURL", Message = "Đường dẫn không hợp lệ." };

        string cacheKey = $"yt_stream_{youtubeId}";
        if (_cache.TryGetValue(cacheKey, out string? cachedStreamUrl) && !string.IsNullOrEmpty(cachedStreamUrl))
        {
            _logger.LogInformation("[PlaybackFacade] Cache HIT for stream: {VideoId}", youtubeId);
            return await AssembleStreamDtoAsync(youtubeId, cachedStreamUrl, title, artist, userId);
        }

        // Parallelize DB calls and YouTube stream resolution to hit <1s playback target
        // Each DB-hitting task MUST use its own scope to avoid DbContext concurrency issues
        var isPremiumTask = Task.Run(async () => {
             if (!userId.HasValue) return false;
             using var scope = _scopeFactory.CreateScope();
             var subSvc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
             return await subSvc.IsUserPremiumAsync(userId.Value);
        });

        var songTask = Task.Run(async () => {
             using var scope = _scopeFactory.CreateScope();
             var songSvc = scope.ServiceProvider.GetRequiredService<ISongService>();
             return await songSvc.GetOrCreateByYoutubeIdAsync(youtubeId);
        });
        
        var streamUrlTask = _youtubeService.GetAudioStreamUrlAsync(videoUrl, title, artist, false);

        await Task.WhenAll(isPremiumTask, songTask, streamUrlTask);

        var isPremium = await isPremiumTask;
        var song = await songTask;
        var streamUrl = await streamUrlTask;

        if (!string.IsNullOrEmpty(streamUrl))
        {
            _cache.Set(cacheKey, streamUrl, TimeSpan.FromHours(2));
        }

        return await AssembleStreamDtoAsync(youtubeId, streamUrl, title, artist, userId, isPremium, song);
    }

    private async Task<PlaybackStreamDto> AssembleStreamDtoAsync(
        string youtubeId, 
        string streamUrl, 
        string? title, 
        string? artist, 
        int? userId, 
        bool? isPremiumInput = null, 
        SongDto? songInput = null)
    {
        bool isPremium = isPremiumInput ?? false;
        SongDto? song = songInput;

        // If we came from cache, we still need the Song object and Premium status
        if (isPremiumInput == null || songInput == null)
        {
            var isPremiumTask = Task.Run(async () => {
                 if (!userId.HasValue) return false;
                 using var scope = _scopeFactory.CreateScope();
                 var subSvc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                 return await subSvc.IsUserPremiumAsync(userId.Value);
            });

            var songTask = Task.Run(async () => {
                 using var scope = _scopeFactory.CreateScope();
                 var songSvc = scope.ServiceProvider.GetRequiredService<ISongService>();
                 return await songSvc.GetOrCreateByYoutubeIdAsync(youtubeId);
            });

            await Task.WhenAll(isPremiumTask, songTask);
            isPremium = await isPremiumTask;
            song = await songTask;
        }

        var result = new PlaybackStreamDto 
        { 
            StreamUrl = streamUrl, 
            VideoId = youtubeId, 
            SongId = song?.SongId, 
            ShowAd = !isPremium,
            Title = song?.Title ?? title,
            Author = song?.AuthorName ?? artist,
            ThumbnailUrl = song?.ThumbnailUrl
        };

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
                await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, ct) =>
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

    public async Task<RichMetadataDto> GetRichMetadataAsync(string videoId, string? lang = null)
    {
        var metadataTask = _songService.GetLyricsAndBioAsync(videoId);
        var captionsTask = _youtubeService.GetAvailableCaptionTracksAsync(videoId);

        await Task.WhenAll(metadataTask, captionsTask);

        var metadata = await metadataTask;
        var availableCaptions = await captionsTask;

        var lyricsRaw = metadata.Lyrics;
        string? plainLyrics = lyricsRaw;
        List<TimedLyricLine>? timedLyrics = null;

        // Detect and parse JSON format
        if (!string.IsNullOrEmpty(lyricsRaw) && (lyricsRaw.Trim().StartsWith("[") || lyricsRaw.Trim().StartsWith("{")))
        {
            try {
                timedLyrics = JsonSerializer.Deserialize<List<TimedLyricLine>>(lyricsRaw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (timedLyrics != null && timedLyrics.Any()) {
                    plainLyrics = string.Join("\n", timedLyrics.Select(l => l.Text));
                }
            } catch {
                _logger.LogWarning("[PlaybackFacade] Failed to parse timed lyrics JSON for {VideoId}", videoId);
            }
        }

        bool isAlreadyTimed = timedLyrics != null && timedLyrics.Any();

        // REFINED LOGIC: If we don't have timed lyrics OR we specifically want a different language OR current lyrics are non-JSON plain text,
        // prioritizing YouTube captions as the most reliable source for synchronization.
        if (!isAlreadyTimed || !string.IsNullOrEmpty(lang))
        {
            _logger.LogInformation("[PlaybackFacade] Checking YouTube Captions for {VideoId} (Language: {Lang}, AlreadyTimed: {IsTimed})", 
                videoId, lang ?? "default", isAlreadyTimed);
            
            var lyricsResult = await _youtubeService.GetClosedCaptionsAsync(videoId, lang);
            
            if (lyricsResult != null && !string.IsNullOrEmpty(lyricsResult.Text))
            {
                // If YouTube provided better (timed) data, or if we had nothing, use it.
                if (lyricsResult.Lines != null && lyricsResult.Lines.Any())
                {
                    _logger.LogInformation("[PlaybackFacade] Successfully retrieved {LineCount} timed lines from YouTube for {VideoId}", lyricsResult.Lines.Count, videoId);
                    plainLyrics = lyricsResult.Text;
                    timedLyrics = lyricsResult.Lines;
                }
                else if (string.IsNullOrEmpty(plainLyrics))
                {
                    _logger.LogInformation("[PlaybackFacade] YouTube provided plain lyrics (no timing) for {VideoId}", videoId);
                    plainLyrics = lyricsResult.Text;
                }

                // Background save to DB only if it's the default language (null lang) and we got timed lyrics
                if (string.IsNullOrEmpty(lang) && timedLyrics != null && timedLyrics.Any())
                {
                    await _backgroundQueue.QueueBackgroundWorkItemAsync(async (sp, ct) => {
                        try {
                            var uow = sp.GetRequiredService<IUnitOfWork>();
                            var songRepo = uow.Repository<Song>();
                            var s = await songRepo.Query().FirstOrDefaultAsync(x => x.YoutubeVideoId == videoId);
                            if (s != null) {
                                s.LyricsText = JsonSerializer.Serialize(timedLyrics);
                                songRepo.Update(s);
                                await uow.CompleteAsync();
                                _logger.LogInformation("[PlaybackFacade] Cached timed lyrics to DB for {VideoId}", videoId);
                            }
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Failed to cache timed lyrics to DB for {VideoId}", videoId);
                        }
                    });
                }
            }
        }

        bool hasTimedLyrics = timedLyrics != null && timedLyrics.Any();

        return new RichMetadataDto
        {
            Lyrics = plainLyrics ?? string.Empty,
            TimedLyrics = timedLyrics,
            Bio = metadata.Bio ?? "Thông tin nghệ sĩ đang được cập nhật...",
            Status = string.IsNullOrEmpty(plainLyrics) ? "NOT_FOUND" : "SUCCESS",
            LyricsType = hasTimedLyrics ? "TIMED" : "PLAIN",
            AvailableCaptions = availableCaptions
        };
    }

    private string ExtractYoutubeId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        
        // If it's already an 11-character ID (no slashes, no dots, just alphanumeric/dashes/underscores)
        if (url.Length == 11 && !url.Contains("/") && !url.Contains("."))
        {
            return url;
        }

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
