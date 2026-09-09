using Microsoft.AspNetCore.SignalR;

namespace GameGaraj.Discussion.API.Hubs;

public class DiscussionHub : Hub
{
    private readonly ILogger<DiscussionHub> _logger;

    public DiscussionHub(ILogger<DiscussionHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client joins a product's Q&A group to receive real-time updates.
    /// </summary>
    public async Task JoinProductGroup(string productId)
    {
        if (!string.IsNullOrWhiteSpace(productId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"product-{productId}");
            _logger.LogInformation("Connection {ConnectionId} joined product-{ProductId}", Context.ConnectionId, productId);
        }
    }

    /// <summary>
    /// Client leaves a product's Q&A group.
    /// </summary>
    public async Task LeaveProductGroup(string productId)
    {
        if (!string.IsNullOrWhiteSpace(productId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"product-{productId}");
            _logger.LogInformation("Connection {ConnectionId} left product-{ProductId}", Context.ConnectionId, productId);
        }
    }

    /// <summary>
    /// User joins their personal discussion notifications group.
    /// </summary>
    public async Task JoinUserGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("Connection {ConnectionId} joined user-{UserId}", Context.ConnectionId, userId);
        }
    }

    public async Task LeaveUserGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }
    }

    /// <summary>
    /// Admin joins the store-wide discussion management group.
    /// </summary>
    public async Task JoinAdminGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin-discussions");
        _logger.LogInformation("Admin Connection {ConnectionId} joined admin-discussions", Context.ConnectionId);
    }

    /// <summary>
    /// Admin leaves the store-wide discussion management group.
    /// </summary>
    public async Task LeaveAdminGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin-discussions");
        _logger.LogInformation("Admin Connection {ConnectionId} left admin-discussions", Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Discussion hub connection established: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Discussion hub connection closed: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
