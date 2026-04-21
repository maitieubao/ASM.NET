using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using VibeMusic.Application.Common;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace VibeMusic.Controllers;

[Authorize(Roles = UserRoles.Admin)]
[Route("Admin/ArtistVerification")]
public class AdminArtistVerificationController : BaseController
{
    private readonly IArtistVerificationService _verificationService;
    private readonly IArtistService _artistService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminArtistVerificationController(
        IArtistVerificationService verificationService,
        IArtistService artistService,
        IUnitOfWork unitOfWork)
    {
        _verificationService = verificationService;
        _artistService = artistService;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var stats = await _verificationService.GetVerificationStatsAsync();
        
        // Lấy danh sách nghệ sĩ với trạng thái xác minh (tất cả, không lọc)
        var artists = await _unitOfWork.Repository<Artist>().Query()
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.VerificationStatus)
            .ThenBy(a => a.Name)
            .Select(a => new
            {
                a.ArtistId,
                a.Name,
                a.AvatarUrl,
                a.VerificationStatus,
                a.DeezerArtistId,
                a.VerifiedAt,
                a.IsVerified
            })
            .ToListAsync();

        ViewBag.Stats = stats;
        ViewBag.Artists = artists;
        return View();
    }

    [HttpPost]
    [Route("Verify/{artistId}")]
    public async Task<IActionResult> VerifyArtist(int artistId)
    {
        var result = await _verificationService.VerifyArtistAsync(artistId);
        return SuccessResponse(new
        {
            success = result.Status == ArtistVerificationStatus.Verified,
            status = result.Status.ToString(),
            artistName = result.ArtistName,
            deezerArtistId = result.DeezerArtistId,
            deezerArtistName = result.DeezerArtistName,
            matchScore = $"{result.MatchScore:P0}",
            message = result.Status == ArtistVerificationStatus.Verified
                ? $"✅ Đã xác minh: {result.ArtistName} khớp với '{result.DeezerArtistName}' trên Deezer ({result.MatchScore:P0})"
                : $"❌ Không xác minh: {result.FailureReason}"
        });
    }

    [HttpPost]
    [Route("VerifyBatch")]
    public async Task<IActionResult> VerifyBatch(int batchSize = 50)
    {
        if (batchSize < 1 || batchSize > 100)
            return BadRequest("Batch size phải từ 1 đến 100.");

        var results = await _verificationService.VerifyBatchAsync(batchSize);
        var resultList = results.ToList();

        var verifiedCount = resultList.Count(r => r.Status == ArtistVerificationStatus.Verified);
        var unverifiedCount = resultList.Count(r => r.Status == ArtistVerificationStatus.Unverified);
        var failedCount = resultList.Count(r => r.Status == ArtistVerificationStatus.Failed);

        return SuccessResponse(new
        {
            success = true,
            message = $"Hoàn thành xác minh hàng loạt: {verifiedCount} xác minh, {unverifiedCount} không xác minh, {failedCount} lỗi",
            verifiedCount,
            unverifiedCount,
            failedCount,
            totalProcessed = resultList.Count
        });
    }

    [HttpPost]
    [Route("Reset/{artistId}")]
    public async Task<IActionResult> ResetVerification(int artistId)
    {
        await _verificationService.ResetVerificationAsync(artistId);
        return SuccessResponse(new
        {
            success = true,
            message = "Đã đặt lại trạng thái xác minh về Pending"
        });
    }

    [HttpGet]
    [Route("Stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _verificationService.GetVerificationStatsAsync();
        return SuccessResponse(stats);
    }
}
