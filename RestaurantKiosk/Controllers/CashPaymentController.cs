using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RestaurantKiosk.Data.Entities;
using RestaurantKiosk.Data.Services;
using RestaurantKiosk.Hubs;
using System.Text.Json;

namespace RestaurantKiosk.Controllers;

/// <summary>
/// API controller for handling cash payment updates from Arduino/Python API
/// Uses polling architecture for simpler, more reliable communication
/// </summary>
[ApiController]
[Route("api/cash-payment")]
public class CashPaymentController : ControllerBase
{
    private readonly IHubContext<OrderHub> _orderHub;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductService _productService;
    private readonly IReceiptService _receiptService;
    private readonly ILogger<CashPaymentController> _logger;
    private readonly IConfiguration _configuration;

    // In-memory storage for active cash payment sessions (in production, use Redis or database)
    private static readonly Dictionary<string, CashPaymentSession> _activeSessions = new();
    private static readonly object _sessionLock = new();

    public CashPaymentController(
        IHubContext<OrderHub> orderHub,
        IOrderRepository orderRepository,
        IProductService productService,
        IReceiptService receiptService,
        ILogger<CashPaymentController> logger,
        IConfiguration configuration)
    {
        _orderHub = orderHub;
        _orderRepository = orderRepository;
        _productService = productService;
        _receiptService = receiptService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Validate API key from request header (optional security for production)
    /// </summary>
    private bool ValidateApiKey()
    {
        var configuredApiKey = _configuration["CashPayment:ApiKey"];
        
        // If no API key is configured, allow all requests (backward compatible)
        if (string.IsNullOrEmpty(configuredApiKey))
        {
            return true;
        }

        // Check if API key is provided in request header
        if (!Request.Headers.TryGetValue("X-API-Key", out var providedApiKey))
        {
            _logger.LogWarning("API key required but not provided in request");
            return false;
        }

        var isValid = configuredApiKey == providedApiKey.ToString();
        if (!isValid)
        {
            _logger.LogWarning("Invalid API key provided from IP: {IpAddress}", 
                HttpContext.Connection.RemoteIpAddress);
        }

        return isValid;
    }

    /// <summary>
    /// Initialize a cash payment session for an order
    /// </summary>
    [HttpPost("init")]
    public IActionResult InitializePaymentSession([FromBody] InitCashPaymentRequest request)
    {
        try
        {
            lock (_sessionLock)
            {
                if (_activeSessions.ContainsKey(request.OrderNumber))
                {
                    return BadRequest(new { success = false, message = "Payment session already exists for this order" });
                }

                var session = new CashPaymentSession
                {
                    OrderNumber = request.OrderNumber,
                    TotalRequired = request.TotalAmount,
                    AmountInserted = 0,
                    StartedAt = DateTime.UtcNow,
                    Status = PaymentSessionStatus.Active
                };

                _activeSessions[request.OrderNumber] = session;
                _logger.LogInformation("Cash payment session initialized for order: {OrderNumber}, Amount: {TotalAmount}", 
                    request.OrderNumber, request.TotalAmount);

                return Ok(new
                {
                    success = true,
                    orderNumber = request.OrderNumber,
                    totalRequired = request.TotalAmount,
                    sessionId = request.OrderNumber
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing cash payment session");
            return StatusCode(500, new { success = false, message = "Failed to initialize payment session" });
        }
    }

    /// <summary>
    /// Endpoint for Python API to update the cash amount inserted
    /// This will be called by the Python script reading from Arduino (running on Raspberry Pi)
    /// Note: Python script makes OUTGOING connections to VPS, so dynamic home IP is OK
    /// Frontend polls /api/cash-payment/status/{orderNumber} to get updates
    /// </summary>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateCashAmount([FromBody] CashUpdateRequest request)
    {
        // Validate API key if configured
        if (!ValidateApiKey())
        {
            return Unauthorized(new { success = false, message = "Invalid or missing API key" });
        }

        try
        {
            CashPaymentSession session;
            
            lock (_sessionLock)
            {
                if (!_activeSessions.TryGetValue(request.OrderNumber, out session!))
                {
                    return NotFound(new { success = false, message = "Payment session not found" });
                }

                if (session.Status != PaymentSessionStatus.Active)
                {
                    return BadRequest(new { success = false, message = "Payment session is not active" });
                }

                // Update the amount inserted
                session.AmountInserted += request.AmountAdded;
                session.LastUpdateAt = DateTime.UtcNow;

                _logger.LogInformation("Cash amount updated for order {OrderNumber}: Added={AmountAdded}, Total={AmountInserted}/{TotalRequired}",
                    request.OrderNumber, request.AmountAdded, session.AmountInserted, session.TotalRequired);
            }

            // Check if payment is complete
            if (session.AmountInserted >= session.TotalRequired)
            {
                await CompletePayment(request.OrderNumber);
            }

            return Ok(new
            {
                success = true,
                orderNumber = request.OrderNumber,
                amountInserted = session.AmountInserted,
                totalRequired = session.TotalRequired,
                remainingAmount = Math.Max(0, session.TotalRequired - session.AmountInserted),
                isComplete = session.AmountInserted >= session.TotalRequired
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating cash amount for order {OrderNumber}", request.OrderNumber);
            return StatusCode(500, new { success = false, message = "Failed to update cash amount" });
        }
    }

    /// <summary>
    /// Get all active cash payment sessions (for Raspberry Pi to poll)
    /// Returns list of orders waiting for cash payment
    /// </summary>
    [HttpGet("active-sessions")]
    public IActionResult GetActiveSessions()
    {
        try
        {
            lock (_sessionLock)
            {
                var activeSessions = _activeSessions.Values
                    .Where(s => s.Status == PaymentSessionStatus.Active)
                    .Select(s => new
                    {
                        orderNumber = s.OrderNumber,
                        totalRequired = s.TotalRequired,
                        amountInserted = s.AmountInserted,
                        remainingAmount = Math.Max(0, s.TotalRequired - s.AmountInserted),
                        startedAt = s.StartedAt
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    count = activeSessions.Count,
                    sessions = activeSessions
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active payment sessions");
            return StatusCode(500, new { success = false, message = "Failed to get active sessions" });
        }
    }

    /// <summary>
    /// Get the current status of a cash payment session
    /// </summary>
    [HttpGet("status/{orderNumber}")]
    public IActionResult GetPaymentStatus(string orderNumber)
    {
        try
        {
            lock (_sessionLock)
            {
                if (!_activeSessions.TryGetValue(orderNumber, out var session))
                {
                    return NotFound(new { success = false, message = "Payment session not found" });
                }

                return Ok(new
                {
                    success = true,
                    orderNumber = session.OrderNumber,
                    amountInserted = session.AmountInserted,
                    totalRequired = session.TotalRequired,
                    remainingAmount = Math.Max(0, session.TotalRequired - session.AmountInserted),
                    change = Math.Max(0, session.AmountInserted - session.TotalRequired),
                    status = session.Status.ToString(),
                    startedAt = session.StartedAt,
                    completedAt = session.CompletedAt
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status for order {OrderNumber}", orderNumber);
            return StatusCode(500, new { success = false, message = "Failed to get payment status" });
        }
    }

    /// <summary>
    /// Cancel a cash payment session and return money
    /// Frontend polls /api/cash-payment/status/{orderNumber} to detect cancellation
    /// </summary>
    [HttpPost("cancel/{orderNumber}")]
    public async Task<IActionResult> CancelPayment(string orderNumber)
    {
        try
        {
            decimal amountToReturn = 0;

            lock (_sessionLock)
            {
                if (!_activeSessions.TryGetValue(orderNumber, out var session))
                {
                    return NotFound(new { success = false, message = "Payment session not found" });
                }

                amountToReturn = session.AmountInserted;
                session.Status = PaymentSessionStatus.Cancelled;
                session.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Cash payment cancelled for order {OrderNumber}, Returning: {AmountToReturn}",
                    orderNumber, amountToReturn);
            }

            // Update order status to cancelled
            await _orderRepository.UpdateOrderStatusByOrderNumberAsync(orderNumber, OrderStatus.Cancelled, null);

            // Clean up session after a delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                lock (_sessionLock)
                {
                    _activeSessions.Remove(orderNumber);
                }
            });

            return Ok(new
            {
                success = true,
                orderNumber = orderNumber,
                amountReturned = amountToReturn,
                message = $"Payment cancelled. Returning {amountToReturn:C}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment for order {OrderNumber}", orderNumber);
            return StatusCode(500, new { success = false, message = "Failed to cancel payment" });
        }
    }

    /// <summary>
    /// Complete the payment when sufficient cash has been inserted
    /// Frontend polls /api/cash-payment/status/{orderNumber} to detect completion
    /// </summary>
    private async Task CompletePayment(string orderNumber)
    {
        try
        {
            decimal change = 0;
            decimal amountPaid = 0;

            lock (_sessionLock)
            {
                if (!_activeSessions.TryGetValue(orderNumber, out var session))
                {
                    _logger.LogWarning("Cannot complete payment: session not found for order {OrderNumber}", orderNumber);
                    return;
                }

                if (session.Status != PaymentSessionStatus.Active)
                {
                    _logger.LogWarning("Cannot complete payment: session is not active for order {OrderNumber}", orderNumber);
                    return;
                }

                amountPaid = session.AmountInserted;
                change = Math.Max(0, session.AmountInserted - session.TotalRequired);
                session.Status = PaymentSessionStatus.Completed;
                session.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation("Cash payment completed for order {OrderNumber}: Paid={AmountPaid}, Change={Change}",
                    orderNumber, amountPaid, change);
            }

            // Update order status in database
            var order = await _orderRepository.GetOrderByOrderNumberAsync(orderNumber);
            if (order != null)
            {
                await _orderRepository.UpdateOrderStatusByOrderNumberAsync(orderNumber, OrderStatus.Paid, DateTime.UtcNow);

                // Decrease product quantities
                var quantityDecreased = await _productService.DecreaseProductQuantitiesForOrderAsync(order.Id);
                if (quantityDecreased)
                {
                    _logger.LogInformation("Product quantities decreased for order ID: {OrderId}", order.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to decrease product quantities for order ID: {OrderId}", order.Id);
                }

                // Notify kitchen staff via SignalR (OrderHub is still used for kitchen orders)
                await _orderHub.Clients.All.SendAsync("NewOrder", order.Id, order.OrderNumber);
                
                // Print receipt
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _receiptService.PrintOrderReceiptAsync(order, amountPaid, change);
                        _logger.LogInformation("Receipt printed for cash payment order: {OrderNumber}", orderNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to print receipt for order: {OrderNumber}", orderNumber);
                    }
                });
            }

            // Clean up session after a delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                lock (_sessionLock)
                {
                    _activeSessions.Remove(orderNumber);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing payment for order {OrderNumber}", orderNumber);
        }
    }

    /// <summary>
    /// Test endpoint to simulate cash insertion (for development/testing)
    /// </summary>
    [HttpPost("test/simulate")]
    public async Task<IActionResult> SimulateCashInsertion([FromBody] SimulateCashRequest request)
    {
        try
        {
            // This simulates the Python API sending updates
            var updateRequest = new CashUpdateRequest
            {
                OrderNumber = request.OrderNumber,
                AmountAdded = request.Amount
            };

            return await UpdateCashAmount(updateRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating cash insertion");
            return StatusCode(500, new { success = false, message = "Failed to simulate cash insertion" });
        }
    }
}

#region Request/Response Models

public class InitCashPaymentRequest
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

public class CashUpdateRequest
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal AmountAdded { get; set; }
}

public class SimulateCashRequest
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CashPaymentSession
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalRequired { get; set; }
    public decimal AmountInserted { get; set; }
    public PaymentSessionStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? LastUpdateAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum PaymentSessionStatus
{
    Active,
    Completed,
    Cancelled
}

#endregion

