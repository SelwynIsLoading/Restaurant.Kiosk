using Microsoft.Extensions.Logging;
using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

public class OrderService : IOrderService
{
    private readonly ILogger<OrderService> _logger;

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }
    public class CartItem
    {
        public Products Product { get; set; } = new();
        public int Quantity { get; set; }
    }

    private List<CartItem> _cartItems = new();

    public List<CartItem> GetCartItems()
    {
        return _cartItems.ToList();
    }

    public void AddToCart(Products product, int quantity = 1)
    {
        _logger.LogInformation("Adding product {ProductName} (ID: {ProductId}) to cart with quantity: {Quantity}", 
            product.Name, product.Id, quantity);
            
        var existingItem = _cartItems.FirstOrDefault(item => item.Product.Id == product.Id);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
            _logger.LogInformation("Updated quantity for {ProductName} to {TotalQuantity}", 
                product.Name, existingItem.Quantity);
        }
        else
        {
            _cartItems.Add(new CartItem { Product = product, Quantity = quantity });
            _logger.LogInformation("Added new item {ProductName} to cart", product.Name);
        }
    }

    public void UpdateQuantity(int productId, int quantity)
    {
        var item = _cartItems.FirstOrDefault(item => item.Product.Id == productId);
        if (item != null)
        {
            if (quantity <= 0)
            {
                _cartItems.Remove(item);
            }
            else
            {
                item.Quantity = quantity;
            }
        }
    }

    public void RemoveFromCart(int productId)
    {
        _cartItems.RemoveAll(item => item.Product.Id == productId);
    }

    public void ClearCart()
    {
        _logger.LogInformation("Clearing cart with {ItemCount} items", _cartItems.Count);
        _cartItems.Clear();
    }

    public decimal GetCartTotal()
    {
        return _cartItems.Sum(item => item.Product.Price * item.Quantity);
    }

    public int GetCartItemCount()
    {
        return _cartItems.Sum(item => item.Quantity);
    }

    public bool HasItems()
    {
        return _cartItems.Any();
    }
}
