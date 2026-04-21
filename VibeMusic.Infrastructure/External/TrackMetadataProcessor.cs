using System.Text.RegularExpressions;
using VibeMusic.Application.Common;
using VibeMusic.Application.Interfaces;

namespace VibeMusic.Infrastructure.External;

public class TrackMetadataProcessor : ITrackMetadataProcessor
{
    // Static readonly regex patterns
    private static readonly Regex _cleanTitlePattern = new Regex(
        @"\(.*?\)|\[.*?\]|official|music|video|audio|lyrics|mv",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _isTooSimilarCleanPattern = new Regex(
        @"\(.*?\)|\[.*?\]|official|music|video|audio|lyrics|mv| - topic|vevo",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _hashtagPattern = new Regex(
        @"#\w+",
        RegexOptions.Compiled);

    private static readonly Regex _normalizeArtistPattern = new Regex(
        @" ft\.| feat\.| x | & |,| ft | feat ",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public (string Artist, string Song) ParseTitle(string title, string author)
    {
        var splitters = new[] { " - ", " | ", " – ", ": ", " by " };
        foreach (var s in splitters)
        {
            if (title.Contains(s))
            {
                var parts = title.Split(new[] { s }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) return (parts[0].Trim(), parts[1].Trim());
            }
        }
        return (author, title);
    }

    public string CleanTitle(string title)
    {
        return _cleanTitlePattern.Replace(title, "").Trim();
    }

    public string NormalizeArtist(string artist)
    {
        string[] splitters = new[] { " ft.", " feat.", " x ", " & ", ",", " ft ", " feat " };
        foreach (var s in splitters)
        {
            var parts = Regex.Split(artist, Regex.Escape(s), RegexOptions.IgnoreCase);
            if (parts.Length > 1) return parts[0].Trim();
        }
        return artist.Trim();
    }

    public string DetectTrackType(string title)
    {
        var t = title.ToLower();
        if (t.Contains("karaoke") || t.Contains("beat") || t.Contains("beat chuẩn") || t.Contains("tách lời")) return TrackTypes.Karaoke;
        if (t.Contains("tổng hợp") || t.Contains("full album") || t.Contains("nonstop") || t.Contains("collection")) return TrackTypes.Compilation;
        if (t.Contains("official music video") || t.Contains("official mv") || (t.Contains("mv") && t.Contains("official"))) return TrackTypes.OfficialMV;
        if (t.Contains("official audio") || t.Contains("official music audio")) return TrackTypes.OfficialAudio;
        if (t.Contains("official video") || t.Contains("official lyric") || t.Contains("official visualizer")) return TrackTypes.Official;

        if (t.Contains("remix") || t.Contains("mix")) return TrackTypes.Remix;
        if (t.Contains("live") || t.Contains("concert")) return TrackTypes.Live;
        if (t.Contains("cover")) return TrackTypes.Cover;
        if (t.Contains("lyric")) return TrackTypes.Lyrics;
        if (t.Contains("acoustic")) return TrackTypes.Acoustic;

        return TrackTypes.Unknown;
    }

    public string GuessGenre(string title, IEnumerable<string> tags)
    {
        title = title.ToLower();
        var allMarkers = tags.Select(t => t.ToLower()).ToList();
        allMarkers.Add(title);

        // Priority Logic: Check markers first
        if (allMarkers.Any(m => m.Contains("kpop") || m.Contains("k-pop") || m.Contains("korean"))) return "K-Pop";
        if (allMarkers.Any(m => m.Contains("jpop") || m.Contains("j-pop") || m.Contains("japanese") || m.Contains("anime"))) return "J-Pop/Anime";
        if (allMarkers.Any(m => m.Contains("remix") || m.Contains("vinahouse") || m.Contains("nonstop") || m.Contains("ncs") || m.Contains("edm"))) return "Remix";
        if (allMarkers.Any(m => m.Contains("lofi") || m.Contains("chill") || m.Contains("sleep") || m.Contains("relax"))) return "Lofi/Chill";
        if (allMarkers.Any(m => m.Contains("rap") || m.Contains("hiphop") || m.Contains("hip hop") || m.Contains("trap"))) return "Rap/Hip-Hop";
        if (allMarkers.Any(m => m.Contains("classic") || m.Contains("classical") || m.Contains("orchestra") || m.Contains("piano") || m.Contains("instrumental"))) return "Nhạc Classic";
        if (allMarkers.Any(m => m.Contains("ballad") || m.Contains("buồn") || m.Contains("tâm trạng"))) return "Ballad";
        if (allMarkers.Any(m => m.Contains("nhạc trẻ") || m.Contains("vpop") || m.Contains("v-pop") || m.Contains("việt"))) return "Nhạc trẻ";
        if (allMarkers.Any(m => m.Contains("us-uk") || m.Contains("usuk") || m.Contains("vevo") || m.Contains("pop"))) return "US-UK";

        return "Nhạc trẻ"; // Default fallback to Vietnamese popular category
    }

    public List<string> ExtractHashtags(string description)
    {
        if (string.IsNullOrEmpty(description)) return new List<string>();
        var matches = _hashtagPattern.Matches(description);
        return matches.Cast<Match>().Select(m => m.Value).ToList();
    }

    public bool IsTooSimilar(string s1, string s2, double threshold = 0.5)
    {
        string Clean(string s) => _isTooSimilarCleanPattern.Replace(s.ToLower(), "").Trim();

        var t1 = Clean(s1);
        var t2 = Clean(s2);

        if (t1 == t2) return true;

        var tokens1 = t1.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2).ToList();
        var tokens2 = t2.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2).ToList();

        if (!tokens1.Any() || !tokens2.Any()) return false;

        var common = tokens1.Intersect(tokens2).Count();
        double similarity = (double)common / Math.Max(tokens1.Count, tokens2.Count);

        return similarity > threshold;
    }

    public List<string> GenerateAiTags(YoutubeVideoDetails v)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var t = v.Title.ToLower();
        var a = v.AuthorName.ToLower();

        // Duration-based tags
        if (v.Duration.HasValue)
        {
            if (v.Duration.Value.TotalMinutes < 3) tags.Add("fast");
            if (v.Duration.Value.TotalMinutes >= 5) tags.Add("chill/long");
        }

        // Keyword-based sentiment/vibe
        if (t.Contains("remix") || t.Contains("dj") || t.Contains("vinahouse")) { tags.Add("energetic"); tags.Add("electronic"); }
        if (t.Contains("acoustic") || t.Contains("guitar") || t.Contains("piano")) { tags.Add("chill"); tags.Add("instrumental"); }
        if (t.Contains("live")) tags.Add("vocal");
        if (t.Contains("lofi") || t.Contains("chill")) tags.Add("lofi");
        if (t.Contains("sad") || t.Contains("buồn")) tags.Add("sad");

        // Channel-based tags
        if (a.Contains("vevo") || a.Contains("topic")) tags.Add("official");

        if (v.Genre != "General") tags.Add(v.Genre);

        return tags.ToList();
    }
}
