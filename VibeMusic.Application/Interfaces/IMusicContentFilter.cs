namespace VibeMusic.Application.Interfaces;

public interface IMusicContentFilter
{
    bool IsMusic(YoutubeVideoDetails details);
    bool IsRankingVideo(YoutubeVideoDetails details);
    bool IsCompilation(YoutubeVideoDetails details);
    bool IsKaraoke(YoutubeVideoDetails details);
    bool IsUnwantedContent(string title, string author);
    bool IsLikelyMusicCore(string title, string author, TimeSpan? duration, bool searchCompilations = false);
    bool IsPlaylistOrWeeklyCompilation(string title, string author);
    bool HasSingleTrackViolations(string title, string author);
    bool IsNonPureTrackType(string? trackType);
}
