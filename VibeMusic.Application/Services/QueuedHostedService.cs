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
            _logger.LogInformation("Waiting for next background work item...");
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            // Retry logic for transient failures (e.g. DB connection)
            int retryCount = 0;
            const int maxRetries = 3;
            bool success = false;

            while (!success && retryCount <= maxRetries && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting execution of background task (Attempt {Attempt}).", retryCount + 1);

                    using var scope = _serviceProvider.CreateScope();
                    await workItem(scope.ServiceProvider, stoppingToken);
                    
                    success = true;
                    _logger.LogInformation("Finished execution of background task.");
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogError(ex, "Error executing background task (Attempt {Attempt}/{Max}).", retryCount, maxRetries + 1);
                    
                    if (retryCount <= maxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff: 2s, 4s, 8s
                        _logger.LogInformation("Retrying in {Delay}s...", delay.TotalSeconds);
                        await Task.Delay(delay, stoppingToken);
                    }
                }
            }
        }

        _logger.LogInformation("Queued Hosted Service is stopping.");
    }
}
