using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("user_subscriptions")]
public class UserSubscription
{
    [Key]
    [Column("user_subscription_id")]
    public int UserSubscriptionId { get; set; }

    [Column("userid")]
    public int UserId { get; set; }

    [Column("planid")]
    public int PlanId { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("PlanId")]
    public SubscriptionPlan? Plan { get; set; }
}
