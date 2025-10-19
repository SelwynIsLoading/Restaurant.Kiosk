# Blazor Server Prerendering Fix - NavigationManager Error

## Problem

Getting error: `'RemoteNavigationManager' has not been initialized` when accessing payment pages (GCash, Maya, Card, Cash).

```
System.InvalidOperationException: 'RemoteNavigationManager' has not been initialized.
```

## Root Cause

Blazor Server uses **prerendering** by default. During prerendering:
- Components render on the server first (static HTML)
- Then hydrate on the client (become interactive)
- **NavigationManager.BaseUri is not available** during prerendering

### Where It Was Happening

**Checkout.razor (Line 372):**
```csharp
var baseUrl = $"{NavigationManager.BaseUri}";  // ❌ Fails during prerendering
```

**CashPayment.razor (Multiple lines):**
```csharp
await HttpClient.PostAsJsonAsync($"{NavigationManager.BaseUri}api/...", ...)  // ❌ Fails
```

---

## Solutions Applied

### Fix 1: Program.cs - Remove Scoped HttpClient Registration ✅ PRIMARY FIX

**Before (WRONG):**
```csharp
// Line 64-71 in Program.cs
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)  // ❌ Causes error during startup!
    };
});
```

**After (CORRECT):**
```csharp
// Removed entirely - components use IHttpClientFactory instead
builder.Services.AddHttpClient();  // Only keep factory registration
```

**This was the main culprit!** The scoped HttpClient was trying to access NavigationManager.BaseUri during service registration, causing the error on every page load.

### Fix 2: Checkout.razor - Use NavigationManager.Uri Instead

**Before (WRONG):**
```csharp
var baseUrl = $"{NavigationManager.BaseUri}";  // Not available during prerendering
```

**After (CORRECT):**
```csharp
// Extract base URL from current URI (always available)
var baseUrl = NavigationManager.Uri.Substring(0, NavigationManager.Uri.IndexOf("/checkout", StringComparison.OrdinalIgnoreCase));
if (!baseUrl.EndsWith("/"))
{
    baseUrl += "/";
}
```

**How It Works:**
- `NavigationManager.Uri` → Full current URL (e.g., `https://bochogs-kiosk.store/checkout`)
- Extract base → `https://bochogs-kiosk.store`
- Add trailing slash → `https://bochogs-kiosk.store/`

### Fix 2: CashPayment.razor - Use IHttpClientFactory

**Before (WRONG):**
```csharp
@inject HttpClient HttpClient

await HttpClient.PostAsJsonAsync($"{NavigationManager.BaseUri}api/...", ...)
```

**After (CORRECT):**
```csharp
@inject IHttpClientFactory HttpClientFactory

var httpClient = HttpClientFactory.CreateClient();
await httpClient.PostAsJsonAsync($"{NavigationManager.BaseUri}api/...", ...)
```

**Why This Works:**
- Creates new HttpClient instance per request
- Doesn't rely on scoped HttpClient with wrong BaseAddress
- NavigationManager.BaseUri used in method (after interactive render)

---

## Understanding Blazor Server Rendering

### Render Modes

```csharp
@rendermode InteractiveServer  // Your components use this
@attribute [StreamRendering]   // Checkout.razor also uses this
```

**Rendering Process:**
1. **Prerendering (Server-side)**
   - Component renders to static HTML
   - Sent to browser immediately
   - NavigationManager.BaseUri NOT available
   
2. **Interactive (After SignalR connects)**
   - WebSocket connection established
   - Component becomes interactive
   - NavigationManager.BaseUri NOW available

### What's Safe During Prerendering

✅ **Always Available:**
- `NavigationManager.Uri` - Current full URL
- `NavigationManager.ToAbsoluteUri()`
- `NavigationManager.ToBaseRelativePath()`
- `NavigationManager.NavigateTo()` (throws exception, but caught by framework)

❌ **NOT Available:**
- `NavigationManager.BaseUri` - Only after interactive render
- Some HttpClient configurations
- SignalR connections

---

## Alternative Solutions (If Issues Persist)

### Option 1: Inject HttpContext

Use HttpContext to get the base URL (works during prerendering):

```csharp
@inject IHttpContextAccessor HttpContextAccessor

private string GetBaseUrl()
{
    var httpContext = HttpContextAccessor.HttpContext;
    if (httpContext != null)
    {
        var request = httpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }
    
    // Fallback to NavigationManager (interactive mode)
    return NavigationManager.BaseUri;
}
```

Then register HttpContextAccessor in Program.cs:
```csharp
builder.Services.AddHttpContextAccessor();
```

### Option 2: Disable Prerendering

Remove `@attribute [StreamRendering]` and use only InteractiveServer:

