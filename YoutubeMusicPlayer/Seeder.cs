using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Infrastructure.Persistence;
using YoutubeMusicPlayer.Domain.Entities;
using System;
using System.Linq;
using BCrypt.Net;

namespace YoutubeMusicPlayer.Testing 
{
    public class Seeder 
    {
        public static void Seed() 
        {
            var contextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=aws-1-ap-south-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.djesziteeraxenpdhybn;Password=MaiTieuBao1234!@#$;SSL Mode=Require;Trust Server Certificate=true;Pooling=true;")
                .Options;

            using var db = new AppDbContext(contextOptions);
            
            // 1. Seed Admin
            var admin = db.Users.FirstOrDefault(u => u.Email == "admin@musi.ca");
            if (admin == null) {
                admin = new User {
                    Username = "MasterAdmin",
                    Email = "admin@musi.ca",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!"),
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(admin);
                Console.WriteLine("Created Admin: admin@musi.ca");
            } else {
                admin.Role = "Admin"; 
                admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!");
                Console.WriteLine("Updated Admin: admin@musi.ca");
            }

            // 2. Seed Minor User
            var minor = db.Users.FirstOrDefault(u => u.Email == "minor@test.com");
            if (minor == null) {
                minor = new User {
                    Username = "YoungListener",
                    Email = "minor@test.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234!"),
                    Role = "Customer",
                    CreatedAt = DateTime.UtcNow,
                    DateOfBirth = new DateTime(2012, 1, 1).ToUniversalTime()
                };
                db.Users.Add(minor);
                Console.WriteLine("Created Minor User: minor@test.com");
            } else {
                minor.DateOfBirth = new DateTime(2012, 1, 1).ToUniversalTime();
                minor.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234!");
                Console.WriteLine("Updated Minor User: minor@test.com");
            }

            db.SaveChanges();
            Console.WriteLine("Done.");
        }
    }
}
