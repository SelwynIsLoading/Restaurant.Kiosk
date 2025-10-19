namespace RestaurantKiosk.Data.DTOs;

/// <summary>
/// Data transfer object for sales report summary
/// </summary>
public class SalesReportDto
{
    public decimal TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int PendingOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<DailySalesDto> DailySales { get; set; } = new();
    public List<ProductSalesDto> TopProducts { get; set; } = new();
    public List<PaymentMethodSalesDto> SalesByPaymentMethod { get; set; } = new();
}

/// <summary>
/// Daily sales breakdown
/// </summary>
public class DailySalesDto
{
    public DateTime Date { get; set; }
    public decimal TotalSales { get; set; }
    public int OrderCount { get; set; }
}

/// <summary>
/// Product sales statistics
/// </summary>
public class ProductSalesDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

/// <summary>
/// Sales breakdown by payment method
/// </summary>
public class PaymentMethodSalesDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public int OrderCount { get; set; }
    public decimal Percentage { get; set; }
}

