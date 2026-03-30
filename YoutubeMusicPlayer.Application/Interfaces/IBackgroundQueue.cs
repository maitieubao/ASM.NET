using System;
using System.Threading.Tasks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IBackgroundQueue
{
    void QueueBackgroundWorkItem(Func<IServiceProvider, Task> workItem);
    Task<Func<IServiceProvider, Task>> DequeueAsync(System.Threading.CancellationToken cancellationToken);
}
