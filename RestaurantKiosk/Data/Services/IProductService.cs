using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

public interface IProductService
{
    Task<List<Products>> GetAllProductsAsync();
    Task<List<Products>> GetProductsByCategoryAsync(string category);
    Task<Products?> GetProductByIdAsync(int id);
    Task<bool> AddProductAsync(Products product);
    Task<bool> UpdateProductAsync(Products product);
    Task<bool> DeleteProductAsync(int productId);
    Task<List<string>> GetUniqueCategoriesAsync();
    
    /// <summary>
    /// Decreases the quantity of products based on order items
    /// </summary>
    /// <param name="orderId">The ID of the order whose items should be processed</param>
    /// <returns>True if all product quantities were successfully decreased, false otherwise</returns>
    Task<bool> DecreaseProductQuantitiesForOrderAsync(int orderId);
}

