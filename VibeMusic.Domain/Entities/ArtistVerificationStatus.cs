namespace VibeMusic.Domain.Entities;

/// <summary>
/// Trạng thái xác minh nghệ sĩ dựa trên metadata từ các nền tảng âm nhạc chính thức.
/// </summary>
public enum ArtistVerificationStatus
{
    /// <summary>Chưa được xác minh (mặc định khi tạo mới)</summary>
    Pending = 0,

    /// <summary>Đã xác minh - tìm thấy trên Deezer/Spotify</summary>
    Verified = 1,

    /// <summary>Không tìm thấy trên bất kỳ nền tảng âm nhạc nào</summary>
    Unverified = 2,

    /// <summary>Xác minh thất bại do lỗi API</summary>
    Failed = 3
}
