using Microsoft.EntityFrameworkCore;
using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

/// <summary>
/// Repository for order data access operations
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(ApplicationDbContext context, ILogger<OrderRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        try
        {
            _logger.LogInformation("Creating order {OrderNumber} with {ItemCount} items", 
                order.OrderNumber, order.OrderItems.Count);

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderNumber} created successfully with ID {OrderId}", 
                order.OrderNumber, order.Id);

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order {OrderNumber}", order.OrderNumber);
            throw;
        }
    }

    public async Task<Order?> GetOrderByIdAsync(int orderId)
    {
        try
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by ID {OrderId}", orderId);
            throw;
        }
    }

    public async Task<Order?> GetOrderByExternalIdAsync(string externalId)
    {
        try
        {
            _logger.LogInformation("Retrieving order by ExternalId: {ExternalId}", externalId);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.ExternalId == externalId);

            if (order != null)
            {
                _logger.LogInformation("Found order {OrderNumber} with {ItemCount} items", 
                    order.OrderNumber, order.OrderItems.Count);
            }
            else
            {
                _logger.LogWarning("No order found with ExternalId: {ExternalId}", externalId);
            }

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by ExternalId {ExternalId}", externalId);
            throw;
        }
    }

    public async Task<Order?> GetOrderByOrderNumberAsync(string orderNumber)
    {
        try
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by OrderNumber {OrderNumber}", orderNumber);
            throw;
        }
    }

    public async Task<Order> UpdateOrderAsync(Order order)
    {
        try
        {
            order.UpdatedAt = DateTime.UtcNow;
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderNumber} updated successfully", order.OrderNumber);

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderNumber}", order.OrderNumber);
            throw;
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(string externalId, OrderStatus status, DateTime? paidAt = null)
    {
        try
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.ExternalId == externalId);

            if (order == null)
            {
                _logger.LogWarning("Cannot update status: Order not found with ExternalId {ExternalId}", externalId);
                return false;
            }

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;

            if (paidAt.HasValue && status == OrderStatus.Paid)
            {
                order.PaidAt = paidAt.Value;
            }

            if (status == OrderStatus.Completed)
            {
                order.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderNumber} status updated to {Status}", 
                order.OrderNumber, status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status for ExternalId {ExternalId}", externalId);
            throw;
        }
    }

    public async Task<List<Order>> GetOrdersAsync(OrderStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= toDate.Value);
            }

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders");
            throw;
        }
    }

    public async Task<bool> DeleteOrderAsync(int orderId)
    {
        try
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Cannot delete: Order not found with ID {OrderId}", orderId);
                return false;
            }

            // Instead of hard delete, update status to Cancelled
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderNumber} marked as cancelled", order.OrderNumber);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<List<Order>> GetActiveOrdersAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving active orders (status = Paid)");

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.Status == OrderStatus.Paid)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("Found {Count} active orders", orders.Count);

            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active orders");
            throw;
        }
    }

    public async Task<bool> UpdateOrderStatusByOrderNumberAsync(string orderNumber, OrderStatus status, DateTime? paidAt = null)
    {
        try
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                _logger.LogWarning("Cannot update status: Order not found with OrderNumber {OrderNumber}", orderNumber);
                return false;
            }

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;

            if (paidAt.HasValue && status == OrderStatus.Paid)
            {
                order.PaidAt = paidAt.Value;
            }

            if (status == OrderStatus.Completed)
            {
                order.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderNumber} status updated to {Status}", 
                orderNumber, status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status for OrderNumber {OrderNumber}", orderNumber);
            throw;
        }
    }

    public async Task<bool> MarkOrderAsCompletedAsync(int orderId)
    {
        try
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Cannot mark as completed: Order not found with ID {OrderId}", orderId);
                return false;
            }

            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Order {OrderNumber} marked as completed", order.OrderNumber);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking order {OrderId} as completed", orderId);
            throw;
        }
    }
}

