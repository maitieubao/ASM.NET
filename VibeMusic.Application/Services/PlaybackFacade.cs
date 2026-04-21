using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Threading.Tasks;

namespace VibeMusic.Application.Services;

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

    public async Task<PlaybackStreamDto> ResolveAndGetStreamAsync(string query, string? title, string? artist, int? userId, int? durationMs = null)
    {
        _logger.LogInformation("[PlaybackFacade] Resolving external track for query: {Query}", query);

        string cacheKey = $"yt_search_{query.ToLower().Replace(" ", "_")}";
        if (_cache.TryGetValue(cacheKey, out List<YoutubeVideoDetails>? cachedResults) && cachedResults != null)
        {
            _logger.LogInformation("[PlaybackFacade] Cache HIT for search: {Query}", query);
            return await ProcessSearchResultsAsync(cachedResults, title, artist, userId, query, durationMs);
        }

        // 1. Fetch top 10 results (increase from 5 for better chance of finding a match)
        var results = (await _youtubeService.SearchVideosAsync(query, 10)).ToList();
        
        _cache.Set(cacheKey, results, TimeSpan.FromMinutes(30));

        return await ProcessSearchResultsAsync(results, title, artist, userId, query, durationMs);
    }

    private async Task<PlaybackStreamDto> ProcessSearchResultsAsync(List<YoutubeVideoDetails> results, string? title, string? artist, int? userId, string query, int? durationMs = null)
    {
        YoutubeVideoDetails? bestMatch = null;
        double highestBaseScore = -1;
        
        if (!string.IsNullOrEmpty(title))
        {
            _logger.LogInformation("[PlaybackFacade] Verifying results for: {Title} by {Artist} (Expected Duration: {Duration}ms)", 
                title, artist ?? "Unknown", durationMs ?? 0);
            
            // Required Keywords (The heart of accuracy)
            // Filter out common small words to find "unique" identifiers
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "the", "a", "an", "of", "in", "on", "at", "to", "by", "for", "with", "and", "or", "is", "it" };
            var titleTokens = title.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                  .Where(t => t.Length > 2 && !stopWords.Contains(t))
                                  .ToList();
            
            foreach (var v in results)
            {
                double currentScore = 0;
                string vTitle = v.Title.ToLower();
                
                // 1. Keyword check (MUST HAVE at least 1-2 important title keywords if title is long)
                bool hasKeyword = !titleTokens.Any() || titleTokens.Any(t => vTitle.Contains(t));
                if (!hasKeyword) continue; 
                
                // 2. Similarity Score
                double similarity = 0;
                if (_youtubeService.IsTooSimilar(v.Title, title, 0.45)) similarity = 0.5;
                if (_youtubeService.IsTooSimilar(v.Title, $"{artist} {title}", 0.55)) similarity = 0.8;
                
                currentScore += similarity * 1000;

                // 3. Duration penalty/bonus (Critical for Opalite/intro issues)
                if (durationMs > 0 && v.Duration.HasValue)
                {
                    double diffSec = Math.Abs(v.Duration.Value.TotalMilliseconds - durationMs.Value) / 1000.0;
                    if (diffSec < 10) currentScore += 500; // Perfect length
                    else if (diffSec < 20) currentScore += 200;
                    else if (diffSec > 45) currentScore -= 800; // Huge intro or mashup detected
                }
                
                // 4. Artist matching bonus
                if (!string.IsNullOrEmpty(artist) && v.AuthorName.ToLower().Contains(artist.ToLower()))
                {
                    currentScore += 300;
                }

                if (currentScore > highestBaseScore && (similarity > 0.3 || hasKeyword))
                {
                    highestBaseScore = currentScore;
                    bestMatch = v;
                }
            }
        }
        
        // Final gate: If bestMatch is still too low in similarity or far in duration, we yield.
        if (bestMatch != null && !string.IsNullOrEmpty(title) && highestBaseScore < 300)
        {
             _logger.LogWarning("[PlaybackFacade] Best match for '{Title}' rejected due to low score ({Score})", title, highestBaseScore);
             bestMatch = null; 
        }

        // Final last-resort fallback ONLY if we are confident (or if no title provided)
        if (bestMatch == null && string.IsNullOrEmpty(title))
        {
            bestMatch = results.FirstOrDefault();
        }
        
        if (bestMatch == null)
        {
            _logger.LogWarning("[PlaybackFacade] Exhausted all search results for: {Query}", query);
            return new PlaybackStreamDto { Error = "NotFound", Message = "Không tìm thấy bài hát này trên YouTube." };
        }

        _logger.LogInformation("[PlaybackFacade] Selected best match for playback: {Title} ({Id}) - Score: {Score}", 
            bestMatch.Title, bestMatch.YoutubeVideoId, highestBaseScore);

        var streamResult = await GetStreamAsync(bestMatch.YoutubeVideoId, title ?? bestMatch.Title, artist ?? bestMatch.AuthorName, userId, durationMs);
        streamResult.VideoId = bestMatch.YoutubeVideoId;
        return streamResult;
    }

    public async Task<PlaybackStreamDto> GetStreamAsync(string videoUrl, string? title, string? artist, int? userId, int? durationMs = null)
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
        // CACHING METADATA & PREMIUM STATUS (To avoid 1.3s DB queries)
        var isPremiumTask = Task.Run(async () => {
             if (!userId.HasValue) return false;
             string userCacheKey = $"user_premium_{userId.Value}";
             if (_cache.TryGetValue(userCacheKey, out bool isPrem)) return isPrem;

             using var scope = _scopeFactory.CreateScope();
             var subSvc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
             var result = await subSvc.IsUserPremiumAsync(userId.Value);
             
             _cache.Set(userCacheKey, result, TimeSpan.FromMinutes(10));
             return result;
        });

        var songTask = Task.Run(async () => {
             string songCacheKey = $"song_meta_{youtubeId}";
             if (_cache.TryGetValue(songCacheKey, out SongDto? cachedSong)) return cachedSong;

             using var scope = _scopeFactory.CreateScope();
             var songSvc = scope.ServiceProvider.GetRequiredService<ISongService>();
             // IMPORTANT: Use CancellationToken.None to prevent ObjectDisposedException 
             // when the request times out or is cancelled, but we still want to finish importing.
             var result = await songSvc.GetOrCreateByYoutubeIdAsync(youtubeId, CancellationToken.None);
             
             if (result != null)
                 _cache.Set(songCacheKey, result, TimeSpan.FromMinutes(10));
             
             return result;
        });
        
        var streamUrlTask = _youtubeService.GetAudioStreamUrlAsync(videoUrl, title, artist, false);

        // We only wait for DB/Premium check for a short duration (Optimize for speed)
        // If it takes longer than 1500ms, we proceed with the stream URL alone
        var dbTimeoutTask = Task.Delay(1500); 
        var metadataTask = Task.WhenAll(isPremiumTask, songTask);

        // Level 3 Ultra-Priority: Try to resolve SongId from YouTube mapping cache first
        string mappingCacheKey = $"yt_to_songid_{youtubeId}";
        if (_cache.TryGetValue(mappingCacheKey, out SongDto? mappedSong) && mappedSong != null) {
            _logger.LogInformation("[ULTRA-FAST] Cache HIT for YoutubeId -> SongId mapping: {VideoId}", youtubeId);
            songTask = Task.FromResult<SongDto?>(mappedSong);
        }

        // ALWAYS wait for the stream URL as it's mandatory
        var streamUrl = await streamUrlTask;

        // Optimistically wait for metadata, but don't block more than a fraction of a second
        var firstFinished = await Task.WhenAny(metadataTask, dbTimeoutTask);
        
        bool isPremium = false;
        SongDto? song = null;

        if (metadataTask.IsCompleted)
        {
            isPremium = await isPremiumTask;
            song = await songTask;
        }
        else
        {
            _logger.LogWarning("[PLAYBACK-FACADE] Metadata/DB task timed out after 400ms. Proceeding with optimistic playback for: {VideoId}", youtubeId);
            // We don't await them here, but they will continue to run in the background
            // to ensure the song is eventually imported and history is recorded.
            
            // Fire and forget (safely) to ensure DB tasks finish eventually
            _ = metadataTask.ContinueWith(t => {
                if (t.IsFaulted) _logger.LogError(t.Exception, "Background metadata task failed");
            });
        }

        if (!string.IsNullOrEmpty(streamUrl))
        {
            _cache.Set(cacheKey, streamUrl, TimeSpan.FromHours(5));
            
            // Level 3: Ensure mapping cache is populated
            if (song != null) {
                _cache.Set(mappingCacheKey, song, TimeSpan.FromMinutes(30));
            }
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

        // Level 2 Optimization: If they are null/not provided, we check cache first
        if (isPremiumInput == null || songInput == null)
        {
             string userCacheKey = $"user_premium_{userId ?? 0}";
             string songCacheKey = $"song_meta_{youtubeId}";

             if (_cache.TryGetValue(userCacheKey, out bool cachedPrem)) isPremium = cachedPrem;
             if (_cache.TryGetValue(songCacheKey, out SongDto? cachedSong)) song = cachedSong;
        }

        // HIGH PRIORITY FALLBACK: If song metadata is missing or generic (e.g. "Nghệ sĩ")
        // we pull from YouTube's basic details to avoid "Nghệ sĩ" and empty tags.
        bool isGenericArtist = song?.AuthorName == "Nghệ sĩ" || string.IsNullOrEmpty(song?.AuthorName);
        var genreList = song?.GenreNames?.ToList() ?? new List<string>();
        bool isGenericTags = genreList.Count == 0 || (genreList.Count == 1 && genreList[0] == "Music");

        if (song == null || isGenericArtist || isGenericTags)
        {
            try {
                var ytDetails = await _youtubeService.GetBasicVideoDetailsAsync(youtubeId);
                if (ytDetails != null)
                {
                    // Bridge artist name and tags directly from YouTube if DB is generic
                    if (isGenericArtist) artist = ytDetails.AuthorName;
                    
                    var bridgedTags = new List<string>();
                    if (!string.IsNullOrEmpty(ytDetails.Genre)) bridgedTags.Add(ytDetails.Genre);
                    if (ytDetails.Hashtags?.Any() == true) bridgedTags.AddRange(ytDetails.Hashtags);
                    
                    if (song == null) {
                        song = new SongDto {
                            Title = title ?? ytDetails.Title,
                            AuthorName = artist,
                            ThumbnailUrl = ytDetails.ThumbnailUrl,
                            GenreNames = bridgedTags
                        };
                    } else {
                        // Patch existing song object with better metadata for display
                        if (isGenericArtist) song.AuthorName = artist;
                        if (isGenericTags && bridgedTags.Any()) song.GenreNames = bridgedTags;
                    }
                }
            } catch { /* Suppress and fallback */ }
        }

        var result = new PlaybackStreamDto 
        { 
            StreamUrl = streamUrl, 
            VideoId = youtubeId, 
            SongId = song?.SongId, 
            ShowAd = !isPremium,
            Title = song?.Title ?? title,
            Author = song?.AuthorName ?? artist ?? "Nghệ sĩ",
            ThumbnailUrl = song?.ThumbnailUrl,
            GenreNames = song?.GenreNames?.ToList() ?? new List<string>()
        };

        // If we don't have the song object yet, we skip premium/explicit checks for now
        // they will be handled by the next request or by the player stopping if check fails later.
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
