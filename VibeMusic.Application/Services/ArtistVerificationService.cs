using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;

namespace VibeMusic.Application.Services;

public class ArtistVerificationService : IArtistVerificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeezerService _deezerService;
    private readonly ILogger<ArtistVerificationService> _logger;

    // Ngưỡng tương đồng tối thiểu để coi là khớp (85%)
    private const double MinMatchScore = 0.85;

    public ArtistVerificationService(
        IUnitOfWork unitOfWork,
        IDeezerService deezerService,
        ILogger<ArtistVerificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _deezerService = deezerService;
        _logger = logger;
    }

    public async Task<ArtistVerificationResult> VerifyArtistAsync(int artistId, CancellationToken ct = default)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(artistId, ct);
        if (artist == null || artist.IsDeleted)
        {
            return new ArtistVerificationResult
            {
                ArtistId = artistId,
                Status = ArtistVerificationStatus.Failed,
                FailureReason = "Nghệ sĩ không tồn tại"
            };
        }

        _logger.LogInformation("Bắt đầu xác minh nghệ sĩ {ArtistId} ({Name})", artistId, artist.Name);

        try
        {
            // Tìm kiếm trên Deezer
            var deezerResults = await _deezerService.SearchArtistsAsync(artist.Name, 5);
            var deezerList = deezerResults?.ToList() ?? new List<DeezerArtistInfo>();

            // Tìm kết quả khớp tốt nhất
            var bestMatch = FindBestMatch(artist.Name, deezerList);

            ArtistVerificationResult result;

            if (bestMatch.score >= MinMatchScore)
            {
                // Xác minh thành công
                artist.VerificationStatus = ArtistVerificationStatus.Verified;
                artist.DeezerArtistId = bestMatch.artist.DeezerId;
                artist.VerifiedAt = DateTime.UtcNow;
                artist.IsVerified = true;

                // Đồng bộ avatar nếu chưa có
                if (string.IsNullOrEmpty(artist.AvatarUrl) && !string.IsNullOrEmpty(bestMatch.artist.ImageUrl))
                {
                    artist.AvatarUrl = bestMatch.artist.ImageUrl;
                    artist.BannerUrl = bestMatch.artist.ImageUrl;
                }

                result = new ArtistVerificationResult
                {
                    ArtistId = artistId,
                    ArtistName = artist.Name,
                    Status = ArtistVerificationStatus.Verified,
                    DeezerArtistId = bestMatch.artist.DeezerId,
                    DeezerArtistName = bestMatch.artist.Name,
                    DeezerImageUrl = bestMatch.artist.ImageUrl,
                    MatchScore = bestMatch.score
                };

                _logger.LogInformation(
                    "Xác minh thành công nghệ sĩ {ArtistId} ({Name}) - Deezer: {DeezerName} (score: {Score:P0})",
                    artistId, artist.Name, bestMatch.artist.Name, bestMatch.score);
            }
            else if (deezerList.Count == 0)
            {
                // Không tìm thấy trên Deezer
                artist.VerificationStatus = ArtistVerificationStatus.Unverified;
                artist.IsVerified = false;

                result = new ArtistVerificationResult
                {
                    ArtistId = artistId,
                    ArtistName = artist.Name,
                    Status = ArtistVerificationStatus.Unverified,
                    MatchScore = 0,
                    FailureReason = "Không tìm thấy trên Deezer"
                };

                _logger.LogInformation("Không tìm thấy nghệ sĩ {ArtistId} ({Name}) trên Deezer", artistId, artist.Name);
            }
            else
            {
                // Tìm thấy nhưng không đủ độ tương đồng
                artist.VerificationStatus = ArtistVerificationStatus.Unverified;
                artist.IsVerified = false;

                result = new ArtistVerificationResult
                {
                    ArtistId = artistId,
                    ArtistName = artist.Name,
                    Status = ArtistVerificationStatus.Unverified,
                    MatchScore = bestMatch.score,
                    FailureReason = $"Độ tương đồng thấp ({bestMatch.score:P0} < {MinMatchScore:P0})"
                };

                _logger.LogInformation(
                    "Nghệ sĩ {ArtistId} ({Name}) không đủ độ tương đồng: {Score:P0}",
                    artistId, artist.Name, bestMatch.score);
            }

            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync(ct);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xác minh nghệ sĩ {ArtistId} ({Name})", artistId, artist.Name);

            artist.VerificationStatus = ArtistVerificationStatus.Failed;
            _unitOfWork.Repository<Artist>().Update(artist);
            await _unitOfWork.CompleteAsync(ct);

            return new ArtistVerificationResult
            {
                ArtistId = artistId,
                ArtistName = artist.Name,
                Status = ArtistVerificationStatus.Failed,
                FailureReason = ex.Message
            };
        }
    }

    public async Task<IEnumerable<ArtistVerificationResult>> VerifyBatchAsync(int batchSize = 50, CancellationToken ct = default)
    {
        // Lấy các nghệ sĩ chưa xác minh (Pending hoặc Failed)
        var artists = await _unitOfWork.Repository<Artist>().Query()
            .Where(a => !a.IsDeleted &&
                       (a.VerificationStatus == ArtistVerificationStatus.Pending ||
                        a.VerificationStatus == ArtistVerificationStatus.Failed))
            .OrderBy(a => a.ArtistId)
            .Take(batchSize)
            .ToListAsync(ct);

        _logger.LogInformation("Bắt đầu xác minh hàng loạt {Count} nghệ sĩ", artists.Count);

        var results = new List<ArtistVerificationResult>();

        foreach (var artist in artists)
        {
            if (ct.IsCancellationRequested) break;

            var result = await VerifyArtistAsync(artist.ArtistId, ct);
            results.Add(result);

            // Delay nhỏ để tránh quá tải Deezer API
            await Task.Delay(200, ct);
        }

        var verifiedCount = results.Count(r => r.Status == ArtistVerificationStatus.Verified);
        _logger.LogInformation(
            "Hoàn thành xác minh hàng loạt: {Verified}/{Total} nghệ sĩ được xác minh",
            verifiedCount, results.Count);

        return results;
    }

    public async Task<VerificationStatsDto> GetVerificationStatsAsync(CancellationToken ct = default)
    {
        var stats = await _unitOfWork.Repository<Artist>().Query()
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.VerificationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = stats.Sum(s => s.Count);

        return new VerificationStatsDto
        {
            TotalArtists = total,
            VerifiedCount = stats.FirstOrDefault(s => s.Status == ArtistVerificationStatus.Verified)?.Count ?? 0,
            UnverifiedCount = stats.FirstOrDefault(s => s.Status == ArtistVerificationStatus.Unverified)?.Count ?? 0,
            PendingCount = stats.FirstOrDefault(s => s.Status == ArtistVerificationStatus.Pending)?.Count ?? 0,
            FailedCount = stats.FirstOrDefault(s => s.Status == ArtistVerificationStatus.Failed)?.Count ?? 0
        };
    }

    public async Task ResetVerificationAsync(int artistId, CancellationToken ct = default)
    {
        var artist = await _unitOfWork.Repository<Artist>().GetByIdAsync(artistId, ct);
        if (artist == null || artist.IsDeleted) return;

        artist.VerificationStatus = ArtistVerificationStatus.Pending;
        artist.DeezerArtistId = null;
        artist.VerifiedAt = null;
        artist.IsVerified = false;

        _unitOfWork.Repository<Artist>().Update(artist);
        await _unitOfWork.CompleteAsync(ct);

        _logger.LogInformation("Đặt lại trạng thái xác minh cho nghệ sĩ {ArtistId}", artistId);
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private (DeezerArtistInfo artist, double score) FindBestMatch(string artistName, List<DeezerArtistInfo> candidates)
    {
        if (candidates.Count == 0)
            return (new DeezerArtistInfo(), 0);

        var normalizedInput = NormalizeName(artistName);
        var inputVariants = GetNameVariants(artistName);

        DeezerArtistInfo? bestArtist = null;
        double bestScore = 0;

        foreach (var candidate in candidates)
        {
            var normalizedCandidate = NormalizeName(candidate.Name);

            // Tính điểm tương đồng
            double score = CalculateSimilarity(normalizedInput, normalizedCandidate);

            // Thử các biến thể tên
            foreach (var variant in inputVariants)
            {
                var variantScore = CalculateSimilarity(NormalizeName(variant), normalizedCandidate);
                score = Math.Max(score, variantScore);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestArtist = candidate;
            }
        }

        return (bestArtist ?? candidates[0], bestScore);
    }

    /// <summary>Chuẩn hóa tên: lowercase, bỏ dấu tiếng Việt, bỏ ký tự đặc biệt</summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        // Lowercase
        var result = name.ToLowerInvariant().Trim();

        // Bỏ dấu tiếng Việt
        result = RemoveDiacritics(result);

        // Bỏ ký tự đặc biệt, chỉ giữ chữ cái, số và khoảng trắng
        result = Regex.Replace(result, @"[^a-z0-9\s]", " ");

        // Chuẩn hóa khoảng trắng
        result = Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    private static string RemoveDiacritics(string text)
    {
        // Map tiếng Việt phổ biến
        var map = new Dictionary<string, string>
        {
            {"à|á|ả|ã|ạ|ă|ắ|ặ|ằ|ẳ|ẵ|â|ấ|ầ|ẩ|ẫ|ậ", "a"},
            {"è|é|ẻ|ẽ|ẹ|ê|ế|ề|ể|ễ|ệ", "e"},
            {"ì|í|ỉ|ĩ|ị", "i"},
            {"ò|ó|ỏ|õ|ọ|ô|ố|ồ|ổ|ỗ|ộ|ơ|ớ|ờ|ở|ỡ|ợ", "o"},
            {"ù|ú|ủ|ũ|ụ|ư|ứ|ừ|ử|ữ|ự", "u"},
            {"ỳ|ý|ỷ|ỹ|ỵ", "y"},
            {"đ", "d"}
        };

        foreach (var entry in map)
        {
            foreach (var ch in entry.Key.Split('|'))
            {
                text = text.Replace(ch, entry.Value);
            }
        }

        // Xử lý các ký tự Latin có dấu còn lại bằng Unicode normalization
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Tạo các biến thể tên để thử khớp</summary>
    private static List<string> GetNameVariants(string name)
    {
        var variants = new List<string> { name };

        // Bỏ các từ phổ biến không cần thiết
        var withoutSuffix = Regex.Replace(name, @"\s*(official|music|vevo|channel|tv|records?)\s*$", "", RegexOptions.IgnoreCase).Trim();
        if (withoutSuffix != name) variants.Add(withoutSuffix);

        // Bỏ dấu tiếng Việt
        var withoutDiacritics = RemoveDiacritics(name.ToLowerInvariant());
        if (withoutDiacritics != name.ToLowerInvariant()) variants.Add(withoutDiacritics);

        return variants;
    }

    /// <summary>Tính độ tương đồng Jaro-Winkler giữa hai chuỗi (0.0 - 1.0)</summary>
    private static double CalculateSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

        // Exact match sau normalize
        if (s1.Equals(s2, StringComparison.OrdinalIgnoreCase)) return 1.0;

        // Contains check (một cái chứa cái kia)
        if (s1.Contains(s2) || s2.Contains(s1))
        {
            var shorter = Math.Min(s1.Length, s2.Length);
            var longer = Math.Max(s1.Length, s2.Length);
            return (double)shorter / longer * 0.95; // Slight penalty for partial match
        }

        // Jaro similarity
        return JaroSimilarity(s1, s2);
    }

    private static double JaroSimilarity(string s1, string s2)
    {
        if (s1.Length == 0 && s2.Length == 0) return 1.0;

        int matchDistance = Math.Max(s1.Length, s2.Length) / 2 - 1;
        if (matchDistance < 0) matchDistance = 0;

        var s1Matches = new bool[s1.Length];
        var s2Matches = new bool[s2.Length];

        int matches = 0;
        int transpositions = 0;

        for (int i = 0; i < s1.Length; i++)
        {
            int start = Math.Max(0, i - matchDistance);
            int end = Math.Min(i + matchDistance + 1, s2.Length);

            for (int j = start; j < end; j++)
            {
                if (s2Matches[j] || s1[i] != s2[j]) continue;
                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        int k = 0;
        for (int i = 0; i < s1.Length; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (s1[i] != s2[k]) transpositions++;
            k++;
        }

        double jaro = ((double)matches / s1.Length +
                       (double)matches / s2.Length +
                       (double)(matches - transpositions / 2) / matches) / 3;

        // Jaro-Winkler prefix bonus
        int prefix = 0;
        for (int i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
        {
            if (s1[i] == s2[i]) prefix++;
            else break;
        }

        return jaro + prefix * 0.1 * (1 - jaro);
    }
}
