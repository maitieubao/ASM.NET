using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface INotificationService
{
    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId, int count = 20);
    Task MarkAsReadAsync(int notificationId);
    
    // Admin features
    Task SendSystemNotificationAsync(string title, string message, string type = NotificationTypes.System);
    Task SendUserNotificationAsync(int userId, string title, string message, string type = NotificationTypes.StatusChange);
    Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync(int count = 50);
    Task DeleteNotificationAsync(int notificationId);
}
