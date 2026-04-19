namespace YoutubeMusicPlayer.Application.Common;

public static class SectionTypes
{
    public const string Trending = "trending";
    public const string Albums = "albums";
    public const string DailyMix = "dailymix";
    public const string Mix1 = "mix1";
    public const string Mix2 = "mix2";
    public const string Mix3 = "mix3";
    public const string Contextual = "contextual";
    public const string Focus = "focus";
    public const string Chill = "chill";
    public const string Sad = "sad";
    public const string Compilations = "compilations";
}

public static class TrackTypes
{
    public const string OfficialMV = "Official MV";
    public const string OfficialAudio = "Official Audio";
    public const string Official = "Official";
    public const string Unknown = "Unknown";
    public const string Karaoke = "Karaoke";
    public const string Compilation = "Compilation";
    public const string Remix = "Remix/Mix";
    public const string Live = "Live";
    public const string Cover = "Cover";
    public const string Lyrics = "Lyrics";
    public const string Acoustic = "Acoustic";
}
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}

public static class AuthConstants
{
    public const string InternalUserIdClaim = "InternalUserId";
}

public static class ReportStatus
{
    public const string Pending = "Pending";
    public const string Resolved = "Resolved";
    public const string Dismissed = "Dismissed";
}

public static class PaymentStatus
{
    public const string Success = "Success";
    public const string Pending = "Pending";
    public const string Failed = "Failed";
}

public static class TargetTypes
{
    public const string Song = "Song";
    public const string Playlist = "Playlist";
    public const string User = "User";
}

public static class NotificationTypes
{
    public const string System = "System";
    public const string StatusChange = "StatusChange";
    public const string Success = "Success";
    public const string Alert = "Alert";
    public const string Promotion = "Promotion";
}

public static class SearchSettings
{
    public const int DefaultDiscoveryLimit = 15;
    public const int DefaultFetchBuffer = 20;
    public const int MaxHistoryForDiscovery = 50;
    public const string TrendingVPopQuery = "V-Pop trending music 2026";
}

public static class MoodQueries
{
    // Multiple queries per mood to find more individual tracks instead of compilations
    private static readonly Dictionary<string, string[]> _querySets = new(StringComparer.OrdinalIgnoreCase)
    {
        { "chill", new[] { 
            "nhạc chill vpop hay nhất 2026 official audio", 
            "chill acoustic Việt Nam official mv 2026",
            "lofi Việt chill official audio single" 
        }},
        { "giai điệu chill", new[] { 
            "nhạc chill vpop hay nhất 2026 official audio", 
            "chill acoustic official mv 2026" 
        }},
        { "workout", new[] { 
            "gym workout music official audio 2026", 
            "nhạc tập gym sôi động official mv" 
        }},
        { "tập thể dục", new[] { 
            "gym workout music official audio 2026", 
            "nhạc tập gym sôi động official mv" 
        }},
        { "focus", new[] { 
            "nhạc piano nhẹ nhàng thư giãn official audio",
            "acoustic instrumental study music official audio",
            "nhạc nhẹ nhàng tập trung official audio 2026" 
        }},
        { "tập trung", new[] { 
            "nhạc piano nhẹ nhàng thư giãn official audio",
            "nhạc nhẹ nhàng tập trung official audio 2026" 
        }},
        { "party", new[] { 
            "nhạc dance sôi động 2026 official mv", 
            "EDM remix official audio single 2026" 
        }},
        { "sôi động", new[] { 
            "nhạc dance sôi động 2026 official mv", 
            "EDM Việt remix official audio 2026" 
        }},
        { "sad", new[] { 
            "nhạc buồn vpop hay nhất official audio 2026", 
            "ballad Việt tâm trạng official mv 2026",
            "sad vietnamese songs official audio" 
        }},
        { "tâm trạng", new[] { 
            "nhạc buồn vpop hay nhất official audio 2026", 
            "ballad Việt tâm trạng official mv 2026" 
        }},
        { "v-pop hot", new[] { 
            "vpop mới nhất 2026 official mv", 
            "nhạc Việt hot 2026 official audio" 
        }},
        { "us-uk", new[] {
            "us uk pop hits 2026 official audio",
            "billboard us uk official mv 2026"
        }},
        { "nhạc pop", new[] {
            "nhạc pop hay nhất 2026 official audio",
            "pop hits official mv 2026"
        }},
        { "k-pop", new[] {
            "kpop new songs 2026 official mv",
            "kpop trending official audio"
        }},
        { "remix", new[] {
            "remix việt hot 2026 official audio",
            "edm remix hits 2026 official mix"
        }},
        { "lofi/chill", new[] {
            "lofi chill mix 2026 official audio",
            "chill lofi vietnamese official audio"
        }},
        { "nhạc trẻ", new[] { 
            "nhạc trẻ mới ra 2026 official mv", 
            "vpop hot 2026 official audio" 
        }}
    };

    /// <summary>Returns multiple queries per mood for diverse individual track results.</summary>
    public static List<string> GetQueries(string moodTag) 
        => _querySets.TryGetValue(moodTag, out var queries) 
            ? queries.ToList() 
            : new List<string> { $"{moodTag} official audio music single 2026" };

    /// <summary>Legacy: returns first query for backward compatibility.</summary>
    public static string GetQuery(string moodTag) 
        => GetQueries(moodTag).First();
}
