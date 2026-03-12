using System.Threading.Tasks;
using LibrarySystemBBU.Models;

namespace LibrarySystemBBU.Services
{
    public interface INotificationService
    {
        Task SendAsync(string eventType, string title, string? message = null, string? url = null);
    }
}