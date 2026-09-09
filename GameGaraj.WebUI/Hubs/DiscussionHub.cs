using Microsoft.AspNetCore.SignalR;

namespace GameGaraj.WebUI.Hubs;

public class DiscussionHub : Hub
{
    private readonly ILogger<DiscussionHub> _logger;

    public DiscussionHub(ILogger<DiscussionHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinProductGroup(string productId)
    {
        if (!string.IsNullOrWhiteSpace(productId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"product-{productId}");
            _logger.LogInformation("[WebUI DiscussionHub] Connection {ConnectionId} joined product-{ProductId}", Context.ConnectionId, productId);
        }
    }

    public async Task LeaveProductGroup(string productId)
    {
        if (!string.IsNullOrWhiteSpace(productId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"product-{productId}");
            _logger.LogInformation("[WebUI DiscussionHub] Connection {ConnectionId} left product-{ProductId}", Context.ConnectionId, productId);
        }
    }

    public async Task JoinUserGroup(string? requestedUserId = null)
    {
        var currentUserId = Context.User?.FindFirst("sub")?.Value 
            ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? Context.UserIdentifier;

        // Güvenlik Koruması: Kullanıcı yalnızca kendi user-id grubuna katılabilir
        var targetUserId = !string.IsNullOrWhiteSpace(currentUserId) ? currentUserId : requestedUserId;
        if (!string.IsNullOrWhiteSpace(targetUserId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{targetUserId}");
            _logger.LogInformation("[WebUI DiscussionHub] User Connection {ConnectionId} joined user-{UserId}", Context.ConnectionId, targetUserId);
        }
    }

    public async Task LeaveUserGroup(string? requestedUserId = null)
    {
        var currentUserId = Context.User?.FindFirst("sub")?.Value 
            ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? Context.UserIdentifier;

        var targetUserId = !string.IsNullOrWhiteSpace(currentUserId) ? currentUserId : requestedUserId;
        if (!string.IsNullOrWhiteSpace(targetUserId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{targetUserId}");
        }
    }

    public async Task JoinAdminGroup()
    {
        var isAdmin = Context.User?.IsInRole("admin") == true 
            || Context.User?.HasClaim(c => (c.Type == "role" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") && string.Equals(c.Value, "admin", StringComparison.OrdinalIgnoreCase)) == true;

        if (!isAdmin)
        {
            _logger.LogWarning("[WebUI DiscussionHub] Unauthorized JoinAdminGroup attempt rejected for connection {ConnectionId}", Context.ConnectionId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, "admin-discussions");
        _logger.LogInformation("[WebUI DiscussionHub] Admin Connection {ConnectionId} joined admin-discussions", Context.ConnectionId);
    }

    public async Task LeaveAdminGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin-discussions");
    }
}
