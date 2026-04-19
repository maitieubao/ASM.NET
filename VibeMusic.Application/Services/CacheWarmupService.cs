using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

/// <summary>
/// FIX PERF: Startup cache warming service.
/// Pre-populate các section phổ biến (trending, mood music) khi app khởi động
/// để request đầu tiên của user không phải chờ YouTube API.
/// </summary>
public class CacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CacheWarmupService> _logger;

    public CacheWarmupService(IServiceProvider serviceProvider, ILogger<CacheWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ app khởi động xong (DB connection, DI setup)
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        _logger.LogInformation("[CacheWarmup] Starting background cache warming...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var homeFacade = scope.ServiceProvider.GetRequiredService<IHomeFacade>();

            // Warm các section không phụ thuộc user (shared sections) song song
            var warmupTasks = new[]
            {
                WarmSectionAsync(homeFacade, "trending", stoppingToken),
                WarmSectionAsync(homeFacade, "focus", stoppingToken),
                WarmSectionAsync(homeFacade, "chill", stoppingToken),
                WarmSectionAsync(homeFacade, "compilations", stoppingToken),
            };

            await Task.WhenAll(warmupTasks);

            // Warm albums sau (phụ thuộc Deezer, chạy riêng để tránh rate limit)
            await WarmSectionAsync(homeFacade, "albums", stoppingToken);

            _logger.LogInformation("[CacheWarmup] Cache warming completed successfully.");
        }
        catch (OperationCanceledException)
        {
            // App đang shutdown — bình thường
        }
        catch (Exception ex)
        {
            // Không throw — warmup failure không được crash app
            _logger.LogWarning(ex, "[CacheWarmup] Cache warming failed (non-critical). App will continue normally.");
        }
    }

    private async Task WarmSectionAsync(IHomeFacade homeFacade, string sectionType, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("[CacheWarmup] Warming section: {Section}", sectionType);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await homeFacade.GetHomeSectionAsync(sectionType, null, false);
            sw.Stop();
            _logger.LogInformation("[CacheWarmup] Section '{Section}' warmed in {Ms}ms", sectionType, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CacheWarmup] Failed to warm section '{Section}': {Msg}", sectionType, ex.Message);
        }
    }
}
