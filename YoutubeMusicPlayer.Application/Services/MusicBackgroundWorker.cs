using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class MusicBackgroundWorker : BackgroundService
{
    private readonly IBackgroundQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;

    public MusicBackgroundWorker(IBackgroundQueue taskQueue, IServiceProvider serviceProvider)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("[BackgroundWorker] Background Music Worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                // Each work item gets its own scope to avoid DbContext issues
                using (var scope = _serviceProvider.CreateScope())
                {
                    await workItem(scope.ServiceProvider);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BackgroundWorker] Task execution failed: {ex.Message}");
            }
        }

        Console.WriteLine("[BackgroundWorker] Background Music Worker stopping...");
    }
}
