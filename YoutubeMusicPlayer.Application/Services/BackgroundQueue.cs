using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class BackgroundQueue : IBackgroundQueue
{
    private readonly Channel<Func<IServiceProvider, Task>> _queue;

    public BackgroundQueue()
    {
        // Simple bounded channel to avoid memory leaks if tasks pile up
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<IServiceProvider, Task>>(options);
    }

    public void QueueBackgroundWorkItem(Func<IServiceProvider, Task> workItem)
    {
        if (workItem == null) throw new ArgumentNullException(nameof(workItem));
        _queue.Writer.TryWrite(workItem);
    }

    public async Task<Func<IServiceProvider, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
