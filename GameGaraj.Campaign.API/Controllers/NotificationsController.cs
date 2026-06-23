using GameGaraj.Campaign.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Campaign.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUserId(string userId, [FromQuery] bool unreadOnly = false)
        {
            var notifications = await _notificationService.GetByUserIdAsync(userId, unreadOnly);
            return Ok(notifications);
        }

        [HttpGet("user/{userId}/unreadcount")]
        public async Task<IActionResult> GetUnreadCount(string userId)
        {
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(count);
        }

        [HttpPost("{id}/markread")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return NoContent();
        }

        [HttpPost("user/{userId}/markallread")]
        public async Task<IActionResult> MarkAllAsRead(string userId)
        {
            await _notificationService.MarkAllAsReadAsync(userId);
            return NoContent();
        }
    }
}
