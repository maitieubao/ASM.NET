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
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Fix PostgreSQL DateTime issue (Enable legacy timestamp behavior)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Performance Optimization: Memory Cache
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

// --- SMART DATABASE CONNECTION DISCOVERY (Thích nghi mọi loại Wifi) ---
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
var fallbacks = builder.Configuration.GetSection("ConnectionStrings:ConnectionStringsFallback").Get<string[]>() ?? Array.Empty<string>();
var connectionStrings = new List<string> { defaultConn! };
connectionStrings.AddRange(fallbacks);

string? activeConnectionString = null;
Console.WriteLine("[DB-SMART] Đang dò tìm phương thức kết nối tối ưu cho Wifi hiện tại...");

foreach (var connStr in connectionStrings.Distinct())
{
    if (string.IsNullOrEmpty(connStr)) continue;
    
    try
    {
        // Thử kết nối nhanh (timeout 4s)
        var connectionBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connStr) 
        { 
            Timeout = 4, 
            CommandTimeout = 4,
            Pooling = false 
        };
        
        using var testConn = new Npgsql.NpgsqlConnection(connectionBuilder.ConnectionString);
        await testConn.OpenAsync();
        
        activeConnectionString = connStr;
        
        // 1. CLEAR ALL POOLS & LOG STARTUP
        NpgsqlConnection.ClearAllPools();
        Console.WriteLine("[STARTUP] Database connection pools cleared.");

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
    activeConnectionString = defaultConn; 
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(activeConnectionString, npgsqlOptions => {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

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
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<ILyricsService, LyricsService>();
builder.Services.AddHttpClient<IDeezerService, DeezerService>();
builder.Services.AddHttpClient<IITunesService, ITunesService>();
builder.Services.AddHttpClient<IWikipediaService, WikipediaService>();
builder.Services.AddScoped<IAiAgentService, SemanticKernelAgentService>();

var app = builder.Build();

// --- DATABASE INITIALIZATION & SEEDING (Disabled due to Npgsql/Supavisor race condition in .NET 10) ---
// await DbInitializer.SeedAsync(app.Services);

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
