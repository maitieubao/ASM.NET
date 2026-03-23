using System.Collections.Generic;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Models;

public class HomeViewModel
{
    public IEnumerable<YoutubeVideoDetails> NhacTre { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacRemix { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacTikTok { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacShorts { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacPop { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacBallad { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacClassic { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacKPop { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> NhacUSUK { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<YoutubeVideoDetails> RecentListened { get; set; } = new List<YoutubeVideoDetails>();
    public IEnumerable<string> RecentSearches { get; set; } = new List<string>();
}
