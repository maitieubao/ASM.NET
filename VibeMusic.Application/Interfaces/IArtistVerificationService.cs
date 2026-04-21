using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VibeMusic.Domain.Entities;

namespace VibeMusic.Application.Interfaces;

public class ArtistVerificationResult
{
    public int ArtistId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public ArtistVerificationStatus Status { get; set; }
    public string? DeezerArtistId { get; set; }
    public string? DeezerArtistName { get; set; }
    public string? DeezerImageUrl { get; set; }
    public double MatchScore { get; set; }
    public string? FailureReason { get; set; }
}

public class VerificationStatsDto
{
    public int TotalArtists { get; set; }
    public int VerifiedCount { get; set; }
    public int UnverifiedCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public double VerificationRate => TotalArtists > 0 ? (double)VerifiedCount / TotalArtists * 100 : 0;
}

public interface IArtistVerificationService
{
    /// <summary>Xác minh một nghệ sĩ qua Deezer API</summary>
    Task<ArtistVerificationResult> VerifyArtistAsync(int artistId, CancellationToken ct = default);

    /// <summary>Xác minh hàng loạt nghệ sĩ chưa được xác minh</summary>
    Task<IEnumerable<ArtistVerificationResult>> VerifyBatchAsync(int batchSize = 50, CancellationToken ct = default);

    /// <summary>Lấy thống kê xác minh</summary>
    Task<VerificationStatsDto> GetVerificationStatsAsync(CancellationToken ct = default);

    /// <summary>Đặt lại trạng thái xác minh về Pending để xác minh lại</summary>
    Task ResetVerificationAsync(int artistId, CancellationToken ct = default);
}
