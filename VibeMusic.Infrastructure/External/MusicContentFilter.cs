using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VibeMusic.Application.Common;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Infrastructure.External;

public class MusicContentFilter : IMusicContentFilter
{
    // --- Static readonly regex patterns ---
    private static readonly Regex _rankingTopNumberPattern = new Regex(@"top\s+\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _weeklyCompilationOfThePattern = new Regex(@"\bof\s+the\s+(week|month|year)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _weeklyCompilationThisPattern = new Regex(@"\bthis\s+(week|month)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _newMusicDayPattern = new Regex(@"\bnew\s+music\s+(friday|monday|tuesday|wednesday|thursday|saturday|sunday)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _weeklyMonthlyHitsPattern = new Regex(@"\b(weekly|monthly)\s+(hits|songs|music|playlist|mix|chart)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _newReleasesStartPattern = new Regex(@"^new\s+(songs|releases|music|hits)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _bestNewSongsPattern = new Regex(@"\bbest\s+(new\s+)?(songs|music|hits)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _yearPattern = new Regex(@"\b20\d\d\b", RegexOptions.Compiled);
    private static readonly Regex _hotTrendingThisPattern = new Regex(@"\b(hot|trending|popular)\s+this\s+(week|month)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _songsOfMonthPattern = new Regex(@"\b(songs|hits|music)\s+of\s+(january|february|march|april|may|june|july|august|september|october|november|december)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _newSongsParenPattern = new Regex(@"^new\s+songs?\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _newMusicParenPattern = new Regex(@"^new\s+music\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _seasonMixPattern = new Regex(@"\b(january|february|march|april|may|june|july|august|september|october|november|december|spring|summer|fall|autumn|winter)\s+mix\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _speedUpSlowedPattern = new Regex(@"\b(sped\s*up|slowed|reverb|nightcore|phonk)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _mashupMegamixPattern = new Regex(@"\b(mashup|megamix|medley)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _djSetPattern = new Regex(@"\b(dj\s+set|full\s+set|live\s+set)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _fullSetMegamixPattern = new Regex(@"\b(full\s+set|megamix)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _nhacMoodPattern = new Regex(@"^nhạc\s+(chill|buồn|hay|hot|trẻ|trữ tình|sôi động|tập trung|thư giãn|lofi|lo-fi|edm|remix|acoustic|piano|piano nhẹ)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _nhacMoodCorePattern = new Regex(@"^nhạc\s+(chill|buồn|hay|hot|trẻ|trữ tình|sôi động|tập trung|thư giãn|lofi|lo-fi|edm|remix|acoustic|piano)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _beatStandalonePattern = new Regex(@"\bbeat\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _beatStartPattern = new Regex(@"^beat\s", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _beatEndPattern = new Regex(@"\s+beat$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Script detection patterns
    private static readonly Regex _devanagariPattern = new Regex(@"[\u0900-\u097F]", RegexOptions.Compiled);
    private static readonly Regex _arabicPattern = new Regex(@"[\u0600-\u06FF]", RegexOptions.Compiled);
    private static readonly Regex _gurmukhiPattern = new Regex(@"[\u0A00-\u0A7F]", RegexOptions.Compiled);
    private static readonly Regex _bengaliPattern = new Regex(@"[\u0980-\u09FF]", RegexOptions.Compiled);
    private static readonly Regex _tamilPattern = new Regex(@"[\u0B80-\u0BFF]", RegexOptions.Compiled);
    private static readonly Regex _teluguPattern = new Regex(@"[\u0C00-\u0C7F]", RegexOptions.Compiled);
    private static readonly Regex _malayalamPattern = new Regex(@"[\u0D00-\u0D7F]", RegexOptions.Compiled);
    private static readonly Regex _thaiPattern = new Regex(@"[\u0E00-\u0E7F]", RegexOptions.Compiled);

    // --- Static readonly keyword arrays ---
    private static readonly string[] _rankingChannelKeywords = new[]
    {
        "bảng xếp hạng", "xếp hạng", "top music", "ranking", "billboard"
    };

    private static readonly string[] _rankingKeywords = new[]
    {
        "top 10", "top 20", "top 50", "top 100",
        "bxh", "bảng xếp hạng", "billboard", "chart", "ranking", "countdown",
        "xếp hạng", "top bài hát", "top music", "top hits",
        "top trending", "top ca khúc", "top nhạc",
        "top vpop", "top v-pop", "top nhạc trẻ", "top ca sĩ",
        "top nghệ sĩ", "top album", "top songs", "top tracks",
        "nhiều lượt xem nhất", "lượt xem nhiều nhất",
        "most viewed", "most popular",
        "hot nhất tháng", "hay nhất tuần"
    };

    private static readonly string[] _unwantedKeywords = new[]
    {
        // Indian languages
        "hindi", "bollywood", "punjabi", "bhojpuri", "tamil song", "telugu song",
        "kannada", "malayalam", "marathi", "gujarati", "bengali song",
        "desi", "tollywood", "kollywood", "mollywood",
        "new hindi", "hindi song", "hindi music", "hindi official",
        "new punjabi", "punjabi song", "punjabi music",
        // Arabic/Middle Eastern
        "arabic", "nasheed", "urdu", "qawwali", "ghazal",
        // Other non-target
        "reggaeton", "bachata"
    };

    private static readonly string[] _unwantedChannels = new[]
    {
        "speed records", "t-series", "zee music", "tips official", "shemaroo",
        "saregama", "yrf", "sony music india", "eros now", "venus",
        "geet mp3", "white hill music", "desi music factory",
        "aditya music", "mango music", "lahari music",
        "anand audio", "rotana", "melody", "music world"
    };

    private static readonly string[] _compilationChannels = new[]
    {
        "inmusic", "in music", "new music",
        "music weekly", "weekly music", "songs weekly",
        "music friday", "new releases", "fresh music",
        "music chart", "chart music", "hit music",
        "music playlist", "playlist music",
        "top music official", "music official top"
    };

    private static readonly string[] _compilationKeywords = new[]
    {
        "nonstop", "tổng hợp", "tuyển tập", "collection", "best of",
        "full album", "nhạc tuyển tập", "tổng hợp nhạc",
        "liên khúc", "dễ ngủ", "nghe cả ngày", "nghe hoài không chán",
        "mashup", "tiktok", "megamix", "medley"
    };

    private static readonly string[] _nonSingleChannelHints = new[]
    {
        "nightcore", "mashup channel", "mix channel", "remix channel"
    };

    /// <summary>
    /// Detects ranking/chart/billboard videos that should NEVER appear on the UI.
    /// These are videos like "BXH Nhạc Trẻ", "Top 100 bài hát", "Billboard Hot 100", etc.
    /// </summary>
    public bool IsRankingVideo(YoutubeVideoDetails details)
    {
        var title = details.Title.ToLower();
        var author = details.AuthorName?.ToLower() ?? "";

        // Check author/channel name for ranking channel indicators
        if (_rankingChannelKeywords.Any(k => author.Contains(k))) return true;

        // Regex: "top" followed by a number (like "top 5", "top 30")
        // Exception FIRST: short titles like "Top of the World" (a real song, < 25 chars)
        if (title.Contains("top") && title.Length < 25)
        {
            // Only allow if it doesn't contain a number after "top"
            if (!_rankingTopNumberPattern.IsMatch(title))
                return false; // Short title without number → real song, allow
        }

        if (_rankingTopNumberPattern.IsMatch(title))
            return true;

        // Comprehensive ranking keywords
        return _rankingKeywords.Any(k => title.Contains(k));
    }

    /// <summary>
    /// Detects content in unwanted languages/regions (Indian, Arabic, etc.)
    /// Only Vietnamese, English, Korean, Japanese, Chinese content is allowed.
    /// </summary>
    public bool IsUnwantedContent(string title, string author)
    {
        var t = (title ?? "").ToLower();
        var a = (author ?? "").ToLower();

        // Language/region keywords to exclude
        if (_unwantedKeywords.Any(k => t.Contains(k))) return true;

        // Known Indian/Arabic music channels to block
        if (_unwantedChannels.Any(k => a.Contains(k))) return true;

        // Detect non-Latin scripts (Devanagari, Arabic, Gurmukhi/Punjabi, Bengali, Tamil, Telugu)
        // These indicate Indian/Arabic content that is outside target audience
        if (_devanagariPattern.IsMatch(title ?? "")) return true;  // Devanagari (Hindi)
        if (_arabicPattern.IsMatch(title ?? "")) return true;      // Arabic
        if (_gurmukhiPattern.IsMatch(title ?? "")) return true;    // Gurmukhi (Punjabi)
        if (_bengaliPattern.IsMatch(title ?? "")) return true;     // Bengali
        if (_tamilPattern.IsMatch(title ?? "")) return true;       // Tamil
        if (_teluguPattern.IsMatch(title ?? "")) return true;      // Telugu
        if (_malayalamPattern.IsMatch(title ?? "")) return true;   // Malayalam
        if (_thaiPattern.IsMatch(title ?? "")) return true;        // Thai

        return false;
    }

    /// <summary>
    /// Checks if a song is a pure individual track (not ranking, not too long, not karaoke).
    /// Used by ALL sections EXCEPT "Nhạc tổng hợp".
    /// Strict 7-minute limit for non-official content, 10-minute limit for official MVs.
    /// </summary>
    public bool IsMusic(YoutubeVideoDetails details)
    {
        if (IsRankingVideo(details)) return false;
        if (IsKaraoke(details)) return false;
        if (IsUnwantedContent(details.Title, details.AuthorName)) return false;
        if (IsPlaylistOrWeeklyCompilation(details.Title, details.AuthorName)) return false;
        if (HasSingleTrackViolations(details.Title, details.AuthorName)) return false;
        if (IsNonPureTrackType(details.TrackType)) return false;

        var title = details.Title.ToLower();

        // Duration filtering: strict 7-minute limit, relaxed to 10 mins for official MVs
        if (details.Duration.HasValue)
        {
            bool isOfficialContent = title.Contains("official") || title.Contains("mv");
            double maxMinutes = isOfficialContent ? 10.0 : 7.0;
            if (details.Duration.Value.TotalMinutes > maxMinutes) return false;
        }

        // Exclude compilation-type keywords (nonstop, tổng hợp, tuyển tập, mashup, tiktok, etc.)
        if (_compilationKeywords.Any(k => title.Contains(k))) return false;

        // Exclude viral/trends compilation videos
        if ((title.Contains("viral") && (title.Contains("trend") || title.Contains("music"))) ||
            (title.Contains("trend") && title.Contains("music")))
            return false;

        // Generic "nhạc + mood descriptor" titles are always compilations, not individual songs
        if (_nhacMoodPattern.IsMatch(title))
            return false;

        return IsLikelyMusicCore(title, details.AuthorName, details.Duration);
    }

    /// <summary>
    /// Phát hiện playlist tổng hợp theo tuần/tháng và các kênh chuyên làm compilation.
    /// Ví dụ: "New Songs Of The Week", "New Music Friday", "Best Songs This Month", v.v.
    /// </summary>
    public bool IsPlaylistOrWeeklyCompilation(string title, string author)
    {
        var t = (title ?? "").ToLower();
        var a = (author ?? "").ToLower();

        // --- PATTERN 1: Weekly/Monthly playlist titles ---
        // "New Songs Of The Week", "Songs Of The Week", "Music Of The Week"
        if (_weeklyCompilationOfThePattern.IsMatch(t)) return true;
        // "This Week", "This Month" in music context
        if (_weeklyCompilationThisPattern.IsMatch(t) &&
            (t.Contains("song") || t.Contains("music") || t.Contains("hit") || t.Contains("new"))) return true;
        // "New Music Friday", "New Music Monday", etc.
        if (_newMusicDayPattern.IsMatch(t)) return true;
        // "Weekly", "Monthly" music roundups
        if (_weeklyMonthlyHitsPattern.IsMatch(t)) return true;
        // "New Releases", "New Songs" as a collection title (not a single song)
        if (_newReleasesStartPattern.IsMatch(t) &&
            (t.Contains("week") || t.Contains("month") || t.Contains("april") || t.Contains("march") ||
             t.Contains("january") || t.Contains("february") || t.Contains("may") || t.Contains("june") ||
             t.Contains("july") || t.Contains("august") || t.Contains("september") || t.Contains("october") ||
             t.Contains("november") || t.Contains("december") || t.Contains("2025") || t.Contains("2026")))
            return true;
        // "Best New Songs", "Best Songs April 2026"
        if (_bestNewSongsPattern.IsMatch(t) &&
            (t.Contains("week") || t.Contains("month") || _yearPattern.IsMatch(t)))
            return true;
        // "Hot This Week", "Trending This Week"
        if (_hotTrendingThisPattern.IsMatch(t)) return true;

        // --- PATTERN 2: Playlist/compilation channel indicators ---
        // Channels that ONLY make weekly/monthly compilation playlists
        if (_compilationChannels.Any(k => a.Contains(k))) return true;

        // --- PATTERN 3: Title structure "Songs Of [Month] [Year]" ---
        // "Songs Of April 2026", "Hits Of March 2025"
        if (_songsOfMonthPattern.IsMatch(t))
            return true;

        // --- PATTERN 4: Numbered/dated collection titles ---
        // "New Songs (April 3, 2026)", "New Songs (Week 14)"
        if (_newSongsParenPattern.IsMatch(t) || _newMusicParenPattern.IsMatch(t))
            return true;

        // --- PATTERN 5: "Mix" + date/period patterns ---
        // "April Mix 2026", "Spring Mix 2026"
        if (_seasonMixPattern.IsMatch(t))
            return true;

        return false;
    }

    /// <summary>
    /// Legacy method kept for GetCompilationsAsync.
    /// Checks if a video is a compilation (long music, nonstop, tổng hợp, etc.).
    /// EXCLUDES ranking videos — compilations section should never show BXH/chart videos.
    /// </summary>
    public bool IsCompilation(YoutubeVideoDetails details)
    {
        // NEVER allow ranking videos, even in compilations
        if (IsRankingVideo(details)) return false;
        // NEVER allow unwanted language content
        if (IsUnwantedContent(details.Title, details.AuthorName)) return false;

        var title = details.Title.ToLower();

        var compilationKeywords = new[]
        {
            "tổng hợp", "nonstop", "collection", "best of",
            "tuyển tập", "tổng hợp nhạc", "nhạc tuyển tập",
            "full album", "playlist"
        };

        bool hasCompilationKeyword = compilationKeywords.Any(k => title.Contains(k));

        // Also consider long videos (> 7 mins) as potential compilations
        bool isLongVideo = details.Duration.HasValue && details.Duration.Value.TotalMinutes > 7.0;

        return hasCompilationKeyword || isLongVideo;
    }

    public bool IsKaraoke(YoutubeVideoDetails details)
    {
        var title = details.Title.ToLower();
        // "beat" chỉ bị chặn khi đứng một mình hoặc kết hợp với "chuẩn"/"gốc"
        // Tránh chặn "Beat It", "Heartbeat", "Deadbeat" v.v.
        bool isBeat = _beatStandalonePattern.IsMatch(title) &&
                      (title.Contains("beat chuẩn") || title.Contains("beat gốc") ||
                       title.Contains("phối beat") || title.Contains("nhạc beat") ||
                       _beatStartPattern.IsMatch(title) ||
                       _beatEndPattern.IsMatch(title));
        return title.Contains("karaoke") || isBeat || title.Contains("tách lời") || title.Contains("không lời");
    }

    public bool IsLikelyMusicCore(string title, string author, TimeSpan? duration, bool searchCompilations = false)
    {
        var t = (title ?? "").ToLower();

        // Unwanted language/region content — ALWAYS excluded
        if (IsUnwantedContent(title ?? "", author ?? "")) return false;

        // Weekly/monthly playlist compilations — ALWAYS excluded from individual track sections
        if (!searchCompilations && IsPlaylistOrWeeklyCompilation(title ?? "", author ?? "")) return false;
        if (!searchCompilations && HasSingleTrackViolations(title ?? "", author ?? "")) return false;

        // Ranking videos — ALWAYS excluded
        var authorLower = (author ?? "").ToLower();
        if (_rankingChannelKeywords.Any(k => authorLower.Contains(k))) return false;

        // "top N" pattern — always ranking
        if (_rankingTopNumberPattern.IsMatch(t)) return false;

        // Short "top" titles without number → real song (e.g. "Top of the World")
        if (t.Contains("top") && t.Length < 25 && !_rankingTopNumberPattern.IsMatch(t))
        {
            // Allow — fall through to other checks
        }
        else
        {
            if (_rankingKeywords.Any(k => t.Contains(k))) return false;
        }

        if (searchCompilations)
        {
            // For explicitly searching compilations, allow long content but still block ranking
            return true;
        }

        // Strict 7-minute limit for regular discovery
        bool isOfficialContent = t.Contains("official") || t.Contains("mv");
        double maxMinutes = isOfficialContent ? 10.0 : 7.0;
        if (duration.HasValue && duration.Value.TotalMinutes > maxMinutes) return false;

        // Exclude compilation-type keywords in regular search
        if (_compilationKeywords.Any(k => t.Contains(k))) return false;

        // Exclude viral/trends compilations
        if ((t.Contains("viral") && (t.Contains("trend") || t.Contains("music"))) ||
            (t.Contains("trend") && t.Contains("music")))
            return false;

        // Generic "nhạc + mood descriptor" titles are compilations
        if (_nhacMoodCorePattern.IsMatch(t))
            return false;

        // Exclude karaoke
        if (t.Contains("karaoke") || t.Contains("tách lời")) return false;

        return true;
    }

    /// <summary>
    /// Reject non-single variants: remix/mix/live set/cover/sped/slowed/reverb/mashup...
    /// Chỉ chặn các dạng KHÔNG phải bài hát đơn lẻ chính thức.
    /// Live performance và Cover chính thức vẫn được cho qua.
    /// </summary>
    public bool HasSingleTrackViolations(string title, string author)
    {
        var t = (title ?? "").ToLower();
        var a = (author ?? string.Empty).ToLower();

        // Chặn các biến thể kỹ thuật rõ ràng không phải bài gốc
        if (_speedUpSlowedPattern.IsMatch(t))
            return true;

        // Chặn mashup và megamix (nhiều bài ghép lại)
        if (_mashupMegamixPattern.IsMatch(t))
            return true;

        // Chặn DJ set / full set (không phải bài đơn)
        if (_djSetPattern.IsMatch(t))
            return true;

        // Chặn playlist/full set rõ ràng
        if (_fullSetMegamixPattern.IsMatch(t))
            return true;

        // Chặn kênh chuyên remix/nightcore
        if (_nonSingleChannelHints.Any(k => a.Contains(k))) return true;

        // KHÔNG chặn: remix (có thể là official remix), mix (có thể là "mix" trong tên bài),
        // live (live performance chính thức), cover (cover chính thức), acoustic version
        return false;
    }

    public bool IsNonPureTrackType(string? trackType)
    {
        // Chỉ chặn Compilation và Karaoke — không chặn Remix, Live, Cover vì chúng có thể là nội dung chất lượng
        return string.Equals(trackType, TrackTypes.Compilation, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trackType, TrackTypes.Karaoke, StringComparison.OrdinalIgnoreCase);
    }
}
