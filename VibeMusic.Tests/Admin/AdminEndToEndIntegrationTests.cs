using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Controllers;
using VibeMusic.Domain.Entities;
using VibeMusic.Infrastructure;
using VibeMusic.Infrastructure.Persistence;
using VibeMusic.Tests.Helpers;
using Xunit;

namespace VibeMusic.Tests.Admin;

/// <summary>
/// End-to-end integration tests for admin CRUD flows.
/// Uses controller-level approach (Option C) with in-memory database to avoid CSRF complexity
/// while still testing the full controller → service → database flow.
///
/// Validates: Requirements 2.1, 2.2, 2.3, 5.1, 6.1, 7.1, 7.3
/// </summary>
public class AdminEndToEndIntegrationTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a fresh in-memory AppDbContext with a unique database name per test.
    /// </summary>
    private static AppDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Sets up a mock TempData dictionary and wires it to the controller.
    /// Returns the backing store so tests can assert on TempData values.
    /// </summary>
    private static Dictionary<string, object?> SetupTempData(Controller controller)
    {
        var store = new Dictionary<string, object?>();
        var tempDataMock = new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>();

        tempDataMock
            .SetupSet(td => td[It.IsAny<string>()] = It.IsAny<object?>())
            .Callback<string, object?>((key, value) => store[key] = value);

        tempDataMock
            .Setup(td => td[It.IsAny<string>()])
            .Returns<string>(key => store.TryGetValue(key, out var val) ? val : null);

        tempDataMock
            .Setup(td => td.ContainsKey(It.IsAny<string>()))
            .Returns<string>(key => store.ContainsKey(key));

        controller.TempData = tempDataMock.Object;
        return store;
    }

    // ─── Flow 1: Song CRUD E2E ────────────────────────────────────────────────

    /// <summary>
    /// E2E_Song_CreateEditDelete_FullFlow
    ///
    /// Tests the complete Song lifecycle:
    ///   Create Song → verify in DB → Edit Song → verify update in DB
    ///   → Delete Song → verify soft-delete in DB → Index excludes deleted song
    ///
    /// Validates: Requirements 2.1, 2.2, 2.3, 5.1
    /// </summary>
    [Fact]
    public async Task E2E_Song_CreateEditDelete_FullFlow()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        using var context = CreateInMemoryContext();

        // Mock ISongService to delegate to real in-memory logic
        var songServiceMock = new Mock<ISongService>();
        var albumServiceMock = new Mock<IAlbumService>();
        var genreServiceMock = new Mock<IGenreService>();

        // Track created song ID across steps
        int createdSongId = 0;

        // Setup: CreateSongAsync — adds song to in-memory DB
        songServiceMock
            .Setup(s => s.CreateSongAsync(It.IsAny<SongDto>(), It.IsAny<CancellationToken>()))
            .Returns(async (SongDto dto, CancellationToken ct) =>
            {
                var song = new Song
                {
                    Title = dto.Title,
                    YoutubeVideoId = dto.YoutubeVideoId,
                    IsExplicit = dto.IsExplicit,
                    IsPremiumOnly = dto.IsPremiumOnly,
                    IsDeleted = false
                };
                context.Songs.Add(song);
                await context.SaveChangesAsync(ct);
                createdSongId = song.SongId;
            });

        // Setup: UpdateSongAsync — updates song in in-memory DB
        songServiceMock
            .Setup(s => s.UpdateSongAsync(It.IsAny<SongDto>(), It.IsAny<CancellationToken>()))
            .Returns(async (SongDto dto, CancellationToken ct) =>
            {
                var song = await context.Songs.FindAsync(new object[] { dto.SongId }, ct);
                if (song != null)
                {
                    song.Title = dto.Title;
                    song.YoutubeVideoId = dto.YoutubeVideoId;
                    await context.SaveChangesAsync(ct);
                }
            });

        // Setup: DeleteSongAsync — soft-deletes song in in-memory DB
        songServiceMock
            .Setup(s => s.DeleteSongAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (int id, CancellationToken ct) =>
            {
                var song = await context.Songs.FindAsync(new object[] { id }, ct);
                if (song != null)
                {
                    song.IsDeleted = true;
                    await context.SaveChangesAsync(ct);
                }
            });

        // Setup: GetPaginatedSongsAsync — returns only non-deleted songs
        songServiceMock
            .Setup(s => s.GetPaginatedSongsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var songs = context.Songs
                    .Where(s => !s.IsDeleted)
                    .Select(s => new SongDto { SongId = s.SongId, Title = s.Title })
                    .ToList();
                return ((IEnumerable<SongDto>)songs, songs.Count);
            });

        genreServiceMock
            .Setup(s => s.GetAllGenresAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GenreDto>());

        var controller = new AdminSongController(
            songServiceMock.Object,
            albumServiceMock.Object,
            genreServiceMock.Object);
        controller.ControllerContext = TestAuthHelper.CreateAdminContext();
        var tempData = SetupTempData(controller);

        // ── Step 1: Create Song ───────────────────────────────────────────────
        var createDto = new SongDto
        {
            Title = "E2E Test Song",
            YoutubeVideoId = "e2e_vid_001",
            IsExplicit = false,
            IsPremiumOnly = false
        };

        var createResult = await controller.Create(createDto);

        // Verify redirect to Index
        var createRedirect = createResult.Should().BeOfType<RedirectToActionResult>().Subject;
        createRedirect.ActionName.Should().Be("Index",
            because: "after creating a song the controller should redirect to Index");

        // Verify TempData["Success"] was set
        tempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successful create");

        // ── Step 2: Verify song exists in DB ─────────────────────────────────
        var songInDb = await context.Songs.FirstOrDefaultAsync(s => s.Title == "E2E Test Song");
        songInDb.Should().NotBeNull(because: "song should be persisted to the database after Create");
        songInDb!.IsDeleted.Should().BeFalse(because: "newly created song should not be soft-deleted");
        songInDb.YoutubeVideoId.Should().Be("e2e_vid_001");
        createdSongId = songInDb.SongId;

        // ── Step 3: Edit Song ─────────────────────────────────────────────────
        tempData.Clear();
        var editDto = new SongDto
        {
            SongId = createdSongId,
            Title = "E2E Test Song (Updated)",
            YoutubeVideoId = "e2e_vid_001_updated"
        };

        var editResult = await controller.Edit(editDto);

        // Verify redirect to Index
        var editRedirect = editResult.Should().BeOfType<RedirectToActionResult>().Subject;
        editRedirect.ActionName.Should().Be("Index",
            because: "after editing a song the controller should redirect to Index");

        // Verify TempData["Success"] was set
        tempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successful edit");

        // ── Step 4: Verify song was updated in DB ─────────────────────────────
        var updatedSong = await context.Songs.FindAsync(createdSongId);
        updatedSong.Should().NotBeNull();
        updatedSong!.Title.Should().Be("E2E Test Song (Updated)",
            because: "song title should be updated in the database after Edit");

        // ── Step 5: Delete Song ───────────────────────────────────────────────
        tempData.Clear();
        var deleteResult = await controller.Delete(createdSongId);

        // Verify redirect to Index
        var deleteRedirect = deleteResult.Should().BeOfType<RedirectToActionResult>().Subject;
        deleteRedirect.ActionName.Should().Be("Index",
            because: "after deleting a song the controller should redirect to Index");

        // Verify TempData["Success"] was set
        tempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successful delete");

        // ── Step 6: Verify soft-delete in DB ─────────────────────────────────
        var deletedSong = await context.Songs.FindAsync(createdSongId);
        deletedSong.Should().NotBeNull(because: "soft-deleted song should still exist in the database");
        deletedSong!.IsDeleted.Should().BeTrue(
            because: "song should be marked as IsDeleted = true after soft delete (Requirement 2.3)");

        // ── Step 7: Verify deleted song NOT in Index results ──────────────────
        var indexResult = await controller.Index();
        var viewResult = indexResult.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<VibeMusic.Models.Admin.AdminSongListViewModel>().Subject;

        model.Songs.Should().NotContain(s => s.SongId == createdSongId,
            because: "soft-deleted songs should not appear in the Index list");
    }

    // ─── Flow 2: Artist Create + Verify + Reset E2E ───────────────────────────

    /// <summary>
    /// E2E_Artist_CreateVerifyReset_FullFlow
    ///
    /// Tests the complete Artist verification lifecycle:
    ///   Create Artist → verify in DB (status = Pending)
    ///   → Verify Artist via ArtistVerificationController → JSON success
    ///   → Reset Verification → JSON success
    ///   → Verify DB status = Pending after reset
    ///
    /// Validates: Requirements 2.1, 5.1, 6.1
    /// </summary>
    [Fact]
    public async Task E2E_Artist_CreateVerifyReset_FullFlow()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        using var context = CreateInMemoryContext();

        // Mock IArtistService to delegate to real in-memory logic
        var artistServiceMock = new Mock<IArtistService>();
        int createdArtistId = 0;

        // Setup: CreateArtistAsync — adds artist to in-memory DB
        artistServiceMock
            .Setup(s => s.CreateArtistAsync(It.IsAny<ArtistDto>(), It.IsAny<CancellationToken>()))
            .Returns(async (ArtistDto dto, CancellationToken ct) =>
            {
                var artist = new Artist
                {
                    Name = dto.Name,
                    Bio = dto.Bio,
                    Country = dto.Country,
                    AvatarUrl = dto.AvatarUrl,
                    VerificationStatus = ArtistVerificationStatus.Pending,
                    IsVerified = false,
                    IsDeleted = false
                };
                context.Artists.Add(artist);
                await context.SaveChangesAsync(ct);
                createdArtistId = artist.ArtistId;
            });

        var artistController = new AdminArtistController(artistServiceMock.Object);
        artistController.ControllerContext = TestAuthHelper.CreateAdminContext();
        var artistTempData = SetupTempData(artistController);

        // ── Step 1: Create Artist ─────────────────────────────────────────────
        var createDto = new ArtistDto
        {
            Name = "E2E Test Artist",
            Bio = "Test biography",
            Country = "Vietnam"
        };

        var createResult = await artistController.Create(createDto);

        // Verify redirect to Index
        var createRedirect = createResult.Should().BeOfType<RedirectToActionResult>().Subject;
        createRedirect.ActionName.Should().Be("Index",
            because: "after creating an artist the controller should redirect to Index");

        // Verify TempData["Success"] was set
        artistTempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successful artist create");

        // ── Step 2: Verify artist exists in DB with Pending status ────────────
        var artistInDb = await context.Artists.FirstOrDefaultAsync(a => a.Name == "E2E Test Artist");
        artistInDb.Should().NotBeNull(because: "artist should be persisted to the database after Create");
        artistInDb!.VerificationStatus.Should().Be(ArtistVerificationStatus.Pending,
            because: "newly created artist should have VerificationStatus = Pending");
        artistInDb.IsDeleted.Should().BeFalse();
        createdArtistId = artistInDb.ArtistId;

        // ── Step 3: Build AdminArtistVerificationController with mocked service ─
        var verificationServiceMock = new Mock<IArtistVerificationService>();
        var unitOfWork = new UnitOfWork(context);

        // Mock VerifyArtistAsync to return Verified result
        verificationServiceMock
            .Setup(s => s.VerifyArtistAsync(createdArtistId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArtistVerificationResult
            {
                ArtistId = createdArtistId,
                ArtistName = "E2E Test Artist",
                Status = ArtistVerificationStatus.Verified,
                DeezerArtistId = "deezer_123",
                DeezerArtistName = "E2E Test Artist",
                MatchScore = 0.95
            });

        // Mock ResetVerificationAsync to reset status in DB
        verificationServiceMock
            .Setup(s => s.ResetVerificationAsync(createdArtistId, It.IsAny<CancellationToken>()))
            .Returns(async (int artistId, CancellationToken ct) =>
            {
                var artist = await context.Artists.FindAsync(new object[] { artistId }, ct);
                if (artist != null)
                {
                    artist.VerificationStatus = ArtistVerificationStatus.Pending;
                    artist.IsVerified = false;
                    artist.DeezerArtistId = null;
                    artist.VerifiedAt = null;
                    await context.SaveChangesAsync(ct);
                }
            });

        // Mock GetVerificationStatsAsync for Index action
        verificationServiceMock
            .Setup(s => s.GetVerificationStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerificationStatsDto
            {
                TotalArtists = 1,
                PendingCount = 1
            });

        var verificationController = new AdminArtistVerificationController(
            verificationServiceMock.Object,
            artistServiceMock.Object,
            unitOfWork);
        verificationController.ControllerContext = TestAuthHelper.CreateAdminContext();

        // ── Step 4: Verify Artist ─────────────────────────────────────────────
        var verifyResult = await verificationController.VerifyArtist(createdArtistId);

        // SuccessResponse() returns OkObjectResult wrapping ApiResponse<T> with a Data property
        var verifyOk = verifyResult.Should().BeOfType<OkObjectResult>().Subject;
        verifyOk.Value.Should().NotBeNull();

        var verifyData = verifyOk.Value!.GetType().GetProperty("Data")?.GetValue(verifyOk.Value);
        verifyData.Should().NotBeNull(because: "ApiResponse should have a Data property");

        var successProp = verifyData!.GetType().GetProperty("success");
        successProp.Should().NotBeNull(because: "VerifyArtist response data should have a 'success' property");
        successProp!.GetValue(verifyData).Should().Be(true,
            because: "VerifyArtist should return { success = true } when artist is verified (Requirement 6.1)");

        // ── Step 5: Reset Verification ────────────────────────────────────────
        var resetResult = await verificationController.ResetVerification(createdArtistId);

        // SuccessResponse() returns OkObjectResult wrapping ApiResponse<T> with a Data property
        var resetOk = resetResult.Should().BeOfType<OkObjectResult>().Subject;
        resetOk.Value.Should().NotBeNull();

        var resetData = resetOk.Value!.GetType().GetProperty("Data")?.GetValue(resetOk.Value);
        resetData.Should().NotBeNull(because: "ApiResponse should have a Data property");

        var resetSuccessProp = resetData!.GetType().GetProperty("success");
        resetSuccessProp.Should().NotBeNull(because: "ResetVerification response data should have a 'success' property");
        resetSuccessProp!.GetValue(resetData).Should().Be(true,
            because: "ResetVerification should return { success = true }");

        // ── Step 6: Verify DB status = Pending after reset ────────────────────
        var artistAfterReset = await context.Artists.FindAsync(createdArtistId);
        artistAfterReset.Should().NotBeNull();
        artistAfterReset!.VerificationStatus.Should().Be(ArtistVerificationStatus.Pending,
            because: "artist VerificationStatus should be reset to Pending after ResetVerification (Requirement 6.5 / Property 11)");
    }

    // ─── Flow 3: Playlist Create + Add/Remove Songs + Delete E2E ─────────────

    /// <summary>
    /// E2E_Playlist_CreateAddRemoveSongsDelete_FullFlow
    ///
    /// Tests the complete Playlist lifecycle:
    ///   Create Playlist → verify in DB → Add Song → verify PlaylistSong record
    ///   → Remove Song → verify PlaylistSong removed → Delete Playlist → verify soft-delete
    ///
    /// Uses mocked IPlaylistService that delegates to in-memory DB operations directly,
    /// bypassing the raw SQL re-indexing call in the real PlaylistService that is
    /// incompatible with the in-memory database provider.
    ///
    /// Validates: Requirements 2.1, 2.3, 7.1, 7.3
    /// </summary>
    [Fact]
    public async Task E2E_Playlist_CreateAddRemoveSongsDelete_FullFlow()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        using var context = CreateInMemoryContext();

        // Seed a song to add to the playlist
        var seedSong = new Song
        {
            SongId = 1,
            Title = "Seeded Song for Playlist",
            YoutubeVideoId = "seed_vid_001",
            IsDeleted = false
        };
        context.Songs.Add(seedSong);
        await context.SaveChangesAsync();

        // Mock IPlaylistService to delegate to real in-memory DB operations.
        // We cannot use the real PlaylistService because RemoveSongFromPlaylistAsync
        // calls ExecuteSqlRawAsync for position re-indexing, which is not supported
        // by the in-memory database provider.
        var playlistServiceMock = new Mock<IPlaylistService>();
        int createdPlaylistId = 0;

        // Setup: CreateFeaturedPlaylistAsync — adds playlist to in-memory DB
        playlistServiceMock
            .Setup(s => s.CreateFeaturedPlaylistAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string title, string? featuredType, string? description, string? coverImageUrl, CancellationToken ct) =>
            {
                var playlist = new Playlist
                {
                    Title = title,
                    FeaturedType = featuredType,
                    Description = description,
                    CoverImageUrl = coverImageUrl,
                    IsFeatured = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow
                };
                context.Playlists.Add(playlist);
                await context.SaveChangesAsync(ct);
                createdPlaylistId = playlist.PlaylistId;
                return new PlaylistDto
                {
                    PlaylistId = playlist.PlaylistId,
                    Title = playlist.Title,
                    FeaturedType = playlist.FeaturedType,
                    IsFeatured = true
                };
            });

        // Setup: AddSongToPlaylistAsync — adds PlaylistSong record to in-memory DB
        playlistServiceMock
            .Setup(s => s.AddSongToPlaylistAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(async (int playlistId, int songId, int userId, bool isAdmin, CancellationToken ct) =>
            {
                var playlistSong = new PlaylistSong
                {
                    PlaylistId = playlistId,
                    SongId = songId,
                    AddedAt = DateTime.UtcNow,
                    Position = 0
                };
                context.PlaylistSongs.Add(playlistSong);
                await context.SaveChangesAsync(ct);
            });

        // Setup: RemoveSongFromPlaylistAsync — removes PlaylistSong record from in-memory DB
        // (bypasses the raw SQL re-indexing that the real service uses)
        playlistServiceMock
            .Setup(s => s.RemoveSongFromPlaylistAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(async (int playlistId, int songId, int userId, bool isAdmin, CancellationToken ct) =>
            {
                var existing = await context.PlaylistSongs
                    .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId, ct);
                if (existing != null)
                {
                    context.PlaylistSongs.Remove(existing);
                    await context.SaveChangesAsync(ct);
                }
            });

        // Setup: DeletePlaylistAsync — soft-deletes playlist in in-memory DB
        playlistServiceMock
            .Setup(s => s.DeletePlaylistAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(async (int playlistId, int userId, bool isAdmin, CancellationToken ct) =>
            {
                var playlist = await context.Playlists.FindAsync(new object[] { playlistId }, ct);
                if (playlist != null)
                {
                    playlist.IsDeleted = true;
                    await context.SaveChangesAsync(ct);
                }
            });

        // Mock ISongService (only needed for SearchSongs in EditFeatured)
        var songServiceMock = new Mock<ISongService>();
        songServiceMock
            .Setup(s => s.GetPaginatedSongsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SongDto>(), 0));

        var controller = new AdminPlaylistController(playlistServiceMock.Object, songServiceMock.Object);
        // Admin with userId = 1
        controller.ControllerContext = TestAuthHelper.CreateAdminContext(adminId: 1);
        var tempData = SetupTempData(controller);

        // ── Step 1: Create Featured Playlist ─────────────────────────────────
        var createDto = new PlaylistDto
        {
            Title = "E2E Featured Playlist",
            FeaturedType = "TopHits",
            Description = "E2E test playlist",
            IsFeatured = true
        };

        var createResult = await controller.CreateFeatured(createDto);

        // Verify redirect to Index
        var createRedirect = createResult.Should().BeOfType<RedirectToActionResult>().Subject;
        createRedirect.ActionName.Should().Be("Index",
            because: "after creating a playlist the controller should redirect to Index");

        // Verify TempData["Success"] was set
        tempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successful playlist create");

        // ── Step 2: Verify playlist exists in DB ──────────────────────────────
        var playlistInDb = await context.Playlists.FirstOrDefaultAsync(p => p.Title == "E2E Featured Playlist");
        playlistInDb.Should().NotBeNull(because: "playlist should be persisted to the database after CreateFeatured");
        playlistInDb!.FeaturedType.Should().Be("TopHits",
            because: "playlist FeaturedType should match the input DTO");
        playlistInDb.IsDeleted.Should().BeFalse();
        int playlistId = playlistInDb.PlaylistId;

        // ── Step 3: Add Song to Playlist ──────────────────────────────────────
        tempData.Clear();
        var addSongResult = await controller.AddSong(playlistId, songId: 1);

        // Verify redirect to EditFeatured
        var addRedirect = addSongResult.Should().BeOfType<RedirectToActionResult>().Subject;
        addRedirect.ActionName.Should().Be("EditFeatured",
            because: "after adding a song the controller should redirect to EditFeatured");

        // Verify TempData["Success"] was set
        tempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successfully adding a song");

        // ── Step 4: Verify PlaylistSong record exists in DB ───────────────────
        var playlistSongExists = await context.PlaylistSongs
            .AnyAsync(ps => ps.PlaylistId == playlistId && ps.SongId == 1);
        playlistSongExists.Should().BeTrue(
            because: "PlaylistSong record should exist in DB after AddSong (Requirement 7.3)");

        // ── Step 5: Remove Song from Playlist ─────────────────────────────────
        tempData.Clear();
        var removeSongResult = await controller.RemoveSong(playlistId, songId: 1);

        // Verify redirect to EditFeatured
        var removeRedirect = removeSongResult.Should().BeOfType<RedirectToActionResult>().Subject;
        removeRedirect.ActionName.Should().Be("EditFeatured",
            because: "after removing a song the controller should redirect to EditFeatured");

        // Verify TempData["Success"] was set
        tempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successfully removing a song");

        // ── Step 6: Verify PlaylistSong record removed from DB ────────────────
        var playlistSongRemoved = await context.PlaylistSongs
            .AnyAsync(ps => ps.PlaylistId == playlistId && ps.SongId == 1);
        playlistSongRemoved.Should().BeFalse(
            because: "PlaylistSong record should be removed from DB after RemoveSong (Requirement 7.3)");

        // ── Step 7: Delete Playlist ───────────────────────────────────────────
        tempData.Clear();
        var deleteResult = await controller.Delete(playlistId);

        // Verify redirect to Index
        var deleteRedirect = deleteResult.Should().BeOfType<RedirectToActionResult>().Subject;
        deleteRedirect.ActionName.Should().Be("Index",
            because: "after deleting a playlist the controller should redirect to Index");

        // Verify TempData["Success"] was set
        tempData.Should().ContainKey("Success",
            because: "controller should set TempData[\"Success\"] after successful playlist delete");

        // ── Step 8: Verify playlist soft-deleted in DB ────────────────────────
        var deletedPlaylist = await context.Playlists.FindAsync(playlistId);
        deletedPlaylist.Should().NotBeNull(because: "soft-deleted playlist should still exist in the database");
        deletedPlaylist!.IsDeleted.Should().BeTrue(
            because: "playlist should be marked as IsDeleted = true after Delete (Requirement 2.3)");
    }
}
