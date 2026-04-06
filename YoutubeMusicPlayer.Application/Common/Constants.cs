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
    public const string Official = "Official";
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
    private static readonly Dictionary<string, string> _queries = new(StringComparer.OrdinalIgnoreCase)
    {
        { "chill", "lofi music study chill hits 2026" },
        { "giai điệu chill", "lofi music study chill hits 2026" },
        { "workout", "gym workout motivation music 2026 energetic" },
        { "tập thể dục", "gym workout motivation music 2026 energetic" },
        { "focus", "deep focus music concentration study binaural" },
        { "tập trung", "deep focus music concentration study binaural" },
        { "party", "party mix 2026 club edm music remixes" },
        { "sôi động", "party mix 2026 club edm music remixes" },
        { "sad", "sad emotional songs acoustic pop ballad" },
        { "tâm trạng", "sad emotional songs acoustic pop ballad" },
        { "v-pop hot", "v-pop top hits hiện nay 2026" },
        { "nhạc trẻ", "v-pop top hits hiện nay 2026" }
    };

    public static string GetQuery(string moodTag) 
        => _queries.TryGetValue(moodTag, out var query) ? query : $"{moodTag} music";
}
