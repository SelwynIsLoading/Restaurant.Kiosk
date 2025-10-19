using Microsoft.AspNetCore.Mvc;
using RestaurantKiosk.Data.Services;

namespace RestaurantKiosk.Controllers;

/// <summary>
/// API controller for receipt printing operations
/// </summary>
[ApiController]
[Route("api/receipt")]
public class ReceiptController : ControllerBase
{
    private readonly IReceiptService _receiptService;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<ReceiptController> _logger;

    public ReceiptController(
        IReceiptService receiptService,
        IOrderRepository orderRepository,
        ILogger<ReceiptController> logger)
    {
        _receiptService = receiptService;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    /// <summary>
    /// Print receipt for an order by order number
    /// </summary>
    [HttpPost("print/{orderNumber}")]
    public async Task<IActionResult> PrintReceiptByOrderNumber(string orderNumber, [FromBody] PrintReceiptRequest? request)
    {
        try
        {
            _logger.LogInformation("Received print receipt request for order: {OrderNumber}", orderNumber);
            
            var order = await _orderRepository.GetOrderByOrderNumberAsync(orderNumber);
            
            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderNumber}", orderNumber);
                return NotFound(new
                {
                    success = false,
                    message = $"Order {orderNumber} not found"
                });
            }
            
            var success = await _receiptService.PrintOrderReceiptAsync(
                order, 
                request?.AmountPaid, 
                request?.Change);
            
            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Receipt printed successfully for order {orderNumber}",
                    orderNumber = order.OrderNumber
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to print receipt. Check printer connection."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing receipt for order: {OrderNumber}", orderNumber);
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while printing the receipt"
            });
        }
    }

    /// <summary>
    /// Print receipt for an order by order ID
    /// </summary>
    [HttpPost("print/id/{orderId}")]
    public async Task<IActionResult> PrintReceiptByOrderId(int orderId, [FromBody] PrintReceiptRequest? request)
    {
        try
        {
            _logger.LogInformation("Received print receipt request for order ID: {OrderId}", orderId);
            
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            
            if (order == null)
            {
                _logger.LogWarning("Order not found with ID: {OrderId}", orderId);
                return NotFound(new
                {
                    success = false,
                    message = $"Order with ID {orderId} not found"
                });
            }
            
            var success = await _receiptService.PrintOrderReceiptAsync(
                order, 
                request?.AmountPaid, 
                request?.Change);
            
            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Receipt printed successfully for order {order.OrderNumber}",
                    orderNumber = order.OrderNumber
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to print receipt. Check printer connection."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing receipt for order ID: {OrderId}", orderId);
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while printing the receipt"
            });
        }
    }

    /// <summary>
    /// Reprint a receipt for an existing order
    /// </summary>
    [HttpPost("reprint/{orderNumber}")]
    public async Task<IActionResult> ReprintReceipt(string orderNumber)
    {
        try
        {
            _logger.LogInformation("Received reprint receipt request for order: {OrderNumber}", orderNumber);
            
            var order = await _orderRepository.GetOrderByOrderNumberAsync(orderNumber);
            
            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderNumber}", orderNumber);
                return NotFound(new
                {
                    success = false,
                    message = $"Order {orderNumber} not found"
                });
            }
            
            // For reprints, we don't include cash payment details
            var success = await _receiptService.PrintOrderReceiptAsync(order);
            
            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Receipt reprinted successfully for order {orderNumber}",
                    orderNumber = order.OrderNumber
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to reprint receipt. Check printer connection."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprinting receipt for order: {OrderNumber}", orderNumber);
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while reprinting the receipt"
            });
        }
    }

    /// <summary>
    /// Test printer connection and print a test receipt
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestPrinter()
    {
        try
        {
            _logger.LogInformation("Testing printer connection");
            
            var success = await _receiptService.TestPrinterAsync();
            
            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = "Printer test successful. Check printer for test receipt."
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Printer test failed. Check printer connection and service."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing printer");
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while testing the printer"
            });
        }
    }

    /// <summary>
    /// Generate receipt data without printing (for preview)
    /// </summary>
    [HttpGet("preview/{orderNumber}")]
    public async Task<IActionResult> PreviewReceipt(string orderNumber)
    {
        try
        {
            _logger.LogInformation("Generating receipt preview for order: {OrderNumber}", orderNumber);
            
            var order = await _orderRepository.GetOrderByOrderNumberAsync(orderNumber);
            
            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderNumber}", orderNumber);
                return NotFound(new
                {
                    success = false,
                    message = $"Order {orderNumber} not found"
                });
            }
            
            var receiptData = await _receiptService.GenerateReceiptDataAsync(order);
            
            return Ok(new
            {
                success = true,
                receiptData = receiptData
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating receipt preview for order: {OrderNumber}", orderNumber);
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while generating the receipt preview"
            });
        }
    }

    /// <summary>
    /// Get printer status from Raspberry Pi service
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetPrinterStatus()
    {
        try
        {
            _logger.LogInformation("Checking printer status");
            
            var isConnected = await _receiptService.TestPrinterAsync();
            
            return Ok(new
            {
                success = true,
                connected = isConnected,
                message = isConnected ? "Printer is connected and ready" : "Printer is not available"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking printer status");
            return Ok(new
            {
                success = false,
                connected = false,
                message = "Error checking printer status"
            });
        }
    }
}

#region Request Models

public class PrintReceiptRequest
{
    /// <summary>
    /// Amount paid (for cash payments)
    /// </summary>
    public decimal? AmountPaid { get; set; }
    
    /// <summary>
    /// Change given (for cash payments)
    /// </summary>
    public decimal? Change { get; set; }
}

#endregion

