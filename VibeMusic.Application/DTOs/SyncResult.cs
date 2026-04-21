namespace VibeMusic.Application.DTOs;
public class SyncResult
{
    public bool Success { get; set; }
    public long? NewViewCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SyncedAt { get; set; }
}
