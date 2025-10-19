using RestaurantKiosk.Data.DTOs;

namespace RestaurantKiosk.Data.Services;

/// <summary>
/// Interface for sales tracking and analytics
/// </summary>
public interface ISalesTrackerService
{
    /// <summary>
    /// Gets comprehensive sales report for a date range
    /// </summary>
    Task<SalesReportDto> GetSalesReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
    
    /// <summary>
    /// Gets today's sales summary
    /// </summary>
    Task<SalesReportDto> GetTodaySalesAsync();
    
    /// <summary>
    /// Gets this week's sales summary
    /// </summary>
    Task<SalesReportDto> GetWeekSalesAsync();
    
    /// <summary>
    /// Gets this month's sales summary
    /// </summary>
    Task<SalesReportDto> GetMonthSalesAsync();
    
    /// <summary>
    /// Gets this year's sales summary
    /// </summary>
    Task<SalesReportDto> GetYearSalesAsync();
}

