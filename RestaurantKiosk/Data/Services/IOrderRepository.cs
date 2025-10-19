using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

/// <summary>
/// Interface for order data access operations
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Creates a new order in the database
    /// </summary>
    Task<Order> CreateOrderAsync(Order order);
    
    /// <summary>
    /// Gets an order by its ID
    /// </summary>
    Task<Order?> GetOrderByIdAsync(int orderId);
    
    /// <summary>
    /// Gets an order by its external payment reference ID
    /// </summary>
    Task<Order?> GetOrderByExternalIdAsync(string externalId);
    
    /// <summary>
    /// Gets an order by its order number
    /// </summary>
    Task<Order?> GetOrderByOrderNumberAsync(string orderNumber);
    
    /// <summary>
    /// Updates an existing order
    /// </summary>
    Task<Order> UpdateOrderAsync(Order order);
    
    /// <summary>
    /// Updates the status of an order by external ID
    /// </summary>
    Task<bool> UpdateOrderStatusAsync(string externalId, OrderStatus status, DateTime? paidAt = null);
    
    /// <summary>
    /// Updates the status of an order by order number
    /// </summary>
    Task<bool> UpdateOrderStatusByOrderNumberAsync(string orderNumber, OrderStatus status, DateTime? paidAt = null);
    
    /// <summary>
    /// Gets all orders with optional filtering
    /// </summary>
    Task<List<Order>> GetOrdersAsync(OrderStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null);
    
    /// <summary>
    /// Deletes an order (soft delete recommended)
    /// </summary>
    Task<bool> DeleteOrderAsync(int orderId);
    
    /// <summary>
    /// Gets all active orders that need to be prepared (status = Paid)
    /// </summary>
    Task<List<Order>> GetActiveOrdersAsync();
    
    /// <summary>
    /// Marks an order as completed by ID
    /// </summary>
    Task<bool> MarkOrderAsCompletedAsync(int orderId);
}

