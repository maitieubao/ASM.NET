using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("categories")]
public class Category
{
    [Key]
    [Column("categoryid")]
    public int CategoryId { get; set; }

    [Required]
    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("createdat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Song> Songs { get; set; } = new List<Song>();
}
