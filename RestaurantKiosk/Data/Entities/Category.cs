using System.ComponentModel.DataAnnotations;

namespace RestaurantKiosk.Data.Entities;

/// <summary>
/// Represents a category entity for organizing products
/// </summary>
public class Category
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates if the category is active and should be displayed
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Timestamp when the category was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when the category was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// Display order for sorting categories
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
}
