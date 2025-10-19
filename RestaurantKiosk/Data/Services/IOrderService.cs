using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

public interface IOrderService
{
    List<OrderService.CartItem> GetCartItems();
    void AddToCart(Products product, int quantity = 1);
    void UpdateQuantity(int productId, int quantity);
    void RemoveFromCart(int productId);
    void ClearCart();
    decimal GetCartTotal();
    int GetCartItemCount();
    bool HasItems();
}

