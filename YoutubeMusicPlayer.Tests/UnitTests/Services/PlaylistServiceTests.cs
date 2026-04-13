using NUnit.Framework;
using Moq;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace YoutubeMusicPlayer.Tests.UnitTests.Services;

[TestFixture]
public class PlaylistServiceTests
{
    private AppDbContext _context;
    private UnitOfWork _uow;
    private PlaylistService _playlistService;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _uow = new UnitOfWork(_context);
        _playlistService = new PlaylistService(_uow);
    }

    [TearDown]
    public void TearDown()
    {
        _uow?.Dispose();
        _context?.Dispose();
    }

    [Test]
    public async Task GetUserPlaylistsAsync_ReturnsOnlyUserPlaylists()
    {
        await _context.Playlists.AddRangeAsync(new[] { 
            new Playlist { UserId = 1, Title = "P1" },
            new Playlist { UserId = 2, Title = "P2" }
        });
        await _context.SaveChangesAsync();

        var result = await _playlistService.GetUserPlaylistsAsync(1);
        Assert.That(result.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task AddSongToPlaylistAsync_Success()
    {
        var p = new Playlist { PlaylistId = 1, Title = "My List", UserId = 1 };
        await _context.Playlists.AddAsync(p);
        await _context.SaveChangesAsync();

        await _playlistService.AddSongToPlaylistAsync(1, 10, 1);
        
        Assert.That(await _context.PlaylistSongs.AnyAsync(ps => ps.PlaylistId == 1 && ps.SongId == 10), Is.True);
    }

    [Test]
    public async Task GetPlaylistByIdAsync_ReturnsWithSongs()
    {
        var p = new Playlist { PlaylistId = 5, Title = "Full" };
        var s = new Song { SongId = 1, Title = "S1" };
        var ps = new PlaylistSong { PlaylistId = 5, SongId = 1 };
        await _context.Playlists.AddAsync(p);
        await _context.Songs.AddAsync(s);
        await _context.PlaylistSongs.AddAsync(ps);
        await _context.SaveChangesAsync();

        var result = await _playlistService.GetPlaylistByIdAsync(5);
        Assert.That(result.Songs.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task DeletePlaylistAsync_MarksIsDeleted()
    {
        var p = new Playlist { PlaylistId = 10, UserId = 1 };
        await _context.Playlists.AddAsync(p);
        await _context.SaveChangesAsync();

        await _playlistService.DeletePlaylistAsync(10, 1);
        var deletedPlaylist = await _context.Playlists.FindAsync(10);
        Assert.That(deletedPlaylist, Is.Not.Null);
        Assert.That(deletedPlaylist.IsDeleted, Is.True);
    }
}

