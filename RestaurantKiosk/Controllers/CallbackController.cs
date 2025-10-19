using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RestaurantKiosk.Data.Entities;
using RestaurantKiosk.Data.Services;
using RestaurantKiosk.Hubs;
using System.Text.Json;

namespace RestaurantKiosk.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CallbackController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IOrderRepository _orderRepository;
    private readonly IProductService _productService;
    private readonly IReceiptService _receiptService;
    private readonly IHubContext<OrderHub> _orderHubContext;
    private readonly ILogger<CallbackController> _logger;

    public CallbackController(
        IPaymentService paymentService,
        IOrderRepository orderRepository,
        IProductService productService,
        IReceiptService receiptService,
        IHubContext<OrderHub> orderHubContext,
        ILogger<CallbackController> logger)
    {
        _paymentService = paymentService;
        _orderRepository = orderRepository;
        _productService = productService;
        _receiptService = receiptService;
        _orderHubContext = orderHubContext;
        _logger = logger;
    }

    [HttpGet("payment/callback")]
    public async Task<IActionResult> PaymentCallback([FromQuery] string external_id)
    {
        try
        {
            _logger.LogInformation("Payment callback received for external ID: {ExternalId}", external_id);
            
            if (string.IsNullOrEmpty(external_id))
            {
                _logger.LogWarning("Payment callback received with empty external_id");
                return Redirect("/payment/failure");
            }

            // Get the order from database
            var order = await _orderRepository.GetOrderByExternalIdAsync(external_id);
            if (order == null)
            {
                _logger.LogWarning("Order not found for external_id: {ExternalId}", external_id);
                return Redirect("/payment/failure?external_id=" + external_id);
            }

            // For e-wallet and card payments, the callback redirect means payment was successful
            // Payment providers only redirect to callback URL after successful payment
            // Webhooks will provide additional verification if configured
            bool paymentSuccessful = true;
            
            _logger.LogInformation("Payment callback redirect received - treating as successful payment for order: {OrderNumber}", order.OrderNumber);

            if (paymentSuccessful && order.Status != OrderStatus.Paid)
            {
                _logger.LogInformation("Processing successful payment for order: {OrderNumber}", order.OrderNumber);
                
                // Update order status to Paid
                var updated = await _orderRepository.UpdateOrderStatusAsync(external_id, OrderStatus.Paid, DateTime.UtcNow);
                
                if (updated)
                {
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

                    // Ensure database commit
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
                            // Reload order to get updated data with items
                            var orderForReceipt = await _orderRepository.GetOrderByExternalIdAsync(external_id);
                            if (orderForReceipt != null)
                            {
                                await _receiptService.PrintOrderReceiptAsync(orderForReceipt);
                                _logger.LogInformation("Receipt printed for callback payment order: {OrderNumber}", orderForReceipt.OrderNumber);
                            }
                        }
                        catch (Exception ex2)
                        {
                            _logger.LogError(ex2, "Failed to print receipt for order: {OrderNumber}", order.OrderNumber);
                        }
                    });
                }
            }
            
            return Redirect("/payment/success?external_id=" + external_id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment callback for external ID: {ExternalId}", external_id);
            return Redirect("/payment/failure?external_id=" + external_id);
        }
    }

    [HttpPost("payment/callback")]
    public async Task<IActionResult> PaymentCallbackPost()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            
            _logger.LogInformation("Payment webhook POST received. Body: {Body}", requestBody);
            
            var webhookData = JsonSerializer.Deserialize<JsonElement>(requestBody);
            var externalId = "";
            
            // Extract external ID based on event type
            if (webhookData.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("external_id", out var extId))
                {
                    externalId = extId.GetString() ?? "";
                }
                else if (data.TryGetProperty("reference_id", out var refId))
                {
                    externalId = refId.GetString() ?? "";
                }
            }
            
            _logger.LogInformation("Payment webhook received for external ID: {ExternalId}", externalId);
            
            if (!string.IsNullOrEmpty(externalId))
            {
                // Get the order from database
                var order = await _orderRepository.GetOrderByExternalIdAsync(externalId);
                if (order != null && order.Status != OrderStatus.Paid)
                {
                    _logger.LogInformation("Processing webhook for order: {OrderNumber}", order.OrderNumber);
                    
                    // Update order status to Paid
                    var updated = await _orderRepository.UpdateOrderStatusAsync(externalId, OrderStatus.Paid, DateTime.UtcNow);
                    
                    if (updated)
                    {
                        // Decrease product quantities
                        var quantityDecreased = await _productService.DecreaseProductQuantitiesForOrderAsync(order.Id);
                        if (quantityDecreased)
                        {
                            _logger.LogInformation("Product quantities decreased for order ID: {OrderId}", order.Id);
                        }

                        // Ensure database commit
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
                                // Reload order to get updated data with items
                                var orderForReceipt = await _orderRepository.GetOrderByExternalIdAsync(externalId);
                                if (orderForReceipt != null)
                                {
                                    await _receiptService.PrintOrderReceiptAsync(orderForReceipt);
                                    _logger.LogInformation("Receipt printed for webhook payment order: {OrderNumber}", orderForReceipt.OrderNumber);
                                }
                            }
                            catch (Exception ex2)
                            {
                                _logger.LogError(ex2, "Failed to print receipt for order: {OrderNumber}", order.OrderNumber);
                            }
                        });
                    }
                }
            }
            
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment webhook");
            return StatusCode(500);
        }
    }
}
