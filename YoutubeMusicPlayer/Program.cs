using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Services;
using YoutubeMusicPlayer.Domain.Interfaces;
using YoutubeMusicPlayer.Infrastructure;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Infrastructure.External;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Performance Optimization: Memory Cache
builder.Services.AddMemoryCache();

// Performance// 2. Database (Tối ưu hóa tốc độ bằng pooling và giảm độ trễ DNS)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions => {
        npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
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
builder.Services.AddScoped<IPlaylistService, PlaylistService>();
builder.Services.AddScoped<IPayOSService, PayOSService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// External Services
builder.Services.AddScoped<IYoutubeService, YoutubeService>();
builder.Services.AddScoped<IWikipediaService, WikipediaService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Thêm hệ thống giám sát Request
app.Use(async (context, next) =>
{
    Console.WriteLine($"[MONITOR] Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

// app.UseHttpsRedirection(); // Tạm tắt để tránh lỗi chuyển hướng sai port
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
