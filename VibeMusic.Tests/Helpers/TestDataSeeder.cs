using Bogus;
using VibeMusic.Domain.Entities;
using VibeMusic.Infrastructure.Persistence;

namespace VibeMusic.Tests.Helpers;

/// <summary>
/// Seeds realistic test data into an AppDbContext using Bogus.
/// </summary>
public class TestDataSeeder
{
    private readonly AppDbContext _context;

    public TestDataSeeder(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Seeds User entities with realistic data.
    /// </summary>
    public async Task<List<User>> SeedUsers(int count = 5)
    {
        var faker = new Faker<User>()
            .RuleFor(u => u.Username, f => f.Internet.UserName())
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.PasswordHash, f => BCrypt.Net.BCrypt.HashPassword("Test@1234"))
            .RuleFor(u => u.Role, _ => "Customer")
            .RuleFor(u => u.IsPremium, f => f.Random.Bool(0.2f))
            .RuleFor(u => u.IsLocked, _ => false)
            .RuleFor(u => u.IsDeleted, _ => false)
            .RuleFor(u => u.CreatedAt, f => f.Date.Past(1).ToUniversalTime())
            .RuleFor(u => u.AvatarUrl, f => f.Internet.Avatar());

        var users = faker.Generate(count);
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();
        return users;
    }

    /// <summary>
    /// Seeds Song entities with realistic data.
    /// </summary>
    public async Task<List<Song>> SeedSongs(int count = 5, int? albumId = null)
    {
        var faker = new Faker<Song>()
            .RuleFor(s => s.Title, f => f.Music.Genre() + " - " + f.Lorem.Word())
            .RuleFor(s => s.AlbumId, _ => albumId)
            .RuleFor(s => s.Duration, f => f.Random.Int(120, 360))
            .RuleFor(s => s.ReleaseDate, f => f.Date.Past(5).ToUniversalTime())
            .RuleFor(s => s.YoutubeVideoId, f => f.Random.AlphaNumeric(11))
            .RuleFor(s => s.ThumbnailUrl, f => f.Image.PicsumUrl())
            .RuleFor(s => s.IsExplicit, f => f.Random.Bool(0.1f))
            .RuleFor(s => s.IsPremiumOnly, f => f.Random.Bool(0.2f))
            .RuleFor(s => s.PlayCount, f => f.Random.Long(0, 100000))
            .RuleFor(s => s.IsDeleted, _ => false);

        var songs = faker.Generate(count);
        await _context.Songs.AddRangeAsync(songs);
        await _context.SaveChangesAsync();
        return songs;
    }

    /// <summary>
    /// Seeds Album entities with realistic data.
    /// </summary>
    public async Task<List<Album>> SeedAlbums(int count = 3, int? artistId = null)
    {
        var faker = new Faker<Album>()
            .RuleFor(a => a.Title, f => f.Lorem.Sentence(3).TrimEnd('.'))
            .RuleFor(a => a.AlbumType, f => f.PickRandom("Album", "Single", "EP"))
            .RuleFor(a => a.CoverImageUrl, f => f.Image.PicsumUrl())
            .RuleFor(a => a.ReleaseDate, f => f.Date.Past(10).ToUniversalTime())
            .RuleFor(a => a.RecordLabel, f => f.Company.CompanyName())
            .RuleFor(a => a.IsExplicit, f => f.Random.Bool(0.1f))
            .RuleFor(a => a.IsDeleted, _ => false);

        var albums = faker.Generate(count);
        await _context.Albums.AddRangeAsync(albums);
        await _context.SaveChangesAsync();

        // Link albums to artist if provided
        if (artistId.HasValue)
        {
            foreach (var album in albums)
            {
                _context.AlbumArtists.Add(new AlbumArtist
                {
                    AlbumId = album.AlbumId,
                    ArtistId = artistId.Value
                });
            }
            await _context.SaveChangesAsync();
        }

        return albums;
    }

    /// <summary>
    /// Seeds Artist entities with realistic data.
    /// </summary>
    public async Task<List<Artist>> SeedArtists(int count = 3)
    {
        var faker = new Faker<Artist>()
            .RuleFor(a => a.Name, f => f.Name.FullName() + $"_{Guid.NewGuid():N}")
            .RuleFor(a => a.Bio, f => f.Lorem.Paragraph())
            .RuleFor(a => a.Country, f => f.Address.Country())
            .RuleFor(a => a.AvatarUrl, f => f.Internet.Avatar())
            .RuleFor(a => a.IsVerified, f => f.Random.Bool(0.3f))
            .RuleFor(a => a.SubscriberCount, f => f.Random.Int(0, 1000000))
            .RuleFor(a => a.VerificationStatus, _ => ArtistVerificationStatus.Pending)
            .RuleFor(a => a.IsDeleted, _ => false);

        var artists = faker.Generate(count);
        await _context.Artists.AddRangeAsync(artists);
        await _context.SaveChangesAsync();
        return artists;
    }

