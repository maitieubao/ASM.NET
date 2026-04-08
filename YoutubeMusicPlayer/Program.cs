using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Infrastructure;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure.External;
using YoutubeMusicPlayer.Domain.Entities;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

// Fix PostgreSQL DateTime issue
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Performance Optimization: Memory Cache
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

// Performance// 2. Database (Tối ưu hóa tốc độ bằng pooling và giảm độ trễ DNS)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Authentication & Identity
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/AccessDenied";
})
.AddGoogle(options =>
{
    // Ensure you have "Authentication:Google:ClientId" in appsettings.json
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "placeholder-client-id";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "placeholder-client-secret";
});

// Unit of Work & Services
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IArtistService, ArtistService>();
builder.Services.AddScoped<ISongService, SongService>();
builder.Services.AddScoped<IAlbumService, AlbumService>();
builder.Services.AddScoped<IGenreService, GenreService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IPlaylistService, PlaylistService>();
builder.Services.AddScoped<IPayOSService, PayOSService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Facades (Refinement)
builder.Services.AddScoped<IHomeFacade, HomeFacade>();
builder.Services.AddScoped<IPlaybackFacade, PlaybackFacade>();
builder.Services.AddScoped<IProfileFacade, ProfileFacade>();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- NEW REFINED ARCHITECTURE (Background Processing) ---
builder.Services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
builder.Services.AddHostedService<QueuedHostedService>();

// External Services
builder.Services.AddScoped<IYoutubeService, YoutubeService>();
builder.Services.AddHttpClient<ILyricsService, LyricsService>();
builder.Services.AddHttpClient<IDeezerService, DeezerService>();
builder.Services.AddHttpClient<IWikipediaService, WikipediaService>();
builder.Services.AddScoped<IAiAgentService, SemanticKernelAgentService>();

var app = builder.Build();

