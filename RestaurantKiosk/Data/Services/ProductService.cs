using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(ApplicationDbContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Products>> GetAllProductsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all products");
            return await _context.Products.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all products");
            throw;
        }
    }

    public async Task<List<Products>> GetProductsByCategoryAsync(string category)
    {
        try
        {
            _logger.LogInformation("Retrieving products for category: {Category}", category);
            return await _context.Products
                .Where(p => p.Category == category)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products for category: {Category}", category);
            throw;
        }
    }

    public async Task<Products?> GetProductByIdAsync(int id)
    {
        try
        {
            _logger.LogInformation("Retrieving product with ID: {ProductId}", id);
            return await _context.Products.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product with ID: {ProductId}", id);
            throw;
        }
    }

    public async Task<bool> AddProductAsync(Products product)
    {
        try
        {
            _logger.LogInformation("Adding new product: {ProductName}", product.Name);
            _context.Products.Add(product);
            var result = await _context.SaveChangesAsync() > 0;
            
            if (result)
            {
                _logger.LogInformation("Successfully added product: {ProductName} with ID: {ProductId}", 
                    product.Name, product.Id);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding product: {ProductName}", product.Name);
            throw;
        }
    }

    public async Task<bool> UpdateProductAsync(Products product)
    {
        try
        {
            _logger.LogInformation("Updating product with ID: {ProductId}", product.Id);
            var existingProduct = await _context.Products.FindAsync(product.Id);
            
            if (existingProduct == null)
            {
                _logger.LogWarning("Product with ID: {ProductId} not found", product.Id);
                return false;
            }

            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.Category = product.Category;
            existingProduct.Price = product.Price;
            existingProduct.Quantity = product.Quantity;
            existingProduct.ImageUrl = product.ImageUrl;
            existingProduct.IsActive = product.IsActive;
            
            _context.Products.Update(existingProduct);
            var result = await _context.SaveChangesAsync() > 0;
            
            if (result)
            {
                _logger.LogInformation("Successfully updated product: {ProductName}", product.Name);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product with ID: {ProductId}", product.Id);
            throw;
        }
    }

    public async Task<bool> DeleteProductAsync(int productId)
    {
        try
        {
            _logger.LogInformation("Deleting product with ID: {ProductId}", productId);
            var product = await _context.Products.FindAsync(productId);
            
            if (product == null)
            {
                _logger.LogWarning("Product with ID: {ProductId} not found", productId);
                return false;
            }
            
            // delete the related order items first
            var relatedOrderItems = _context.OrderItems.Where(oi => oi.ProductId == productId);
            _context.OrderItems.RemoveRange(relatedOrderItems);
            
            // then delete the product
            _context.Products.Remove(product);
            var result = await _context.SaveChangesAsync() > 0;
            
            if (result)
            {
                _logger.LogInformation("Successfully deleted product with ID: {ProductId}", productId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product with ID: {ProductId}", productId);
            throw;
        }
    }

    public async Task<List<string>> GetUniqueCategoriesAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving unique categories");
            return await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unique categories");
            throw;
        }
    }

    public async Task<bool> DecreaseProductQuantitiesForOrderAsync(int orderId)
    {
        try
        {
            _logger.LogInformation("Decreasing product quantities for order ID: {OrderId}", orderId);
            
            // Get the order with its items
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                _logger.LogWarning("Order with ID: {OrderId} not found", orderId);
                return false;
            }

            // Decrease quantity for each product in the order
            foreach (var orderItem in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(orderItem.ProductId);
                
                if (product == null)
                {
                    _logger.LogWarning("Product with ID: {ProductId} not found for order item in order {OrderId}", 
                        orderItem.ProductId, orderId);
                    continue;
                }

                // Check if there's enough quantity
                if (product.Quantity < orderItem.Quantity)
                {
                    _logger.LogWarning("Insufficient quantity for product {ProductName} (ID: {ProductId}). Available: {Available}, Required: {Required}", 
                        product.Name, product.Id, product.Quantity, orderItem.Quantity);
                    
                    // Decrease by available quantity (prevent negative values)
                    product.Quantity = 0;
                }
                else
                {
                    product.Quantity -= orderItem.Quantity;
                }

                product.UpdatedAt = DateTime.UtcNow;
                _context.Products.Update(product);
                
                _logger.LogInformation("Decreased quantity for product {ProductName} (ID: {ProductId}) by {Quantity}. New quantity: {NewQuantity}", 
                    product.Name, product.Id, orderItem.Quantity, product.Quantity);
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully decreased product quantities for order ID: {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decreasing product quantities for order ID: {OrderId}", orderId);
            throw;
        }
    }
}