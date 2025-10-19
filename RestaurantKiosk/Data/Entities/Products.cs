using System.ComponentModel.DataAnnotations;

namespace RestaurantKiosk.Data.Entities;

/// <summary>
/// Represents a product entity in the restaurant system
/// </summary>
public class Products
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string Category { get; set; } = string.Empty;
    
    [Required]
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }
    
    [Range(0, 999999)]
    public int Quantity { get; set; }
    
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Timestamp when the product was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when the product was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Indicates if the product is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}