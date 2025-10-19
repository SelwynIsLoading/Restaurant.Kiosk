using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestaurantKiosk.Data.Services;

public class PaymentService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;
    private readonly HttpClient _httpClient;

    public PaymentService(IConfiguration configuration, ILogger<PaymentService> logger, HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
        
        var apiKey = _configuration["Xendit:ApiKey"];
        // Xendit requires Basic Auth with secret key as username and empty password
        var authString = $"{apiKey}:";
        var encodedAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authString));
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedAuth);
        
        // Set the base address for Xendit API
        _httpClient.BaseAddress = new Uri("https://api.xendit.co/");
    }

    public async Task<InvoiceResponse> CreateInvoiceAsync(
        string externalId,
        decimal amount,
        string customerName,
        string customerEmail,
        string description,
        string successRedirectUrl,
        string failureRedirectUrl)
    {
        try
        {
            var request = new
            {
                external_id = externalId,
                amount = (long)amount,
                description = description,
                invoice_duration = 3600,
                currency = "PHP",
                customer = new
                {
                    given_names = customerName,
                    email = customerEmail
                },
                customer_notification_preference = new
                {
                    invoice_created = new[] { "email" },
                    invoice_reminder = new[] { "email" },
                    invoice_paid = new[] { "email" }
                },
                success_redirect_url = successRedirectUrl,
                failure_redirect_url = failureRedirectUrl,
                payment_methods = new[] { "EWALLET", "BANK_TRANSFER", "RETAIL_OUTLET", "CREDIT_CARD" }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("v2/invoices", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<InvoiceResponse>(responseContent);
                _logger.LogInformation("Invoice created successfully: {InvoiceId}", result?.Id);
                return result ?? new InvoiceResponse();
            }
            else
            {
                _logger.LogError("Failed to create invoice: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new Exception($"Failed to create invoice: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invoice for external ID: {ExternalId}", externalId);
            throw;
        }
    }

    public async Task<InvoiceResponse> GetInvoiceAsync(string invoiceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"v2/invoices/{invoiceId}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<InvoiceResponse>(content) ?? new InvoiceResponse();
            }
            else
            {
                _logger.LogError("Failed to get invoice: {StatusCode} - {Content}", response.StatusCode, content);
                throw new Exception($"Failed to get invoice: {content}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoice: {InvoiceId}", invoiceId);
            throw;
        }
    }

    public async Task<EWalletChargeResponse> CreateGCashPaymentAsync(
        string externalId,
        decimal amount,
        string customerPhone,
        string callbackUrl)
    {
        try
        {
            // Format phone number to international format (+63 for Philippines)
            var formattedPhone = FormatPhoneNumber(customerPhone);
            
            // Build webhook callback URL for payment status updates
            var webhookCallbackUrl = _configuration["Xendit:CallbackUrl"] 
                ?? $"{_configuration["BaseUrl"]}/api/callback/payment/callback";
            
            var request = new
            {
                reference_id = externalId,
                currency = "PHP",
                amount = (float)amount,
                checkout_method = "ONE_TIME_PAYMENT",
                channel_code = "PH_GCASH",
                channel_properties = new
                {
                    success_redirect_url = callbackUrl,
                    failure_redirect_url = callbackUrl
                },
                customer = new
                {
                    mobile_number = formattedPhone
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add callback URL header for webhook notifications
            var request2 = new HttpRequestMessage(HttpMethod.Post, "ewallets/charges");
            request2.Content = content;
            request2.Headers.Add("x-callback-url", webhookCallbackUrl);
            
            var response = await _httpClient.SendAsync(request2);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<EWalletChargeResponse>(responseContent);
                _logger.LogInformation("GCash payment created successfully: {ChargeId}", result?.Id);
                return result ?? new EWalletChargeResponse();
            }
            else
            {
                _logger.LogError("Failed to create GCash payment: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new Exception($"Failed to create GCash payment: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GCash payment for external ID: {ExternalId}", externalId);
            throw;
        }
    }

    public async Task<EWalletChargeResponse> CreateMayaPaymentAsync(
        string externalId,
        decimal amount,
        string customerPhone,
        string callbackUrl)
    {
        try
        {
            // Format phone number to international format (+63 for Philippines)
            var formattedPhone = FormatPhoneNumber(customerPhone);
            
            // Build webhook callback URL for payment status updates
            var webhookCallbackUrl = _configuration["Xendit:CallbackUrl"] 
                ?? $"{_configuration["BaseUrl"]}/api/callback/payment/callback";
            
            var request = new
            {
                reference_id = externalId,
                currency = "PHP",
                amount = (float)amount,
                checkout_method = "ONE_TIME_PAYMENT",
                channel_code = "PH_PAYMAYA",
                channel_properties = new
                {
                    success_redirect_url = callbackUrl,
                    failure_redirect_url = callbackUrl,
                    cancel_redirect_url = callbackUrl
                },
                customer = new
                {
                    mobile_number = formattedPhone
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add callback URL header for webhook notifications
            var request2 = new HttpRequestMessage(HttpMethod.Post, "ewallets/charges");
            request2.Content = content;
            request2.Headers.Add("x-callback-url", webhookCallbackUrl);
            
            var response = await _httpClient.SendAsync(request2);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<EWalletChargeResponse>(responseContent);
                _logger.LogInformation("Maya payment created successfully: {ChargeId}", result?.Id);
                return result ?? new EWalletChargeResponse();
            }
            else
            {
                _logger.LogError("Failed to create Maya payment: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new Exception($"Failed to create Maya payment: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Maya payment for external ID: {ExternalId}", externalId);
            throw;
        }
    }

    public async Task<EWalletChargeResponse> GetEWalletChargeAsync(string chargeId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"ewallets/charges/{chargeId}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<EWalletChargeResponse>(content) ?? new EWalletChargeResponse();
            }
            else
            {
                _logger.LogError("Failed to get e-wallet charge: {StatusCode} - {Content}", response.StatusCode, content);
                throw new Exception($"Failed to get e-wallet charge: {content}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving e-wallet charge: {ChargeId}", chargeId);
            throw;
        }
    }

    public bool VerifyWebhook(string webhookToken, string requestBody, string xenditSignature)
    {
        try
        {
            var expectedToken = _configuration["Xendit:WebhookToken"];
            return webhookToken == expectedToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying webhook");
            return false;
        }
    }

    private string FormatPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return "+639123456789"; // Default test number

        // Remove all non-digit characters
        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
        
        // Handle different formats
        if (digitsOnly.StartsWith("63"))
        {
            // Already has country code, add +
            return "+" + digitsOnly;
        }
        else if (digitsOnly.StartsWith("0"))
        {
            // Remove leading 0 and add +63
            return "+63" + digitsOnly.Substring(1);
        }
        else if (digitsOnly.Length == 10)
        {
            // Assume it's a local number, add +63
            return "+63" + digitsOnly;
        }
        else
        {
            // Default: assume it needs +63 prefix
            return "+63" + digitsOnly;
        }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}

public class InvoiceResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("external_id")]
    public string ExternalId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("invoice_url")]
    public string InvoiceUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("amount")]
    public long Amount { get; set; }
    
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class EWalletChargeResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("reference_id")]
    public string ReferenceId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("charge_amount")]
    public float Amount { get; set; }
    
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
    
    [JsonPropertyName("actions")]
    public EWalletActions? Actions { get; set; }
}

public class EWalletActions
{
    [JsonPropertyName("mobile_web_checkout_url")]
    public string? MobileWebCheckoutUrl { get; set; }
    
    [JsonPropertyName("desktop_web_checkout_url")]
    public string? DesktopWebCheckoutUrl { get; set; }
}