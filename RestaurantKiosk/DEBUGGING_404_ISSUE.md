# Debugging 404 Issue - Step by Step

## Current Situation

Getting 404 NotFound when calling `/api/cash-payment/init` from the Blazor component, even though:
- ✅ `AddControllers()` is called (Program.cs line 20)
- ✅ `MapControllers()` is called (Program.cs line 130)
- ✅ Controller exists (`CashPaymentController.cs`)
- ✅ Route is correct `[Route("api/cash-payment")]`

## Step 1: Test API Directly on Server

SSH to your VPS and run:

```bash
# Make script executable
chmod +x test-cash-payment-api.sh

# Run test (replace with your actual URL if different)
./test-cash-payment-api.sh http://localhost:5000
```

**If curl gets 200 OK but browser gets 404:**
→ It's a client-side issue (HttpClient configuration)

**If curl also gets 404:**
→ It's a server-side issue (controller not registered)

---

## Step 2: Check Your Deployment

### A. Verify the app is actually running:

```bash
sudo systemctl status restaurant-kiosk
# or whatever your service is called
sudo systemctl status kestrel-restaurant-kiosk
```

### B. Check which DLL is being run:

```bash
# Find the service file
cat /etc/systemd/system/restaurant-kiosk.service

# Look for the ExecStart line - it should point to your DLL
# Example: /usr/bin/dotnet /var/www/kiosk/RestaurantKiosk.dll
```

### C. Verify the DLL contains the controller:

```bash
# Go to your deployed directory
cd /var/www/kiosk  # or wherever your app is deployed

# Check if controller exists
ls -la RestaurantKiosk.Controllers.dll 2>/dev/null || echo "Controllers in main DLL"

# Check deployment date
ls -lh RestaurantKiosk.dll
```

**If the file is old:**
→ You need to redeploy with the latest code

---

## Step 3: Check Application Logs (Enhanced Logging)

With the updated logging, try creating an order again and check logs:

```bash
sudo journalctl -u restaurant-kiosk -f
```

You should now see:
```
info: Initializing payment session - BaseAddress: <url>, RelativeUrl: api/cash-payment/init
info: Payload: OrderNumber=ORD-xxx, TotalAmount=100
info: Response: StatusCode=404, RequestUri=<full-url>
```

**Look at the RequestUri** - this will show you the EXACT URL being called!

---

## Step 4: Common Issues & Solutions

### Issue A: Controllers Not Registered

**Check Program.cs:**
```bash
grep -A2 -B2 "MapControllers\|AddControllers" RestaurantKiosk/Program.cs
```

Should show:
```csharp
builder.Services.AddControllers();  // Line ~20
// ... later ...
app.MapControllers();  // Line ~130
```

**If missing, add them and redeploy.**

### Issue B: Wrong Base URL

If logs show `BaseAddress: null`:

```csharp
// Program.cs - check HttpClient registration (around line 64-71)
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri) // Make sure this is here
    };
});
```

### Issue C: Nginx Reverse Proxy Issues

If using nginx, check configuration:

```bash
sudo cat /etc/nginx/sites-available/default | grep -A10 "location"
```

Should have something like:
```nginx
location / {
    proxy_pass http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection keep-alive;
    proxy_set_header Host $host;
    proxy_cache_bypass $http_upgrade;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

### Issue D: Antiforgery Token Issues

Try temporarily disabling antiforgery for testing:

```csharp
// Program.cs - comment out temporarily
// app.UseAntiforgery();
```

Then rebuild and test. If it works, antiforgery is the issue.

---

## Step 5: Quick Manual Test

### From Browser Console (F12):

```javascript
// Test if endpoint exists
fetch('https://bochogs-kiosk.store/api/cash-payment/active-sessions')
  .then(r => {
    console.log('Status:', r.status);
    return r.json();
  })
  .then(data => console.log('Data:', data))
  .catch(e => console.error('Error:', e));

// Test init endpoint
fetch('https://bochogs-kiosk.store/api/cash-payment/init', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ orderNumber: 'TEST-001', totalAmount: 100.00 })
})
  .then(r => {
    console.log('Status:', r.status);
    return r.text();
  })
  .then(data => console.log('Response:', data))
  .catch(e => console.error('Error:', e));
```

---

## Step 6: Force Rebuild and Redeploy

If nothing else works, do a clean rebuild:

```bash
# On development machine
cd RestaurantKiosk
dotnet clean
dotnet build -c Release
dotnet publish -c Release -o ./publish

# Deploy to VPS
scp -r publish/* user@your-vps:/var/www/kiosk/

# On VPS
sudo systemctl restart restaurant-kiosk
sudo systemctl status restaurant-kiosk
```

---

## Step 7: Check for Route Conflicts

Make sure no other controllers or endpoints are using the same route:

```bash
# Search for other api/cash-payment routes
grep -r "api/cash-payment" RestaurantKiosk/ --include="*.cs"
```

---

## Expected Working Behavior

When working correctly, you should see:

**Logs:**
```
info: Initializing payment session - BaseAddress: https://bochogs-kiosk.store/, RelativeUrl: api/cash-payment/init
info: Payload: OrderNumber=ORD-20251017-29E46B79, TotalAmount=250.00
info: Response: StatusCode=200, RequestUri=https://bochogs-kiosk.store/api/cash-payment/init
info: Payment session initialized for order: ORD-20251017-29E46B79
```

**Browser Network Tab:**
```
POST https://bochogs-kiosk.store/api/cash-payment/init → 200 OK
```

---

## Quick Checklist

Run through this checklist:

- [ ] Service is running: `sudo systemctl status restaurant-kiosk`
- [ ] Controllers registered in Program.cs
- [ ] DLL is up to date (check file timestamp)
- [ ] Test endpoint with curl (use test script)
- [ ] Check enhanced logs for actual URL being called
- [ ] Nginx config correct (if using reverse proxy)
- [ ] No antiforgery blocking the request
- [ ] BaseAddress is set correctly
- [ ] No route conflicts

---

## Still Not Working?

Please share:

1. **Output of test script:**
   ```bash
   ./test-cash-payment-api.sh http://localhost:5000
   ```

2. **Enhanced logs** (with new logging added):
   ```bash
   sudo journalctl -u restaurant-kiosk -n 100 | grep -A5 -B5 "Initializing payment"
   ```

3. **Program.cs snippet** (lines 15-25 and 125-135):
   ```bash
   sed -n '15,25p;125,135p' RestaurantKiosk/Program.cs
   ```

4. **Service configuration:**
   ```bash
   systemctl cat restaurant-kiosk
   ```

This will help diagnose the exact issue!

