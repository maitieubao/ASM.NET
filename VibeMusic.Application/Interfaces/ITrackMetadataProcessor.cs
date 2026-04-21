namespace VibeMusic.Application.Interfaces;

public interface ITrackMetadataProcessor
{
    (string Artist, string Song) ParseTitle(string title, string author);
    string CleanTitle(string title);
    string NormalizeArtist(string artist);
    string DetectTrackType(string title);
    string GuessGenre(string title, IEnumerable<string> tags);
    List<string> ExtractHashtags(string description);
    bool IsTooSimilar(string s1, string s2, double threshold = 0.5);
    List<string> GenerateAiTags(YoutubeVideoDetails details);
}
