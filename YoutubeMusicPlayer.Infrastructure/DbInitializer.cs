using System.Linq;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Infrastructure.Persistence;

namespace YoutubeMusicPlayer.Infrastructure;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.SubscriptionPlans.Any())
        {
            return; // DB has been seeded
        }

        var plans = new SubscriptionPlan[]
        {
            new SubscriptionPlan { Name = "Gói 1 Tháng", Price = 29000m, DurationDays = 30, Description = "Trải nghiệm âm nhạc không giới hạn trong 1 tháng." },
            new SubscriptionPlan { Name = "Gói 6 Tháng", Price = 149000m, DurationDays = 180, Description = "Tiết kiệm hơn với gói 6 tháng music premium." },
            new SubscriptionPlan { Name = "Gói 1 Năm", Price = 250000m, DurationDays = 365, Description = "Gói hời nhất, tận hưởng âm nhạc trọn vẹn 1 năm." }
        };

        context.SubscriptionPlans.AddRange(plans);
        context.SaveChanges();
    }
}
