using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantKiosk.Data.DTOs;
using RestaurantKiosk.Data.Services;

namespace RestaurantKiosk.Controllers;

/// <summary>
/// API controller for sales tracking and analytics
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesTrackerController : ControllerBase
{
    private readonly ISalesTrackerService _salesTrackerService;
    private readonly ILogger<SalesTrackerController> _logger;

    public SalesTrackerController(ISalesTrackerService salesTrackerService, ILogger<SalesTrackerController> logger)
    {
        _salesTrackerService = salesTrackerService;
        _logger = logger;
    }

    /// <summary>
    /// Gets sales report for a custom date range
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SalesReportDto>> GetSalesReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        try
        {
            _logger.LogInformation("Getting sales report from {FromDate} to {ToDate}", fromDate, toDate);
            var report = await _salesTrackerService.GetSalesReportAsync(fromDate, toDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sales report");
            return StatusCode(500, new { error = "An error occurred while generating the sales report" });
        }
    }

    /// <summary>
    /// Gets today's sales report
    /// </summary>
    [HttpGet("today")]
    public async Task<ActionResult<SalesReportDto>> GetTodaySales()
    {
        try
        {
            _logger.LogInformation("Getting today's sales");
            var report = await _salesTrackerService.GetTodaySalesAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's sales");
            return StatusCode(500, new { error = "An error occurred while getting today's sales" });
        }
    }

    /// <summary>
    /// Gets this week's sales report
    /// </summary>
    [HttpGet("week")]
    public async Task<ActionResult<SalesReportDto>> GetWeekSales()
    {
        try
        {
            _logger.LogInformation("Getting week's sales");
            var report = await _salesTrackerService.GetWeekSalesAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting week's sales");
            return StatusCode(500, new { error = "An error occurred while getting this week's sales" });
        }
    }

    /// <summary>
    /// Gets this month's sales report
    /// </summary>
    [HttpGet("month")]
    public async Task<ActionResult<SalesReportDto>> GetMonthSales()
    {
        try
        {
            _logger.LogInformation("Getting month's sales");
            var report = await _salesTrackerService.GetMonthSalesAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting month's sales");
            return StatusCode(500, new { error = "An error occurred while getting this month's sales" });
        }
    }

    /// <summary>
    /// Gets this year's sales report
    /// </summary>
    [HttpGet("year")]
    public async Task<ActionResult<SalesReportDto>> GetYearSales()
    {
        try
        {
            _logger.LogInformation("Getting year's sales");
            var report = await _salesTrackerService.GetYearSalesAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting year's sales");
            return StatusCode(500, new { error = "An error occurred while getting this year's sales" });
        }
    }
}