    /// <summary>
    /// Seeds Playlist entities with realistic data.
    /// </summary>
    public async Task<List<Playlist>> SeedPlaylists(int count = 3, int ownerId = 1)
    {
        var faker = new Faker<Playlist>()
            .RuleFor(p => p.Title, f => f.Lorem.Sentence(3).TrimEnd('.'))
            .RuleFor(p => p.Description, f => f.Lorem.Sentence())
            .RuleFor(p => p.CoverImageUrl, f => f.Image.PicsumUrl())
            .RuleFor(p => p.Visibility, f => f.PickRandom("Public", "Private"))
            .RuleFor(p => p.IsFeatured, _ => false)
            .RuleFor(p => p.UserId, _ => ownerId)
            .RuleFor(p => p.CreatedAt, f => f.Date.Past(1).ToUniversalTime())
            .RuleFor(p => p.IsDeleted, _ => false);

        var playlists = faker.Generate(count);
        await _context.Playlists.AddRangeAsync(playlists);
        await _context.SaveChangesAsync();
        return playlists;
    }

    /// <summary>
    /// Seeds SubscriptionPlan entities with realistic data.
    /// </summary>
    public async Task<List<SubscriptionPlan>> SeedSubscriptionPlans(int count = 3)
    {
        var faker = new Faker<SubscriptionPlan>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Finance.Amount(1, 50))
            .RuleFor(p => p.DurationDays, f => f.PickRandom(30, 90, 180, 365))
            .RuleFor(p => p.Description, f => f.Lorem.Sentence())
            .RuleFor(p => p.IsActive, _ => true);

        var plans = faker.Generate(count);
        await _context.SubscriptionPlans.AddRangeAsync(plans);
        await _context.SaveChangesAsync();
        return plans;
    }

    /// <summary>
    /// Seeds Report entities with realistic data.
    /// </summary>
    public async Task<List<Report>> SeedReports(int count = 5, string status = "Pending")
    {
        // Ensure at least one user exists for the FK
        var userId = 1;
        if (!_context.Users.Any())
        {
            var users = await SeedUsers(1);
            userId = users[0].UserId;
        }
        else
        {
            userId = _context.Users.First().UserId;
        }

        var faker = new Faker<Report>()
            .RuleFor(r => r.UserId, _ => userId)
            .RuleFor(r => r.TargetType, f => f.PickRandom("Song", "Playlist", "Comment", "User"))
            .RuleFor(r => r.TargetId, f => f.Random.Int(1, 1000).ToString())
            .RuleFor(r => r.Reason, f => f.PickRandom("Spam", "Inappropriate Content", "Copyright Violation", "Harassment"))
            .RuleFor(r => r.Details, f => f.Lorem.Sentence())
            .RuleFor(r => r.Status, _ => status)
            .RuleFor(r => r.CreatedAt, f => f.Date.Past(1).ToUniversalTime());

        var reports = faker.Generate(count);
        await _context.Reports.AddRangeAsync(reports);
        await _context.SaveChangesAsync();
        return reports;
    }

    /// <summary>
    /// Seeds Genre entities with realistic data.
    /// </summary>
    public async Task<List<Genre>> SeedGenres(int count = 3)
    {
        var f = new Faker();
        var genres = Enumerable.Range(0, count).Select(_ => new Genre
        {
            Name = f.Music.Genre() + $"_{Guid.NewGuid():N}",
            Description = f.Lorem.Sentence()
        }).ToList();

        await _context.Genres.AddRangeAsync(genres);
        await _context.SaveChangesAsync();
        return genres;
    }

    /// <summary>
    /// Seeds Category entities with realistic data.
    /// </summary>
    public async Task<List<Category>> SeedCategories(int count = 3)
    {
        var faker = new Faker<Category>()
            .RuleFor(c => c.Name, f => f.Commerce.Department() + $"_{Guid.NewGuid():N}")
            .RuleFor(c => c.Description, f => f.Lorem.Sentence())
            .RuleFor(c => c.CreatedAt, f => f.Date.Past(1).ToUniversalTime());

        var categories = faker.Generate(count);
        await _context.Categories.AddRangeAsync(categories);
        await _context.SaveChangesAsync();
        return categories;
    }
}
