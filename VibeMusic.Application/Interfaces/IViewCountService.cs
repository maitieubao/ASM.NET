using VibeMusic.Application.DTOs;
using VibeMusic.Domain.Entities;
namespace VibeMusic.Application.Interfaces;
public interface IViewCountService
{
    Task<ViewCountResult> GetViewCountAsync(int songId, CancellationToken ct = default);
    Task UpdatePrioritySourceAsync(int songId, ViewCountSource? source, CancellationToken ct = default);
}