// --- SELF-HEALING DATABASE MIGRATION ---
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // ONE-TIME RESET FOR TRENDING ALBUMS (Force refresh from Deezer)
        // context.Database.ExecuteSqlRaw("TRUNCATE TABLE songartists CASCADE; TRUNCATE TABLE albumartists CASCADE; TRUNCATE TABLE songlikes CASCADE; TRUNCATE TABLE playlistsongs CASCADE; DELETE FROM songs; DELETE FROM albums;");
        // Console.WriteLine("[DB INIT] Albums and Songs cleared for fresh Deezer sync.");
        
        // Add lyricstext to songs if missing
        // ... (rest of migration logic)
        await context.Database.ExecuteSqlRawAsync(@"
            DO $$ 
            BEGIN 
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='songs' AND column_name='lyricstext') THEN
                    ALTER TABLE songs ADD COLUMN lyricstext text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='artists' AND column_name='bio') THEN
                    ALTER TABLE artists ADD COLUMN bio text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='artists' AND column_name='wikipedia_url') THEN
                    ALTER TABLE artists ADD COLUMN wikipedia_url text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='total_listen_seconds') THEN
                    ALTER TABLE users ADD COLUMN total_listen_seconds double precision DEFAULT 0;
                END IF;

                -- Premium & Content Security (Subscription Tables)
                CREATE TABLE IF NOT EXISTS subscription_plans (
                    planid SERIAL PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    price DECIMAL NOT NULL,
                    duration_days INTEGER NOT NULL,
                    description TEXT,
                    is_active BOOLEAN DEFAULT TRUE
                );

                CREATE TABLE IF NOT EXISTS user_subscriptions (
                    user_subscription_id SERIAL PRIMARY KEY,
                    userid INTEGER NOT NULL REFERENCES users(userid),
                    planid INTEGER NOT NULL REFERENCES subscription_plans(planid),
                    start_date TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    end_date TIMESTAMP WITH TIME ZONE,
                    is_active BOOLEAN DEFAULT TRUE
                );

                CREATE TABLE IF NOT EXISTS payments (
                    paymentid SERIAL PRIMARY KEY,
                    userid INTEGER NOT NULL REFERENCES users(userid),
                    planid INTEGER NOT NULL REFERENCES subscription_plans(planid),
                    amount DECIMAL NOT NULL,
                    payment_date TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
                    order_code BIGINT NOT NULL,
                    payos_transaction_id TEXT
                );

                -- Fix missing columns in reports
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='reports' AND column_name='details') THEN
                    ALTER TABLE reports ADD COLUMN details text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='reports' AND column_name='status') THEN
                    ALTER TABLE reports ADD COLUMN status text DEFAULT 'Pending';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='reports' AND column_name='reason') THEN
                    ALTER TABLE reports ADD COLUMN reason text NOT NULL DEFAULT 'User Report';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='reports' AND column_name='resolvedat') THEN
                    ALTER TABLE reports ADD COLUMN resolvedat timestamp with time zone;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='reports' AND column_name='targettype') THEN
                    ALTER TABLE reports ADD COLUMN targettype text NOT NULL DEFAULT 'Song';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='reports' AND column_name='targetid') THEN
                    ALTER TABLE reports ADD COLUMN targetid text NOT NULL DEFAULT '0';
                END IF;

                -- Fix missing columns in notifications
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='notifications' AND column_name='type') THEN
                    ALTER TABLE notifications ADD COLUMN type text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='notifications' AND column_name='isread') THEN
                    ALTER TABLE notifications ADD COLUMN isread boolean DEFAULT false;
                END IF;

                -- Fix missing columns in playlists
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='playlists' AND column_name='featuredtype') THEN
                    ALTER TABLE playlists ADD COLUMN featuredtype text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='playlists' AND column_name='isfeatured') THEN
                    ALTER TABLE playlists ADD COLUMN isfeatured boolean DEFAULT false;
                END IF;

                -- Fix missing columns in genres
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='genres' AND column_name='description') THEN
                    ALTER TABLE genres ADD COLUMN description TEXT;
                END IF;

                -- Premium & Content Security
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='dateofbirth') THEN
                    ALTER TABLE users ADD COLUMN dateofbirth TIMESTAMP WITH TIME ZONE;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='playlists' AND column_name='visibility') THEN
                    ALTER TABLE playlists ADD COLUMN visibility TEXT DEFAULT 'Public';
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='playlistsongs' AND column_name='position') THEN
                    ALTER TABLE playlistsongs ADD COLUMN position INTEGER DEFAULT 0;
                END IF;

                -- Social & System Features
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='resettoken') THEN
                    ALTER TABLE users ADD COLUMN resettoken text;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='resettokenexpiry') THEN
                    ALTER TABLE users ADD COLUMN resettokenexpiry timestamp with time zone;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='comments' AND column_name='parentcommentid') THEN
                    ALTER TABLE comments ADD COLUMN parentcommentid integer;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='comments' AND column_name='updatedat') THEN
                    ALTER TABLE comments ADD COLUMN updatedat timestamp with time zone;
                END IF;

                -- New Tables
                CREATE TABLE IF NOT EXISTS comment_likes (
                    likeid SERIAL PRIMARY KEY,
                    userid INTEGER NOT NULL REFERENCES users(userid),
                    commentid INTEGER NOT NULL REFERENCES comments(commentid),
                    createdat TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS categories (
                    categoryid SERIAL PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    description TEXT,
                    createdat TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS reports (
                    reportid SERIAL PRIMARY KEY,
                    userid INTEGER NOT NULL REFERENCES users(userid),
                    targettype TEXT NOT NULL,
                    targetid TEXT NOT NULL,
                    reason TEXT NOT NULL,
                    details TEXT,
                    status TEXT DEFAULT 'Pending',
                    createdat TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    resolvedat TIMESTAMP WITH TIME ZONE
                );

                CREATE TABLE IF NOT EXISTS notifications (
                    notificationid SERIAL PRIMARY KEY,
                    userid INTEGER REFERENCES users(userid),
                    title TEXT NOT NULL,
                    message TEXT NOT NULL,
                    type TEXT,
                    isread BOOLEAN DEFAULT FALSE,
                    createdat TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS user_genre_stats (
                    statid SERIAL PRIMARY KEY,
                    userid INTEGER NOT NULL,
                    genre_name VARCHAR(100) NOT NULL,
                    listen_seconds DOUBLE PRECISION DEFAULT 0,
                    updatedat TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS songlikes (
                    userid INTEGER NOT NULL REFERENCES users(userid),
                    songid INTEGER NOT NULL REFERENCES songs(songid),
                    likedat TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (userid, songid)
                );

                CREATE TABLE IF NOT EXISTS artist_followers (
                    userid INTEGER NOT NULL REFERENCES users(userid),
                    artistid INTEGER NOT NULL REFERENCES artists(artistid),
                    followedat TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (userid, artistid)
                );
            END $$;");

        // --- NEW: SEED SUBSCRIPTION PLANS ---
        if (!await context.SubscriptionPlans.AnyAsync())
        {
            context.SubscriptionPlans.AddRange(new List<SubscriptionPlan>
            {
                new SubscriptionPlan { Name = "Gói 1 Tháng", Price = 59000, DurationDays = 30, Description = "Sử dụng đầy đủ mọi tính năng trong 30 ngày." },
                new SubscriptionPlan { Name = "Gói 3 Tháng", Price = 159000, DurationDays = 90, Description = "Tiết kiệm hơn với gói 3 tháng cao cấp." },
                new SubscriptionPlan { Name = "Gói 1 Năm", Price = 499000, DurationDays = 365, Description = "Trải nghiệm âm nhạc đỉnh cao cả năm." }
            });
            await context.SaveChangesAsync();
            Console.WriteLine("[DB INIT] Default Subscription Plans seeded.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB INIT] Warning: {ex.Message}. Make sure database is reachable.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // Enable HSTS in development too if using HTTPS for Google Login
    app.UseHsts();
}

// Enable Swagger in all environments for testing (as requested by USER)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Youtube Music Player API v1");
    // To serve the Swagger UI at the app's root (http://localhost:<port>/swagger), uncomment below
    // c.RoutePrefix = string.Empty;
});

// Thêm hệ thống giám sát Request
app.Use(async (context, next) =>
{
    Console.WriteLine($"[MONITOR] Request: {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
    await next();
});

app.UseHttpsRedirection(); 
app.UseResponseCaching();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
