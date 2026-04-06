using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(IBackgroundQueue taskQueue, IServiceProvider serviceProvider, ILogger<QueuedHostedService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queued Hosted Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                _logger.LogInformation("Executing background task.");

                using var scope = _serviceProvider.CreateScope();
                await workItem(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing background task.");
            }
        }

        _logger.LogInformation("Queued Hosted Service is stopping.");
    }
}
