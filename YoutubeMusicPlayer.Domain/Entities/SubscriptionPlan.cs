using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("subscription_plans")]
public class SubscriptionPlan
{
    [Key]
    [Column("planid")]
    public int PlanId { get; set; }

    [Required]
    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    [Column("duration_days")]
    public int DurationDays { get; set; }

    [Column("description")]
    public string? Description { get; set; }
}
