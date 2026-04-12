using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Domain.Entities;
using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.IO;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false);

var configuration = builder.Build();
var connectionString = configuration.GetConnectionString("DefaultConnection");

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var context = new AppDbContext(optionsBuilder.Options);

var videoId = "rYEDA3JcQqw";
var song = context.Songs.FirstOrDefault(s => s.YoutubeVideoId == videoId);

if (song == null) {
    Console.WriteLine($"Song with VideoID {videoId} not found.");
} else {
    Console.WriteLine($"Song: {song.Title}");
    Console.WriteLine($"Lyrics Length: {song.LyricsText?.Length ?? 0}");
    if (!string.IsNullOrEmpty(song.LyricsText)) {
        Console.WriteLine("Lyrics Preview: " + (song.LyricsText.Length > 100 ? song.LyricsText.Substring(0, 100) : song.LyricsText));
    }
}
