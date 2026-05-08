using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VibeMusic.Application.Interfaces;
using VibeMusic.Application.Services;
using VibeMusic.Domain.Interfaces;
using VibeMusic.Infrastructure;
using VibeMusic.Infrastructure.Persistence;
using VibeMusic.Infrastructure.External;
using VibeMusic.Infrastructure.External.AiPlugins;
using VibeMusic.Domain.Entities;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

static bool ShouldUseClientPooling(string connectionString)
{
    // Re-enable pooling but with strict idle pruning for Supabase PgBouncer compatibility.
    // PgBouncer usually closes idle connections after 60s. We must evict them from our pool before that.
    return true;
}

// Fix PostgreSQL DateTime issue (Enable legacy timestamp behavior)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Performance Optimization: Memory Cache
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

// --- SMART DATABASE CONNECTION DISCOVERY (Thích nghi mọi loại Wifi) ---
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
var fallbacks = builder.Configuration.GetSection("ConnectionStrings:ConnectionStringsFallback").Get<string[]>() ?? Array.Empty<string>();

// Skip DB connection discovery in Testing environment (used by integration tests with InMemory DB)
var isTestingEnvironment = builder.Environment.IsEnvironment("Testing");

// Chiến lược: Ưu tiên Direct Connection (Port 5432) để tránh Supabase pooler issues
// Supabase pooler (Port 6543) có thể gây ra ObjectDisposedException với Npgsql
var connectionStrings = new List<string>();
connectionStrings.AddRange(fallbacks.Where(c => c.Contains("Port=5432")));
// Tạm thời bỏ qua Supabase pooler để test
// connectionStrings.Add(defaultConn!);
// connectionStrings.AddRange(fallbacks.Where(c => !c.Contains("Port=5432")));

string? activeConnectionString = null;

if (!isTestingEnvironment)
{
Console.WriteLine("[DB-SMART] Đang dò tìm phương thức kết nối tối ưu cho Wifi hiện tại...");

foreach (var connStr in connectionStrings.Distinct())
{
    if (string.IsNullOrEmpty(connStr)) continue;
    
    try
    {
        bool useClientPooling = ShouldUseClientPooling(connStr);

        // Test connection with pooling ENABLED (same as production)
        // This ensures the pool is initialized correctly from the start
        var testBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connStr) 
        { 
            Timeout = 15, 
            CommandTimeout = 15,
            Pooling = useClientPooling
        };

        if (useClientPooling)
        {
            testBuilder.MinPoolSize = 0; // Don't pre-create connections for test
            testBuilder.MaxPoolSize = 2; // Limit test connections
        }
        
        using var testConn = new Npgsql.NpgsqlConnection(testBuilder.ConnectionString);
        await testConn.OpenAsync();
        
        activeConnectionString = connStr;

        var host = new Npgsql.NpgsqlConnectionStringBuilder(connStr).Host;
        var port = new Npgsql.NpgsqlConnectionStringBuilder(connStr).Port;
        Console.WriteLine($"[DB-SMART] THÀNH CÔNG: Đã kết nối qua {host}:{port}");
        break;
    }
    catch (Exception ex)
    {
        var host = new Npgsql.NpgsqlConnectionStringBuilder(connStr).Host;
        Console.WriteLine($"[DB-SMART] BỎ QUA: Không thể tới {host} trên mạng này. ({ex.Message.Split(':')[0]})");
    }
}

if (string.IsNullOrEmpty(activeConnectionString))
{
    Console.WriteLine("[DB-SMART] CẢNH BÁO: Không tìm thấy đường truyền tới Database! Ứng dụng sẽ dùng mặc định.");
    activeConnectionString = defaultConn ?? throw new InvalidOperationException("DefaultConnection is not configured.");
}
} // end if (!isTestingEnvironment)

