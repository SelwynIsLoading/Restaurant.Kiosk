using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(ApplicationDbContext context, ILogger<CategoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        return await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Category>> GetAllCategoriesIncludingInactiveAsync()
    {
        return await _context.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryByIdAsync(int id)
    {
        return await _context.Categories.FindAsync(id);
    }

    public async Task<Category?> GetCategoryByNameAsync(string name)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());
    }

    public async Task<bool> AddCategoryAsync(Category category)
    {
        try
        {
            _logger.LogInformation("Adding new category: {CategoryName}", category.Name);
            _context.Categories.Add(category);
            var result = await _context.SaveChangesAsync() > 0;
            
            if (result)
            {
                _logger.LogInformation("Successfully added category: {CategoryName}", category.Name);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding category: {CategoryName}", category.Name);
            return false;
        }
    }

    public async Task<bool> UpdateCategoryAsync(Category category)
    {
        try
        {
            _logger.LogInformation("Updating category with ID: {CategoryId}", category.Id);
            var existingCategory = await _context.Categories.FindAsync(category.Id);
            
            if (existingCategory == null)
            {
                _logger.LogWarning("Category with ID: {CategoryId} not found", category.Id);
                return false;
            }

            existingCategory.Name = category.Name;
            existingCategory.Description = category.Description;
            existingCategory.IsActive = category.IsActive;
            existingCategory.DisplayOrder = category.DisplayOrder;
            existingCategory.UpdatedAt = DateTime.UtcNow;

            _context.Categories.Update(existingCategory);
            var result = await _context.SaveChangesAsync() > 0;
            
            if (result)
            {
                _logger.LogInformation("Successfully updated category: {CategoryName}", category.Name);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category with ID: {CategoryId}", category.Id);
            return false;
        }
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        try
        {
            _logger.LogInformation("Deleting category with ID: {CategoryId}", id);
            var category = await _context.Categories.FindAsync(id);
            
            if (category == null)
            {
                _logger.LogWarning("Category with ID: {CategoryId} not found", id);
                return false;
            }

            // Check if any products are using this category
            var productsUsingCategory = await _context.Products
                .AnyAsync(p => p.Category == category.Name);
            
            if (productsUsingCategory)
            {
                _logger.LogInformation("Soft deleting category: {CategoryName} (products exist)", category.Name);
                // Soft delete - just mark as inactive
                category.IsActive = false;
                category.UpdatedAt = DateTime.UtcNow;
                _context.Categories.Update(category);
            }
            else
            {
                _logger.LogInformation("Hard deleting category: {CategoryName}", category.Name);
                // Hard delete if no products are using it
                _context.Categories.Remove(category);
            }

            return await _context.SaveChangesAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category with ID: {CategoryId}", id);
            return false;
        }
    }

    public async Task<bool> CategoryExistsAsync(string name)
    {
        return await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == name.ToLower());
    }

    public async Task<List<string>> GetCategoryNamesAsync()
    {
        return await _context.Categories
            .Where(c => c.IsActive)
            .Select(c => c.Name)
            .OrderBy(name => name)
            .ToListAsync();
    }
}
