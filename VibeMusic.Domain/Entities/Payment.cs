using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoutubeMusicPlayer.Domain.Entities;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("paymentid")]
    public int PaymentId { get; set; }

    [Column("userid")]
    public int UserId { get; set; }

    [Column("planid")]
    public int PlanId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("payment_date")]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed, Cancelled

    [Column("order_code")]
    public long OrderCode { get; set; } // PayOS orderCode is long/int64

    [Column("payos_transaction_id")]
    public string? PayosTransactionId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("PlanId")]
    public SubscriptionPlan? Plan { get; set; }
}
