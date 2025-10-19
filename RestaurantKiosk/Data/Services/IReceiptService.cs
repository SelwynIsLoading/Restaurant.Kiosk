using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

/// <summary>
/// Service interface for receipt generation and printing
/// </summary>
public interface IReceiptService
{
    /// <summary>
    /// Generate receipt data from an order
    /// </summary>
    Task<ReceiptData> GenerateReceiptDataAsync(Order order, decimal? amountPaid = null, decimal? change = null);
    
    /// <summary>
    /// Send receipt data to the Raspberry Pi printer service
    /// </summary>
    Task<bool> PrintReceiptAsync(ReceiptData receiptData);
    
    /// <summary>
    /// Generate and print receipt in one operation
    /// </summary>
    Task<bool> PrintOrderReceiptAsync(Order order, decimal? amountPaid = null, decimal? change = null);
    
    /// <summary>
    /// Test printer connection
    /// </summary>
    Task<bool> TestPrinterAsync();
}

/// <summary>
/// Data structure for receipt information
/// </summary>
public class ReceiptData
{
    public string RestaurantName { get; set; } = "Restaurant Kiosk";
    public string RestaurantAddress { get; set; } = string.Empty;
    public string RestaurantPhone { get; set; } = string.Empty;
    public string RestaurantEmail { get; set; } = string.Empty;
    
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    
    public List<ReceiptItem> Items { get; set; } = new();
    
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal TotalAmount { get; set; }
    
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal? AmountPaid { get; set; }
    public decimal? Change { get; set; }
    
    public string Status { get; set; } = "PAID";
    public string FooterMessage { get; set; } = "Thank you for your order!";
    public string? QrData { get; set; }
}

/// <summary>
/// Individual item in a receipt
/// </summary>
public class ReceiptItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
}

