using System.Text.Json;
using RestaurantKiosk.Data.Entities;

namespace RestaurantKiosk.Data.Services;

/// <summary>
/// Service for generating and printing receipts via Raspberry Pi printer
/// </summary>
public class ReceiptService : IReceiptService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPrintQueueService? _printQueueService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReceiptService> _logger;
    
    private readonly string _printerApiUrl;
    private readonly string _restaurantName;
    private readonly string _restaurantAddress;
    private readonly string _restaurantPhone;
    private readonly string _restaurantEmail;
    private readonly bool _usePollingMode;

    public ReceiptService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ReceiptService> logger,
        IPrintQueueService? printQueueService = null)
    {
        _httpClientFactory = httpClientFactory;
        _printQueueService = printQueueService;
        _configuration = configuration;
        _logger = logger;
        
        // Load configuration
        _printerApiUrl = configuration.GetValue<string>("Receipt:PrinterApiUrl") ?? "http://localhost:5001";
        _restaurantName = configuration.GetValue<string>("Receipt:RestaurantName") ?? "Restaurant Kiosk";
        _restaurantAddress = configuration.GetValue<string>("Receipt:RestaurantAddress") ?? "123 Main Street";
        _restaurantPhone = configuration.GetValue<string>("Receipt:RestaurantPhone") ?? "+63 XXX XXX XXXX";
        _restaurantEmail = configuration.GetValue<string>("Receipt:RestaurantEmail") ?? "info@restaurant.com";
        _usePollingMode = configuration.GetValue<bool>("Receipt:UsePollingMode", true); // Default to polling mode
    }

    /// <inheritdoc/>
    public async Task<ReceiptData> GenerateReceiptDataAsync(Order order, decimal? amountPaid = null, decimal? change = null)
    {
        try
        {
            _logger.LogInformation("Generating receipt data for order: {OrderNumber}", order.OrderNumber);
            
            var receiptData = new ReceiptData
            {
                RestaurantName = _restaurantName,
                RestaurantAddress = _restaurantAddress,
                RestaurantPhone = _restaurantPhone,
                RestaurantEmail = _restaurantEmail,
                
                OrderNumber = order.OrderNumber,
                OrderDate = order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                CustomerName = order.CustomerName,
                
                SubTotal = order.SubTotal,
                Tax = order.Tax,
                ServiceCharge = order.ServiceCharge,
                TotalAmount = order.TotalAmount,
                
                PaymentMethod = order.PaymentMethod,
                AmountPaid = amountPaid,
                Change = change,
                
                Status = order.Status.ToString().ToUpper(),
                FooterMessage = "Thank you for your order! Please come again.",
                QrData = $"ORDER:{order.OrderNumber}"
            };
            
            // Add order items
            foreach (var item in order.OrderItems)
            {
                receiptData.Items.Add(new ReceiptItem
                {
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal,
                    Notes = item.Notes
                });
            }
            
            _logger.LogInformation("Receipt data generated successfully for order: {OrderNumber} with {ItemCount} items", 
                order.OrderNumber, receiptData.Items.Count);
            
            return receiptData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating receipt data for order: {OrderNumber}", order.OrderNumber);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PrintReceiptAsync(ReceiptData receiptData)
    {
        try
        {
            // Use polling mode if configured (Raspberry Pi polls for jobs)
            if (_usePollingMode && _printQueueService != null)
            {
                _logger.LogInformation("Queuing receipt for polling mode - order: {OrderNumber}", receiptData.OrderNumber);
                var jobId = await _printQueueService.QueuePrintJobAsync(receiptData);
                _logger.LogInformation("Receipt queued successfully with job ID: {JobId}", jobId);
                return true;
            }
            
            // Direct HTTP mode (push to Raspberry Pi)
            _logger.LogInformation("Sending receipt directly to printer for order: {OrderNumber}", receiptData.OrderNumber);
            
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var url = $"{_printerApiUrl}/api/receipt/print";
            var jsonContent = JsonSerializer.Serialize(receiptData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Sending print request to: {Url}", url);
            
            var response = await httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Receipt printed successfully for order: {OrderNumber}. Response: {Response}", 
                    receiptData.OrderNumber, responseContent);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to print receipt for order: {OrderNumber}. Status: {StatusCode}, Error: {Error}", 
                    receiptData.OrderNumber, response.StatusCode, errorContent);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending receipt to printer for order: {OrderNumber}. " +
                "Is the printer service running on Raspberry Pi?", receiptData.OrderNumber);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout sending receipt to printer for order: {OrderNumber}", receiptData.OrderNumber);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error printing receipt for order: {OrderNumber}", receiptData.OrderNumber);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PrintOrderReceiptAsync(Order order, decimal? amountPaid = null, decimal? change = null)
    {
        try
        {
            _logger.LogInformation("Printing receipt for order: {OrderNumber}", order.OrderNumber);
            
            var receiptData = await GenerateReceiptDataAsync(order, amountPaid, change);
            var success = await PrintReceiptAsync(receiptData);
            
            if (success)
            {
                _logger.LogInformation("Successfully printed receipt for order: {OrderNumber}", order.OrderNumber);
            }
            else
            {
                _logger.LogWarning("Failed to print receipt for order: {OrderNumber}", order.OrderNumber);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PrintOrderReceiptAsync for order: {OrderNumber}", order.OrderNumber);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestPrinterAsync()
    {
        try
        {
            _logger.LogInformation("Testing printer connection at: {PrinterApiUrl}", _printerApiUrl);
            
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            
            // Test health endpoint
            var healthUrl = $"{_printerApiUrl}/health";
            var healthResponse = await httpClient.GetAsync(healthUrl);
            
            if (!healthResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Printer service health check failed. Status: {StatusCode}", healthResponse.StatusCode);
                return false;
            }
            
            // Test print endpoint
            var testUrl = $"{_printerApiUrl}/api/receipt/test";
            var testResponse = await httpClient.PostAsync(testUrl, null);
            
            if (testResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Printer test successful");
                return true;
            }
            else
            {
                var errorContent = await testResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Printer test failed. Status: {StatusCode}, Error: {Error}", 
                    testResponse.StatusCode, errorContent);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error testing printer. Is the printer service running?");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing printer");
            return false;
        }
    }
}

