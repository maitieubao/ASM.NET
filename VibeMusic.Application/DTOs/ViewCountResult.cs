using VibeMusic.Domain.Entities;
namespace VibeMusic.Application.DTOs;
public class ViewCountResult
{
    public long ViewCount { get; set; }
    public ViewCountSource Source { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsFromCache { get; set; }
}
