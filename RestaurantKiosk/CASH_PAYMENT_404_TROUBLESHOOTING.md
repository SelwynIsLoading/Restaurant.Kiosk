# Cash Payment 404 NotFound - Troubleshooting Guide

## Problem

Getting **404 NotFound** error when calling `/api/cash-payment/init` from the `CashPayment.razor` component.

```csharp
// Line 383 in CashPayment.razor
var response = await HttpClient.PostAsJsonAsync("/api/cash-payment/init", new
{
    OrderNumber = OrderNumber,
    TotalAmount = TotalRequired
});

// Line 398: Logs "Failed to initialize payment session: NotFound"
```

---

## Root Cause Analysis

The issue is likely related to how the `HttpClient` is configured in Blazor Server.

### Current HttpClient Configuration (Program.cs line 64-71):

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

### Problem:

When using `HttpClient` with a `BaseAddress` in Blazor Server, **relative URLs should NOT start with a slash** (`/`).

**Current (WRONG):**
```csharp
HttpClient.PostAsJsonAsync("/api/cash-payment/init", ...) // ❌ Starts with /
```

**Should be:**
```csharp
HttpClient.PostAsJsonAsync("api/cash-payment/init", ...) // ✅ No leading slash
```

### Why?

When you use a **leading slash** (`/`) with `HttpClient.BaseAddress`:
- `BaseAddress`: `http://localhost:5000/`
- Request URL: `/api/cash-payment/init`
- **Result**: Ignores BaseAddress → tries to call `http://localhost/api/cash-payment/init` (wrong!)

When you use a **relative URL** (no leading slash):
- `BaseAddress`: `http://localhost:5000/`
- Request URL: `api/cash-payment/init`
- **Result**: Combines correctly → `http://localhost:5000/api/cash-payment/init` ✅

---

## Solution 1: Fix the Razor Component URLs ✅ RECOMMENDED

Update `CashPayment.razor` to use relative URLs without leading slashes:

```csharp
// BEFORE (line 383):
var response = await HttpClient.PostAsJsonAsync("/api/cash-payment/init", new

// AFTER:
var response = await HttpClient.PostAsJsonAsync("api/cash-payment/init", new
```

```csharp
// BEFORE (line 293):
var response = await HttpClient.GetAsync($"/api/cash-payment/status/{OrderNumber}");

// AFTER:
var response = await HttpClient.GetAsync($"api/cash-payment/status/{OrderNumber}");
```

```csharp
// BEFORE (line 413):
var response = await HttpClient.PostAsync($"/api/cash-payment/cancel/{OrderNumber}", null);

// AFTER:
var response = await HttpClient.PostAsync($"api/cash-payment/cancel/{OrderNumber}", null);
```

---

## Solution 2: Use Named HttpClient (Alternative)

Instead of using the scoped HttpClient, create a named HttpClient for API calls:

### Step 1: Update Program.cs

```csharp
// Replace the scoped HttpClient registration (lines 64-71) with:

// Add named HttpClient for internal API calls
builder.Services.AddHttpClient("ServerAPI", client =>
{
    // BaseAddress will be set to the server's own URL
    // This is set at runtime based on the request
});

// Keep the scoped HttpClient for Blazor components (if needed for other uses)
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)
    };
});
```

### Step 2: Update CashPayment.razor

```csharp
// Add injection for IHttpClientFactory
@inject IHttpClientFactory HttpClientFactory

// In the methods, use:
private async Task InitializePaymentSession()
{
    try
    {
        var httpClient = HttpClientFactory.CreateClient("ServerAPI");
        httpClient.BaseAddress = new Uri(NavigationManager.BaseUri);
        
        var response = await httpClient.PostAsJsonAsync("api/cash-payment/init", new
        {
            OrderNumber = OrderNumber,
            TotalAmount = TotalRequired
        });
        
        // ... rest of the code
    }
}
```

---

## Solution 3: Use Absolute URL (Quick Test)

For testing purposes, you can use absolute URLs:

```csharp
var response = await HttpClient.PostAsJsonAsync(
    $"{NavigationManager.BaseUri}api/cash-payment/init", 
    new { ... }
);
```