// Bổ sung Command Timeout cho chuỗi kết nối chính thức
// (Skipped in Testing environment — AdminWebApplicationFactory replaces DbContext with InMemory)
if (!isTestingEnvironment)
{
    var finalConnBuilder = new Npgsql.NpgsqlConnectionStringBuilder(activeConnectionString);
    bool useClientPoolingForActiveConnection = ShouldUseClientPooling(activeConnectionString!);
    
    finalConnBuilder.CommandTimeout = 300; 
    finalConnBuilder.Timeout = 30; // Reduce connection timeout for faster failover
    finalConnBuilder.Pooling = useClientPoolingForActiveConnection;

    if (useClientPoolingForActiveConnection)
    {
        finalConnBuilder.MinPoolSize = 0;   
        finalConnBuilder.MaxPoolSize = 25; // Moderate pool size for student project
        finalConnBuilder.ConnectionIdleLifetime = 20;  // EXTRENELY IMPORTANT: Prune idle connections after 20s (Supabase usually 60s)
        finalConnBuilder.ConnectionPruningInterval = 10;
        
        finalConnBuilder.KeepAlive = 30; 
        finalConnBuilder.TcpKeepAlive = true; 
        
        Console.WriteLine("[DB-STABILITY] Client-side pooling ENABLED (Optimized for Supabase). Pool Size: 25, Idle Lifetime: 20s.");
    }
    else
    {
        finalConnBuilder.MinPoolSize = 0;
        finalConnBuilder.MaxPoolSize = 1;
        Console.WriteLine("[DB-STABILITY] Client pooling DISABLED. System may experience socket exhaustion under load.");
    }

    activeConnectionString = finalConnBuilder.ConnectionString;

    // DbContext with Scoped lifetime (standard for ASP.NET Core)
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(activeConnectionString, npgsqlOptions => {
            npgsqlOptions.CommandTimeout(300);
            npgsqlOptions.ExecutionStrategy(d => new NonRetryingExecutionStrategy(d));
        }));
}
else
{
    // In Testing environment, register a placeholder Npgsql DbContext.
    // AdminWebApplicationFactory will replace this with an InMemory database via ConfigureTestServices.
    // Use a dummy connection string — it will never actually be used.
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql("Host=localhost;Database=TestPlaceholder;Username=test;Password=test"));
}

// Authentication & Identity
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // Use Cookie as default challenge so unauthorized users are redirected to LoginPath
    // instead of being forced into Google OAuth (which breaks local/dev and admin flows).
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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

// PayOS Configuration Binding
builder.Services.Configure<VibeMusic.Application.Common.PayOSSettings>(builder.Configuration.GetSection("PayOS"));

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
builder.Services.AddScoped<ISongMetadataEnrichmentService, SongMetadataEnrichmentService>();
builder.Services.AddScoped<IArtistVerificationService, ArtistVerificationService>();
builder.Services.AddScoped<IViewCountService, ViewCountService>();
builder.Services.AddScoped<IExternalViewCountSyncService, ExternalViewCountSyncService>();

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
// Cache warming service - pre-populate trending/mood sections on startup
builder.Services.AddHostedService<CacheWarmupService>();

// External Services

// YouTube Service Refactored Architecture (Clean Architecture)
// Infrastructure-layer interfaces (YoutubeExplode-dependent)
builder.Services.AddSingleton<IYoutubeApiClient, YoutubeApiClient>();
builder.Services.AddSingleton<IYoutubeStreamResolver, YoutubeStreamResolver>();

// Application-layer interfaces (pure business logic — stateless)
builder.Services.AddSingleton<IMusicContentFilter, MusicContentFilter>();
builder.Services.AddSingleton<ITrackMetadataProcessor, TrackMetadataProcessor>();

// Search service — singleton (stateless, uses IMemoryCache internally)
builder.Services.AddSingleton<IYoutubeSearchService, YoutubeSearchService>();

// Enrichment service — scoped (depends on IDeezerService which is scoped via HttpClient)
builder.Services.AddScoped<IVideoEnrichmentService, VideoEnrichmentService>();

// Thin orchestrator — scoped (depends on IVideoEnrichmentService which is scoped)
builder.Services.AddScoped<IYoutubeService, YoutubeService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<ILyricsService, LyricsService>();
builder.Services.AddHttpClient<IDeezerService, DeezerService>();
builder.Services.AddHttpClient<IITunesService, ITunesService>();
builder.Services.AddHttpClient<IWikipediaService, WikipediaService>();
builder.Services.AddScoped<IAiAgentService, SemanticKernelAgentService>();
builder.Services.AddScoped<AiUserAccessGuard>();

var app = builder.Build();

// Runtime safety: ensure partial unique index exists for non-deleted YouTube IDs.
// This prevents duplicate inserts under concurrent import and reduces DB write contention.
// (Skipped in Testing environment — InMemory DB doesn't support raw SQL index creation)
if (!app.Environment.IsEnvironment("Testing"))
{
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ix_songs_youtubevideoid_active
            ON songs (youtubevideoid)
            WHERE is_deleted = false;
        ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB-INDEX] Could not ensure ix_songs_youtubevideoid_active: {ex.Message}");
    }
}
} // end if (!Testing)

// --- DATABASE INITIALIZATION & SEEDING (Opt-in via config flag) ---
// Enable by setting: SeedData:Enable=true (e.g., in appsettings.Development.json or user secrets)
if (builder.Configuration.GetValue<bool>("SeedData:Enable"))
{
    await DbInitializer.SeedAsync(app.Services);
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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VibeMusic API v1");
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

// Non-retrying execution strategy — prevents Npgsql 10 from retrying on a broken connector
// after cancellation, which causes ObjectDisposedException on ManualResetEventSlim.
internal class NonRetryingExecutionStrategy : ExecutionStrategy
{
    public NonRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, 1, TimeSpan.Zero) { }

    protected override bool ShouldRetryOn(Exception exception) => false;
}
