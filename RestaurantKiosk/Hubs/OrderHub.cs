using Microsoft.AspNetCore.SignalR;

namespace RestaurantKiosk.Hubs;

/// <summary>
/// SignalR hub for real-time order notifications
/// </summary>
public class OrderHub : Hub
{
    private readonly ILogger<OrderHub> _logger;

    public OrderHub(ILogger<OrderHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Broadcasts a new order notification to all connected clients
    /// </summary>
    public async Task NotifyNewOrder(int orderId, string orderNumber)
    {
        _logger.LogInformation("Broadcasting new order: {OrderNumber} (ID: {OrderId})", orderNumber, orderId);
        await Clients.All.SendAsync("NewOrder", orderId, orderNumber);
    }

    /// <summary>
    /// Broadcasts an order completion notification to all connected clients
    /// </summary>
    public async Task NotifyOrderCompleted(int orderId, string orderNumber)
    {
        _logger.LogInformation("Broadcasting order completed: {OrderNumber} (ID: {OrderId})", orderNumber, orderId);
        await Clients.All.SendAsync("OrderCompleted", orderId, orderNumber);
    }
}

