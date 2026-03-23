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
        
        // Song-Album One-to-Many
        modelBuilder.Entity<Song>()
            .HasOne(s => s.Album)
            .WithMany(a => a.Songs)
            .HasForeignKey(s => s.AlbumId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
