using Microsoft.EntityFrameworkCore;
using RestaurantKiosk.Data.DTOs;
using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

/// <summary>
/// Service for sales tracking and analytics
/// </summary>
public class SalesTrackerService : ISalesTrackerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SalesTrackerService> _logger;

    public SalesTrackerService(ApplicationDbContext context, ILogger<SalesTrackerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SalesReportDto> GetSalesReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            _logger.LogInformation("Generating sales report from {FromDate} to {ToDate}", fromDate, toDate);

            // Set default date range if not provided and ensure UTC
            var startDate = fromDate.HasValue 
                ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc) 
                : DateTime.UtcNow.AddMonths(-1);
            var endDate = toDate.HasValue 
                ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc) 
                : DateTime.UtcNow;

            // Get orders in date range
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .ToListAsync();

            // Calculate totals
            var totalSales = orders
                .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                .Sum(o => o.TotalAmount);

            var totalRevenue = orders
                .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                .Sum(o => o.SubTotal);

            var totalOrders = orders.Count;
            var completedOrders = orders.Count(o => o.Status == OrderStatus.Completed);
            var pendingOrders = orders.Count(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.Paid);
            var cancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled);

            var averageOrderValue = totalOrders > 0 ? totalSales / totalOrders : 0;

            // Daily sales breakdown
            var dailySales = orders
                .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new DailySalesDto
                {
                    Date = g.Key,
                    TotalSales = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            // Top products
            var topProducts = orders
                .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => new { oi.ProductId, oi.ProductName })
                .Select(g => new ProductSalesDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.LineTotal)
                })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(10)
                .ToList();

            // Sales by payment method
            var paymentMethodSales = orders
                .Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed)
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new PaymentMethodSalesDto
                {
                    PaymentMethod = g.Key,
                    TotalSales = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count(),
                    Percentage = totalSales > 0 ? (g.Sum(o => o.TotalAmount) / totalSales * 100) : 0
                })
                .OrderByDescending(p => p.TotalSales)
                .ToList();

            var report = new SalesReportDto
            {
                TotalSales = totalSales,
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                PendingOrders = pendingOrders,
                CancelledOrders = cancelledOrders,
                AverageOrderValue = averageOrderValue,
                FromDate = startDate,
                ToDate = endDate,
                DailySales = dailySales,
                TopProducts = topProducts,
                SalesByPaymentMethod = paymentMethodSales
            };

            _logger.LogInformation("Sales report generated: Total Sales = {TotalSales}, Total Orders = {TotalOrders}", 
                totalSales, totalOrders);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sales report");
            throw;
        }
    }

    public async Task<SalesReportDto> GetTodaySalesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        return await GetSalesReportAsync(
            DateTime.SpecifyKind(today, DateTimeKind.Utc), 
            DateTime.SpecifyKind(tomorrow, DateTimeKind.Utc));
    }

    public async Task<SalesReportDto> GetWeekSalesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
        return await GetSalesReportAsync(
            DateTime.SpecifyKind(startOfWeek, DateTimeKind.Utc), 
            DateTime.UtcNow);
    }

    public async Task<SalesReportDto> GetMonthSalesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await GetSalesReportAsync(startOfMonth, DateTime.UtcNow);
    }

    public async Task<SalesReportDto> GetYearSalesAsync()
    {
        var today = DateTime.UtcNow.Date;
        var startOfYear = new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return await GetSalesReportAsync(startOfYear, DateTime.UtcNow);
    }
}

