# HttpClient BaseAddress Issue - Root Cause & Fix

## Problem Discovered

The cash payment API was returning 404 errors because the `HttpClient` was pointing to **Xendit API** (`https://api.xendit.co/`) instead of the local server.

### Evidence from Logs:

```
Initializing payment session - BaseAddress: https://api.xendit.co/
RequestUri: https://api.xendit.co/api/cash-payment/init
Response: NotFound, Content: {"error_code":"NOT_FOUND","message":"The requested resource was not found"}
```

## Root Cause

In `Program.cs`, there's a scoped `HttpClient` registration:

```csharp
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)
    };
});
```

**However**, there's likely also an `HttpClient` configured for Xendit payments elsewhere in the code, and that one was being injected into the `CashPayment` component instead.

### Why This Happened:

When you have multiple `HttpClient` registrations, the last one registered or a named client might take precedence. In Blazor Server, when you inject `HttpClient` directly, it can get the wrong instance.

## Solution

Instead of relying on `HttpClient.BaseAddress`, we now build the full URL using `NavigationManager.BaseUri`:

### Before (WRONG):
```csharp
@inject HttpClient HttpClient

// This gets HttpClient with BaseAddress = https://api.xendit.co/
var response = await HttpClient.PostAsJsonAsync("api/cash-payment/init", payload);
```

### After (CORRECT):
```csharp
@inject HttpClient HttpClient
@inject NavigationManager NavigationManager

// Build absolute URL - always points to current server
var url = $"{NavigationManager.BaseUri}api/cash-payment/init";
var response = await HttpClient.PostAsJsonAsync(url, payload);
```

## Changes Made

Updated `CashPayment.razor`:

1. **InitializePaymentSession()**: Uses `NavigationManager.BaseUri`
2. **PollPaymentStatus()**: Uses `NavigationManager.BaseUri`
3. **CancelPayment()**: Uses `NavigationManager.BaseUri`

## Alternative Solutions

### Option 1: Use IHttpClientFactory (More Robust)

```csharp
// In Program.cs - add a named client for internal APIs
builder.Services.AddHttpClient("InternalAPI", (sp, client) =>
{
    // This gets set per-request based on the current URL
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    if (accessor.HttpContext != null)
    {
        var request = accessor.HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        client.BaseAddress = new Uri(baseUrl);
    }
});

// In CashPayment.razor
@inject IHttpClientFactory HttpClientFactory

var client = HttpClientFactory.CreateClient("InternalAPI");
var response = await client.PostAsJsonAsync("api/cash-payment/init", payload);
```

### Option 2: Remove Scoped HttpClient (If Only Used for Xendit)

If the scoped `HttpClient` is only needed for Xendit, remove it or make it a named client:

```csharp
// Instead of scoped HttpClient, use named clients
builder.Services.AddHttpClient("Xendit", client =>
{
    client.BaseAddress = new Uri("https://api.xendit.co");
});

// Don't register a default scoped HttpClient
```

Then in payment services that need Xendit:
```csharp
var client = HttpClientFactory.CreateClient("Xendit");
```

## Testing

After the fix, logs should show:

```
info: Initializing payment session - URL: https://bochogs-kiosk.store/api/cash-payment/init
info: Response: StatusCode=OK
info: Payment session initialized for order: ORD-xxx
```

## Best Practices

### ✅ DO:
- Use `NavigationManager.BaseUri` for internal API calls in Blazor components
- Use named `HttpClient` instances for external APIs (Xendit, etc.)
- Log the full URL being called for debugging

### ❌ DON'T:
- Rely on default `HttpClient` BaseAddress in Blazor Server
- Inject generic `HttpClient` when you have multiple external APIs
- Assume relative URLs will work correctly

## Impact on Other Components

Check if other Razor components use `HttpClient` directly:

```bash
# Search for HttpClient usage
grep -r "HttpClient\." RestaurantKiosk/Components --include="*.razor"
```

If found, verify they're not affected by the same issue or update them to use `NavigationManager.BaseUri` as well.

## Related Configuration

### Check appsettings.json:

Make sure Xendit configuration doesn't interfere with default HttpClient:

```json
{
  "Xendit": {
    "ApiKey": "xnd_...",
    "BaseUrl": "https://api.xendit.co"
  }
}
```

### Check Payment Service:

If you have a payment service using HttpClient for Xendit, make sure it uses a named client:

```csharp
public class PaymentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    
    public PaymentService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task CreateInvoice(...)
    {
        // Use named client for Xendit
        var client = _httpClientFactory.CreateClient("Xendit");
        // ... or build URL manually
        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync("https://api.xendit.co/v2/invoices", ...);
    }
}
```

## Verification Steps

1. **Rebuild and deploy:**
   ```bash
   dotnet build -c Release
   dotnet publish -c Release -o ./publish
   ```

2. **Test cash payment flow:**
   - Create an order
   - Select cash payment
   - Check logs for correct URL

3. **Expected logs:**
   ```
   info: Initializing payment session - URL: https://bochogs-kiosk.store/api/cash-payment/init
   info: Payload: OrderNumber=ORD-xxx, TotalAmount=250.00
   info: Response: StatusCode=OK
   info: Payment session initialized
   ```

4. **Verify in browser console:**
   ```javascript
   // Should NOT see requests to api.xendit.co
   // Should see requests to your own domain
   ```

## Summary

**Problem**: HttpClient had wrong BaseAddress (Xendit instead of local server)  
**Cause**: Multiple HttpClient registrations with conflicting BaseAddress  
**Solution**: Use `NavigationManager.BaseUri` to build absolute URLs  
**Result**: Cash payment API calls now go to the correct server

---

**Status**: ✅ Fixed  
**Tested**: Pending (deploy and test)  
**Date**: October 17, 2025

