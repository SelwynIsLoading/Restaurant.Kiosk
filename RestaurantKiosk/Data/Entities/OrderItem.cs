using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantKiosk.Data.Entities;

/// <summary>
/// Represents an individual item within an order
/// </summary>
public class OrderItem
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the parent Order
    /// </summary>
    [Required]
    public int OrderId { get; set; }
    
    /// <summary>
    /// Navigation property to the parent Order
    /// </summary>
    [ForeignKey(nameof(OrderId))]
    public virtual Order Order { get; set; } = null!;
    
    /// <summary>
    /// Foreign key to the Product
    /// </summary>
    [Required]
    public int ProductId { get; set; }
    
    /// <summary>
    /// Navigation property to the Product
    /// </summary>
    [ForeignKey(nameof(ProductId))]
    public virtual Products Product { get; set; } = null!;
    
    /// <summary>
    /// Product name at the time of order (snapshot)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// Unit price at the time of order (snapshot)
    /// </summary>
    [Required]
    [Range(0.01, 999999.99)]
    public decimal UnitPrice { get; set; }
    
    /// <summary>
    /// Quantity ordered
    /// </summary>
    [Required]
    [Range(1, 999)]
    public int Quantity { get; set; }
    
    /// <summary>
    /// Line total (UnitPrice * Quantity)
    /// </summary>
    [Required]
    [Range(0, 999999.99)]
    public decimal LineTotal { get; set; }
    
    /// <summary>
    /// Optional notes for this specific item
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Timestamp when the order item was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

