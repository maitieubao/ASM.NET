using System;
using System.Threading.Tasks;

namespace VibeMusic.Application.Interfaces;

public interface IBackgroundQueue
{
    ValueTask QueueBackgroundWorkItemAsync(Func<IServiceProvider, System.Threading.CancellationToken, Task> workItem);
    Task<Func<IServiceProvider, System.Threading.CancellationToken, Task>> DequeueAsync(System.Threading.CancellationToken cancellationToken);
}
