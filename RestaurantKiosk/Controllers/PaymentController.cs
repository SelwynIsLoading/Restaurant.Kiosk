using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RestaurantKiosk.Data.Services;
using RestaurantKiosk.Hubs;
using System.Text.Json;

namespace RestaurantKiosk.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IOrderService _orderService;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductService _productService;
    private readonly IReceiptService _receiptService;
    private readonly IHubContext<OrderHub> _orderHubContext;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPaymentService paymentService, 
        IOrderService orderService,
        IOrderRepository orderRepository,
        IProductService productService,
        IReceiptService receiptService,
        IHubContext<OrderHub> orderHubContext,
        ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _orderService = orderService;
        _orderRepository = orderRepository;
        _productService = productService;
        _receiptService = receiptService;
        _orderHubContext = orderHubContext;
        _logger = logger;
    }

    [HttpPost("create-invoice")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        try
        {
            var externalId = $"order_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            
            var invoice = await _paymentService.CreateInvoiceAsync(
                externalId,
                request.Amount,
                request.CustomerName,
                request.CustomerEmail,
                request.Description,
                $"{baseUrl}/payment/success?external_id={externalId}",
                $"{baseUrl}/payment/failure?external_id={externalId}"
            );

            return Ok(new
            {
                success = true,
                invoiceId = invoice.Id,
                invoiceUrl = invoice.InvoiceUrl,
                externalId = externalId,
                status = invoice.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invoice");
            return BadRequest(new { success = false, message = "Failed to create invoice" });
        }
    }

    [HttpPost("create-gcash")]
    public async Task<IActionResult> CreateGCashPayment([FromBody] CreateEWalletRequest request)
    {
        try
        {
            var externalId = $"gcash_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            
            var payment = await _paymentService.CreateGCashPaymentAsync(
                externalId,
                request.Amount,
                request.CustomerPhone,
                $"{baseUrl}/payment/callback?external_id={externalId}"
            );

            return Ok(new
            {
                success = true,
                chargeId = payment.Id,
                checkoutUrl = payment.Actions?.MobileWebCheckoutUrl ?? payment.Actions?.DesktopWebCheckoutUrl,
                externalId = externalId,
                status = payment.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GCash payment");
            return BadRequest(new { success = false, message = "Failed to create GCash payment" });
        }
    }

    [HttpPost("create-maya")]
    public async Task<IActionResult> CreateMayaPayment([FromBody] CreateEWalletRequest request)
    {
        try
        {
            var externalId = $"maya_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            
            var payment = await _paymentService.CreateMayaPaymentAsync(
                externalId,
                request.Amount,
                request.CustomerPhone,
                $"{baseUrl}/payment/callback?external_id={externalId}"
            );

            return Ok(new
            {
                success = true,
                chargeId = payment.Id,
                checkoutUrl = payment.Actions?.MobileWebCheckoutUrl ?? payment.Actions?.DesktopWebCheckoutUrl,
                externalId = externalId,
                status = payment.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Maya payment");
            return BadRequest(new { success = false, message = "Failed to create Maya payment" });
        }
    }

    [HttpGet("status/{paymentId}")]
    public async Task<IActionResult> GetPaymentStatus(string paymentId, [FromQuery] string type = "invoice")
    {
        try
        {
            if (type == "invoice")
            {
                var invoice = await _paymentService.GetInvoiceAsync(paymentId);
                return Ok(new
                {
                    success = true,
                    status = invoice.Status,
                    amount = invoice.Amount,
                    externalId = invoice.ExternalId
                });
            }
            else if (type == "ewallet")
            {
                var charge = await _paymentService.GetEWalletChargeAsync(paymentId);
                return Ok(new
                {
                    success = true,
                    status = charge.Status,
                    amount = charge.Amount,
                    externalId = charge.ReferenceId
                });
            }

            return BadRequest(new { success = false, message = "Invalid payment type" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status for {PaymentId}", paymentId);
            return BadRequest(new { success = false, message = "Failed to get payment status" });
        }
    }

    /// <summary>
    /// Test endpoint to manually trigger SignalR notification for an order
    /// </summary>
    [HttpPost("test-notification/{orderId}")]
    public async Task<IActionResult> TestNotification(int orderId)
    {
        try
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                return NotFound(new { success = false, message = "Order not found" });
            }

            _logger.LogInformation("Testing SignalR notification for order: {OrderNumber} (ID: {OrderId})", order.OrderNumber, order.Id);
            await _orderHubContext.Clients.All.SendAsync("NewOrder", order.Id, order.OrderNumber);
            _logger.LogInformation("Test notification sent successfully");

            return Ok(new
            {
                success = true,
                message = "Notification sent successfully",
                orderId = order.Id,
                orderNumber = order.OrderNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test notification for order {OrderId}", orderId);
            return StatusCode(500, new { success = false, message = "Failed to send notification" });
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            
            var webhookToken = Request.Headers["x-callback-token"].FirstOrDefault();
            
            if (!_paymentService.VerifyWebhook(webhookToken ?? "", requestBody, ""))
            {
                _logger.LogWarning("Invalid webhook token");
                return Unauthorized();
            }

            var webhookData = JsonSerializer.Deserialize<JsonElement>(requestBody);
            
            // Handle different webhook events
            var eventType = webhookData.GetProperty("event").GetString();
            
            switch (eventType)
            {
                case "invoice.paid":
                    await HandleInvoicePaid(webhookData);
                    break;
                case "ewallet.charge.succeeded":
                    await HandleEWalletChargeSucceeded(webhookData);
                    break;
                case "ewallet.charge.failed":
                    await HandleEWalletChargeFailed(webhookData);
                    break;
                default:
                    _logger.LogInformation("Unhandled webhook event: {EventType}", eventType);
                    break;
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return StatusCode(500);
        }
    }

    private async Task HandleInvoicePaid(JsonElement webhookData)
    {
        var externalId = webhookData.GetProperty("data").GetProperty("external_id").GetString();
        _logger.LogInformation("Invoice paid for external ID: {ExternalId}", externalId);
        
        if (!string.IsNullOrEmpty(externalId))
        {
            // Update order status in database
            var updated = await _orderRepository.UpdateOrderStatusAsync(externalId, Data.Entities.OrderStatus.Paid, DateTime.UtcNow);
            
            if (updated)
            {
                _logger.LogInformation("Order status updated to Paid for ExternalId: {ExternalId}", externalId);
                
                // Decrease product quantities
                var order = await _orderRepository.GetOrderByExternalIdAsync(externalId);
                if (order != null)
                {
                    var quantityDecreased = await _productService.DecreaseProductQuantitiesForOrderAsync(order.Id);
                    if (quantityDecreased)
                    {
                        _logger.LogInformation("Product quantities decreased for order ID: {OrderId}", order.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to decrease product quantities for order ID: {OrderId}", order.Id);
                    }
                    
                    // Ensure all database changes are committed before notifying
                    await Task.Delay(100);
                    
                    // Notify kitchen staff via SignalR
                    try
                    {
                        _logger.LogInformation("Sending SignalR notification for order: {OrderNumber} (ID: {OrderId})", order.OrderNumber, order.Id);
                        await _orderHubContext.Clients.All.SendAsync("NewOrder", order.Id, order.OrderNumber);
                        _logger.LogInformation("Successfully sent SignalR notification for order: {OrderNumber}", order.OrderNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send SignalR notification for order: {OrderNumber}", order.OrderNumber);
                    }
                    
                    // Print receipt
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _receiptService.PrintOrderReceiptAsync(order);
                            _logger.LogInformation("Receipt printed for invoice payment order: {OrderNumber}", order.OrderNumber);
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogError(ex2, "Failed to print receipt for order: {OrderNumber}", order.OrderNumber);
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Order not found after status update for ExternalId: {ExternalId}", externalId);
                }
                
                // TODO: Send confirmation email
            }
            else
            {
                _logger.LogWarning("Failed to update order status for ExternalId: {ExternalId}", externalId);
            }
        }
    }

    private async Task HandleEWalletChargeSucceeded(JsonElement webhookData)
    {
        var externalId = webhookData.GetProperty("data").GetProperty("reference_id").GetString();
        _logger.LogInformation("E-wallet charge succeeded for external ID: {ExternalId}", externalId);
        
        if (!string.IsNullOrEmpty(externalId))
        {
            // Update order status in database
            var updated = await _orderRepository.UpdateOrderStatusAsync(externalId, Data.Entities.OrderStatus.Paid, DateTime.UtcNow);
            
            if (updated)
            {
                _logger.LogInformation("Order status updated to Paid for ExternalId: {ExternalId}", externalId);
                
                // Decrease product quantities
                var order = await _orderRepository.GetOrderByExternalIdAsync(externalId);
                if (order != null)
                {
                    var quantityDecreased = await _productService.DecreaseProductQuantitiesForOrderAsync(order.Id);
                    if (quantityDecreased)
                    {
                        _logger.LogInformation("Product quantities decreased for order ID: {OrderId}", order.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to decrease product quantities for order ID: {OrderId}", order.Id);
                    }
                    
                    // Ensure all database changes are committed before notifying
                    await Task.Delay(100);
                    
                    // Notify kitchen staff via SignalR
                    try
                    {
                        _logger.LogInformation("Sending SignalR notification for order: {OrderNumber} (ID: {OrderId})", order.OrderNumber, order.Id);
                        await _orderHubContext.Clients.All.SendAsync("NewOrder", order.Id, order.OrderNumber);
                        _logger.LogInformation("Successfully sent SignalR notification for order: {OrderNumber}", order.OrderNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send SignalR notification for order: {OrderNumber}", order.OrderNumber);
                    }
                    
                    // Print receipt
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _receiptService.PrintOrderReceiptAsync(order);
                            _logger.LogInformation("Receipt printed for e-wallet payment order: {OrderNumber}", order.OrderNumber);
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogError(ex2, "Failed to print receipt for order: {OrderNumber}", order.OrderNumber);
                        }
                    });
                }
                else
                {
                    _logger.LogWarning("Order not found after status update for ExternalId: {ExternalId}", externalId);
                }
                
                // TODO: Send confirmation email
            }
            else
            {
                _logger.LogWarning("Failed to update order status for ExternalId: {ExternalId}", externalId);
            }
        }
    }

    private async Task HandleEWalletChargeFailed(JsonElement webhookData)
    {
        var externalId = webhookData.GetProperty("data").GetProperty("reference_id").GetString();
        _logger.LogInformation("E-wallet charge failed for external ID: {ExternalId}", externalId);
        
        if (!string.IsNullOrEmpty(externalId))
        {
            // Update order status in database
            var updated = await _orderRepository.UpdateOrderStatusAsync(externalId, Data.Entities.OrderStatus.PaymentFailed, null);
            
            if (updated)
            {
                _logger.LogInformation("Order status updated to PaymentFailed for ExternalId: {ExternalId}", externalId);
                
                // TODO: Send failure notification
            }
            else
            {
                _logger.LogWarning("Failed to update order status for ExternalId: {ExternalId}", externalId);
            }
        }
    }
}

public class CreateInvoiceRequest
{
    public decimal Amount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CreateEWalletRequest
{
    public decimal Amount { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
}
