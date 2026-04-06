using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId, int count = 20)
    {
        // Optimized: SQL-level processing (.Select) to avoid loading all notifications into RAM
        return await _unitOfWork.Repository<Notification>().Query()
            .AsNoTracking()
            .Where(n => n.UserId == userId || n.UserId == null)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(notificationId);
        if (n != null)
        {
            n.IsRead = true;
            _unitOfWork.Repository<Notification>().Update(n);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task SendSystemNotificationAsync(string title, string message, string type = NotificationTypes.System)
    {
        var n = new Notification
        {
            UserId = null,
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Notification>().AddAsync(n);
        await _unitOfWork.CompleteAsync();
        
        // SignalR Placeholder: Send message across SignalR Hub if configured
    }

    public async Task SendUserNotificationAsync(int userId, string title, string message, string type = NotificationTypes.StatusChange)
    {
         var n = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Notification>().AddAsync(n);
        await _unitOfWork.CompleteAsync();

        // SignalR Placeholder: Send message across SignalR Hub if configured
    }

    public async Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync(int count = 50)
    {
        // Optimized: Single SQL join for user names to avoid N+1 and loading all notifications into RAM
        return await _unitOfWork.Repository<Notification>().Query()
            .AsNoTracking()
            .Include(n => n.User)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                UserName = n.User != null ? n.User.Username : "Global",
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();
    }

    public async Task DeleteNotificationAsync(int notificationId)
    {
        var n = await _unitOfWork.Repository<Notification>().GetByIdAsync(notificationId);
        if (n != null)
        {
            _unitOfWork.Repository<Notification>().Remove(n);
            await _unitOfWork.CompleteAsync();
        }
    }
}
