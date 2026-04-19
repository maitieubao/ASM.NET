using System.ComponentModel.DataAnnotations;

namespace YoutubeMusicPlayer.Application.DTOs;

public class GenreDto
{
    public int GenreId { get; set; }

    [Required(ErrorMessage = "Genre name is required.")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
