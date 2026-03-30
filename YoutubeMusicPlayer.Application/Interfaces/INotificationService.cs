using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface INotificationService
{
    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId);
    Task MarkAsReadAsync(int notificationId);
    
    // Admin features
    Task SendSystemNotificationAsync(string title, string message, string type = "System");
    Task SendUserNotificationAsync(int userId, string title, string message, string type = "StatusChange");
    Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync();
    Task DeleteNotificationAsync(int notificationId);
}
