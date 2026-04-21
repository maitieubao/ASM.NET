using System.Threading;
using System.Threading.Tasks;
using VibeMusic.Application.Common;
using VibeMusic.Application.DTOs;

namespace VibeMusic.Application.Interfaces;

public interface INotificationService
{
    Task<IEnumerable<NotificationDto>> GetUserNotificationsAsync(int userId, int count = 20, CancellationToken ct = default);
    Task MarkAsReadAsync(int notificationId, CancellationToken ct = default);
    
    // Admin features
    Task SendSystemNotificationAsync(string title, string message, string type = NotificationTypes.System, CancellationToken ct = default);
    Task SendUserNotificationAsync(int userId, string title, string message, string type = NotificationTypes.StatusChange, CancellationToken ct = default);
    Task<IEnumerable<NotificationDto>> GetAllNotificationsAsync(int count = 50, CancellationToken ct = default);
    Task DeleteNotificationAsync(int notificationId, CancellationToken ct = default);
}