This is NOT recommended for production but can help verify the controller is working.

---

## Verification Steps

### 1. Check if Controllers are Accessible

Test the API endpoint directly:

**Using curl:**
```bash
curl -X POST http://localhost:5000/api/cash-payment/init \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","totalAmount":100.00}'
```

**Using browser console (F12):**
```javascript
fetch('http://localhost:5000/api/cash-payment/init', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ orderNumber: 'TEST-001', totalAmount: 100.00 })
})
.then(r => r.json())
.then(data => console.log(data))
```

### 2. Check HttpClient BaseAddress

Add logging to see what BaseAddress is being used:

```csharp
protected override async Task OnInitializedAsync()
{
    // Add this debug log
    Logger.LogInformation("HttpClient BaseAddress: {BaseAddress}", HttpClient.BaseAddress);
    
    // ... rest of initialization
}
```

### 3. Check Browser Network Tab

1. Open browser developer tools (F12)
2. Go to Network tab
3. Try to initialize payment
4. Look for the failed request
5. Check what URL it's trying to call

---

## Quick Fix Implementation

Here's the exact changes needed in `CashPayment.razor`:

```csharp
// Line 293 - PollPaymentStatus method
var response = await HttpClient.GetAsync($"api/cash-payment/status/{OrderNumber}");  // Remove leading /

// Line 383 - InitializePaymentSession method
var response = await HttpClient.PostAsJsonAsync("api/cash-payment/init", new  // Remove leading /
{
    OrderNumber = OrderNumber,
    TotalAmount = TotalRequired
});

// Line 413 - CancelPayment method
var response = await HttpClient.PostAsync($"api/cash-payment/cancel/{OrderNumber}", null);  // Remove leading /
```

---

## Testing After Fix

1. Restart the application
2. Navigate to cash payment page: `/cash-payment?orderNumber=TEST-001&totalAmount=250`
3. Check browser console for logs
4. Verify "Payment session initialized" appears in logs
5. Test with actual order flow

---

## Common Issues

### Issue 1: Still getting 404 after removing leading slash

**Cause:** Browser cache or old build

**Solution:**
```bash
# Clean and rebuild
dotnet clean
dotnet build
# Hard refresh browser (Ctrl+Shift+R)
```

### Issue 2: CORS error instead of 404

**Cause:** If you see CORS error, it means the URL is being treated as external

**Solution:** This confirms the BaseAddress is not set correctly - use Solution 1 (remove leading slashes)

### Issue 3: 401 Unauthorized

**Cause:** API key is required but not provided

**Solution:** Check `appsettings.json` - set `CashPayment:ApiKey` to `null` for testing:
```json
{
  "CashPayment": {
    "ApiKey": null
  }
}
```

---

## Expected Behavior

After fix, you should see:

**Console logs:**
```
[INF] Payment session initialized for order: TEST-001
[INF] Started polling for payment status updates (1 second interval)
```

**Browser Network tab:**
```
POST http://localhost:5000/api/cash-payment/init → 200 OK
GET http://localhost:5000/api/cash-payment/status/TEST-001 → 200 OK
```

**UI:**
- Shows "Active" payment status
- Displays total amount and amount inserted (0 initially)
- Progress bar visible
- "Please Insert Cash" instructions shown

---

## Recommended Solution

**Remove leading slashes from all API URLs in CashPayment.razor** ✅

This is the simplest and most correct solution. The leading slash is causing the HttpClient to ignore the BaseAddress and try to make an absolute request.

---

**Quick Copy-Paste Fix:**

Replace these lines in `Components/Pages/CashPayment.razor`:

```csharp
// Line ~293
var response = await HttpClient.GetAsync($"api/cash-payment/status/{OrderNumber}");

// Line ~383  
var response = await HttpClient.PostAsJsonAsync("api/cash-payment/init", new { ... });

// Line ~413
var response = await HttpClient.PostAsync($"api/cash-payment/cancel/{OrderNumber}", null);
```

Save, rebuild, and test! 🚀