```csharp
@rendermode InteractiveServer
// Remove: @attribute [StreamRendering]
```

**Pros:** NavigationManager.BaseUri always available  
**Cons:** Slower initial page load

### Option 3: Check if Interactive

Check before using NavigationManager.BaseUri:

```csharp
private async Task ProcessDigitalPayment()
{
    // Wait for interactive mode if needed
    await Task.Delay(100);  // Allow SignalR to connect
    
    var baseUrl = NavigationManager.BaseUri;
    // ...
}
```

---

## Testing the Fix

### 1. Rebuild and Deploy

```bash
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

### 2. Test Each Payment Method

**Cash Payment:**
```
1. Add items to cart
2. Go to checkout
3. Fill in customer info
4. Select "Cash" payment
5. Click "Place Order"
6. Should redirect to /cash-payment (no error)
```

**GCash Payment:**
```
1. Add items to cart
2. Go to checkout
3. Fill in customer info
4. Select "GCash" payment
5. Click "Place Order"
6. Should redirect to Xendit GCash page (no error)
```

**Maya Payment:**
```
1. Add items to cart
2. Go to checkout
3. Fill in customer info
4. Select "Maya" payment
5. Click "Place Order"
6. Should redirect to Xendit Maya page (no error)
```

**Card Payment:**
```
1. Add items to cart
2. Go to checkout
3. Fill in customer info
4. Select "Card" payment
5. Click "Place Order"
6. Should redirect to Xendit invoice page (no error)
```

### 3. Check Logs

```bash
# On VPS
sudo journalctl -u restaurant-kiosk -f

# Should NOT see:
# System.InvalidOperationException: 'RemoteNavigationManager' has not been initialized
```

---

## Related Issues

### Issue: HttpClient BaseAddress Wrong (Xendit)

**Symptoms:**
- API calls go to `https://api.xendit.co/` instead of your server
- 404 errors on internal APIs

**Solution:**
Use `IHttpClientFactory` and build full URLs:
```csharp
var httpClient = HttpClientFactory.CreateClient();
var url = $"{NavigationManager.BaseUri}api/cash-payment/init";
await httpClient.PostAsJsonAsync(url, payload);
```

---

## Best Practices for Blazor Server

### ✅ DO:
- Use `NavigationManager.Uri` when you need the current URL
- Use `IHttpClientFactory` for HTTP calls
- Check component is interactive before accessing BaseUri
- Test both initial load and interactive scenarios

### ❌ DON'T:
- Use `NavigationManager.BaseUri` in OnInitialized/OnInitializedAsync
- Rely on scoped HttpClient when multiple external APIs exist
- Assume NavigationManager is always ready
- Use BaseUri without checking interactive state

---

## Summary of Fixes

| File | Line | What Changed | Why |
|------|------|-------------|-----|
| **Program.cs** | 64-71 | **REMOVED scoped HttpClient** | ✅ **Main fix** - Was causing error during service registration |
| **Checkout.razor** | 372-378 | Use `Uri.Substring()` instead of `BaseUri` | BaseUri not available during prerender |
| **CashPayment.razor** | 10 | Changed to `IHttpClientFactory` | Avoid scoped HttpClient issues |
| **CashPayment.razor** | 293, 384, 429 | Create HttpClient per call | Each call gets fresh client |

---

## Verification

After deploying fixes, verify:

✅ **All payment methods work:**
- [ ] Cash payment redirects correctly
- [ ] GCash creates payment link
- [ ] Maya creates payment link  
- [ ] Card creates invoice

✅ **No errors in logs:**
```bash
sudo journalctl -u restaurant-kiosk -f | grep -i "navigationmanager\|initialized"
# Should be empty (no errors)
```

✅ **URLs are correct:**
- Callback URLs use your domain (not localhost)
- Success/failure URLs use your domain
- No references to Xendit API for internal calls

---

## If Still Having Issues

### Check Render Mode Configuration

```csharp
// In Program.cs - verify these are present:
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();  // ✅ This is needed

// Later:
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();  // ✅ This is needed
```

### Add HttpContextAccessor (If Needed)

```csharp
// In Program.cs, after other services:
builder.Services.AddHttpContextAccessor();
```

Then use it to get base URL:
```csharp
@inject IHttpContextAccessor HttpContextAccessor

var request = HttpContextAccessor.HttpContext?.Request;
var baseUrl = $"{request.Scheme}://{request.Host}/";
```

---

## Summary

**Problem:** NavigationManager.BaseUri not available during prerendering  
**Affected:** GCash, Maya, Card, Cash payment pages  
**Solution:** Use NavigationManager.Uri or IHttpContextAccessor  
**Status:** ✅ Fixed in Checkout.razor and CashPayment.razor  

Deploy the updated files and all payment methods should work! 🚀

