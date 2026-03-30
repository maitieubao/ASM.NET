using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Domain.Entities;

namespace YoutubeMusicPlayer.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<AlbumArtist> AlbumArtists => Set<AlbumArtist>();
    public DbSet<SongArtist> SongArtists => Set<SongArtist>();
    public DbSet<SongGenre> SongGenres => Set<SongGenre>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<ListeningHistory> ListeningHistories => Set<ListeningHistory>();
    public DbSet<UserSearchHistory> UserSearchHistories => Set<UserSearchHistory>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistSong> PlaylistSongs => Set<PlaylistSong>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<UserGenreStat> UserGenreStats => Set<UserGenreStat>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<SongLike> SongLikes => Set<SongLike>();
    public DbSet<ArtistFollower> ArtistFollowers => Set<ArtistFollower>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PlaylistSong Many-to-Many
        modelBuilder.Entity<PlaylistSong>()
            .HasKey(ps => new { ps.PlaylistId, ps.SongId });
            
        modelBuilder.Entity<PlaylistSong>()
            .HasOne(ps => ps.Playlist)
            .WithMany(p => p.PlaylistSongs)
            .HasForeignKey(ps => ps.PlaylistId);
            
        modelBuilder.Entity<PlaylistSong>()
            .HasOne(ps => ps.Song)
            .WithMany()
            .HasForeignKey(ps => ps.SongId);

        // Ensure Unique Artists by Name
        modelBuilder.Entity<Artist>()
            .HasIndex(a => a.Name)
            .IsUnique();

        // AlbumArtist Many-to-Many
        modelBuilder.Entity<AlbumArtist>()
            .HasKey(aa => new { aa.AlbumId, aa.ArtistId });
        
        modelBuilder.Entity<AlbumArtist>()
            .HasOne(aa => aa.Album)
            .WithMany(a => a.AlbumArtists)
            .HasForeignKey(aa => aa.AlbumId);
        
        modelBuilder.Entity<AlbumArtist>()
            .HasOne(aa => aa.Artist)
            .WithMany(a => a.AlbumArtists)
            .HasForeignKey(aa => aa.ArtistId);

        // SongArtist Many-to-Many
        modelBuilder.Entity<SongArtist>()
            .HasKey(sa => new { sa.SongId, sa.ArtistId });
        
        modelBuilder.Entity<SongArtist>()
            .HasOne(sa => sa.Song)
            .WithMany(s => s.SongArtists)
            .HasForeignKey(sa => sa.SongId);
        
        modelBuilder.Entity<SongArtist>()
            .HasOne(sa => sa.Artist)
            .WithMany(a => a.SongArtists)
            .HasForeignKey(sa => sa.ArtistId);

        // SongGenre Many-to-Many
        modelBuilder.Entity<SongGenre>()
            .HasKey(sg => new { sg.SongId, sg.GenreId });
        
        modelBuilder.Entity<SongGenre>()
            .HasOne(sg => sg.Song)
            .WithMany(s => s.SongGenres)
            .HasForeignKey(sg => sg.SongId);
        
        modelBuilder.Entity<SongGenre>()
            .HasOne(sg => sg.Genre)
            .WithMany(g => g.SongGenres)
            .HasForeignKey(sg => sg.GenreId);
        
        modelBuilder.Entity<Song>()
            .HasOne(s => s.Album)
            .WithMany(a => a.Songs)
            .HasForeignKey(s => s.AlbumId)
            .OnDelete(DeleteBehavior.SetNull);

        // PERFORMANCE INDEXES
        modelBuilder.Entity<Song>()
            .HasIndex(s => new { s.Title, s.AlbumId });
            
        modelBuilder.Entity<Song>()
            .HasIndex(s => s.YoutubeVideoId);

        modelBuilder.Entity<Album>()
            .HasIndex(a => a.Title);

        // Explicit Table Mapping for all entities to lowercase
        modelBuilder.Entity<Artist>().ToTable("artists");
        modelBuilder.Entity<Album>().ToTable("albums");
        modelBuilder.Entity<Song>().ToTable("songs");
        modelBuilder.Entity<Genre>().ToTable("genres");
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<UserSession>().ToTable("usersessions");
        modelBuilder.Entity<Playlist>().ToTable("playlists");
        modelBuilder.Entity<PlaylistSong>().ToTable("playlistsongs");
        modelBuilder.Entity<AlbumArtist>().ToTable("albumartists");
        modelBuilder.Entity<SongArtist>().ToTable("songartists");
        modelBuilder.Entity<SongGenre>().ToTable("songgenres");

        // SongLike Composite Key
        modelBuilder.Entity<SongLike>()
            .ToTable("songlikes")
            .HasKey(sl => new { sl.UserId, sl.SongId });

        // ArtistFollower Composite Key
        modelBuilder.Entity<ArtistFollower>()
            .ToTable("artist_followers")
            .HasKey(af => new { af.UserId, af.ArtistId });

        // Explicit Table Mapping for Self-Healing Tables
        modelBuilder.Entity<Category>().ToTable("categories");
        modelBuilder.Entity<Report>().ToTable("reports");
        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<UserGenreStat>().ToTable("user_genre_stats");
        modelBuilder.Entity<SongLike>().ToTable("songlikes");
        modelBuilder.Entity<ListeningHistory>().ToTable("listeninghistory");
        modelBuilder.Entity<UserSearchHistory>().ToTable("usersearchhistory");
        modelBuilder.Entity<SubscriptionPlan>().ToTable("subscription_plans");
        modelBuilder.Entity<UserSubscription>().ToTable("user_subscriptions");
        modelBuilder.Entity<Payment>().ToTable("payments");

        // Global lowercasing of all tables and columns
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // 1. Lowercase Table Name
            var tableName = entity.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entity.SetTableName(tableName.ToLower());
            }

            // 2. Lowercase Column Names
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().ToLower());
            }
        }
    }
}
