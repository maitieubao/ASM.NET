using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public async Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId)
    {
        var notifications = await _unitOfWork.Repository<Notification>().FindAsync(n => n.UserId == userId || n.UserId == null);
        return notifications.OrderByDescending(n => n.CreatedAt).Select(n => new NotificationDto
        {
            NotificationId = n.NotificationId,
            UserId = n.UserId,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
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

    public async Task SendSystemNotificationAsync(string title, string message, string type = "System")
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
    }

    public async Task SendUserNotificationAsync(int userId, string title, string message, string type = "StatusChange")
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
    }

    public async Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync()
    {
        var notifications = await _unitOfWork.Repository<Notification>().GetAllAsync();
        var result = new List<NotificationDto>();

        foreach (var n in notifications.OrderByDescending(x => x.CreatedAt))
        {
            var user = n.UserId.HasValue ? await _unitOfWork.Repository<User>().GetByIdAsync(n.UserId.Value) : null;
            result.Add(new NotificationDto
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                UserName = user?.Username ?? "Global",
                Title = n.Title,
                Message = n.Message,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            });
        }
        return result;
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
