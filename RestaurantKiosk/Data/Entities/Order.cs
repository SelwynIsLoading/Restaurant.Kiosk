using System.ComponentModel.DataAnnotations;

namespace RestaurantKiosk.Data.Entities;

/// <summary>
/// Represents an order entity in the restaurant system
/// </summary>
public class Order
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Unique order number for customer reference
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// External payment reference ID (e.g., from Xendit)
    /// </summary>
    [MaxLength(255)]
    public string? ExternalId { get; set; }
    
    /// <summary>
    /// Customer name
    /// </summary>
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer email address
    /// </summary>
    [MaxLength(255)]
    public string? CustomerEmail { get; set; }
    
    /// <summary>
    /// Customer phone number
    /// </summary>
    [MaxLength(50)]
    public string? CustomerPhone { get; set; }
    
    /// <summary>
    /// Subtotal amount before any additional charges
    /// </summary>
    [Required]
    [Range(0, 999999.99)]
    public decimal SubTotal { get; set; }
    
    /// <summary>
    /// Tax amount
    /// </summary>
    [Range(0, 999999.99)]
    public decimal Tax { get; set; } = 0;
    
    /// <summary>
    /// Service charge amount
    /// </summary>
    [Range(0, 999999.99)]
    public decimal ServiceCharge { get; set; } = 0;
    
    /// <summary>
    /// Total amount to be paid
    /// </summary>
    [Required]
    [Range(0, 999999.99)]
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Payment method used (e.g., Invoice, GCash, Maya)
    /// </summary>
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status of the order
    /// </summary>
    [Required]
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    
    /// <summary>
    /// Optional notes or special instructions
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Timestamp when the order was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when the order was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Timestamp when the payment was completed
    /// </summary>
    public DateTime? PaidAt { get; set; }
    
    /// <summary>
    /// Timestamp when the order was completed/delivered
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Collection of items in this order
    /// </summary>
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>
/// Enum representing the various states of an order
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order created but payment not yet completed
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Payment completed successfully
    /// </summary>
    Paid = 1,
    
    /// <summary>
    /// Order is being prepared by the kitchen
    /// </summary>
    Preparing = 2,
    
    /// <summary>
    /// Order is ready for pickup/delivery
    /// </summary>
    Ready = 3,
    
    /// <summary>
    /// Order has been completed/delivered
    /// </summary>
    Completed = 4,
    
    /// <summary>
    /// Order was cancelled
    /// </summary>
    Cancelled = 5,
    
    /// <summary>
    /// Payment failed
    /// </summary>
    PaymentFailed = 6
}

