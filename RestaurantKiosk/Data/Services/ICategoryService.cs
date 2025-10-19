using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

public interface ICategoryService
{
    Task<List<Category>> GetAllCategoriesAsync();
    Task<List<Category>> GetAllCategoriesIncludingInactiveAsync();
    Task<Category?> GetCategoryByIdAsync(int id);
    Task<Category?> GetCategoryByNameAsync(string name);
    Task<bool> AddCategoryAsync(Category category);
    Task<bool> UpdateCategoryAsync(Category category);
    Task<bool> DeleteCategoryAsync(int id);
    Task<bool> CategoryExistsAsync(string name);
    Task<List<string>> GetCategoryNamesAsync();
}

