using Microsoft.AspNetCore.Identity;

namespace RestaurantKiosk.Data;

/// <summary>
/// Represents the application user with extended properties
/// Add profile data for application users by adding properties to the ApplicationUser class
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// User's full name
    /// </summary>
    public string? FullName { get; set; }
    
    /// <summary>
    /// Timestamp when the user was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when the user was last active
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>
    /// Indicates if the user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}