# SignalR Real-Time Order Updates - Troubleshooting Guide

## Issue: Orders Not Auto-Refreshing

If orders aren't appearing automatically on the Order Management page after payment, follow these steps:

### Step 1: Check SignalR Connection Status

1. Open the Order Management page (`/admin/orders`)
2. Look at the **Connection** status indicator in the statistics cards
3. It should show **"Connected"** (green badge)

**If it shows "Disconnected" or "Error":**
- Check browser console (F12) for JavaScript errors
- Ensure the application is running
- Verify that WebSocket connections are allowed (not blocked by firewall/proxy)

### Step 2: Check Server Logs

When a payment is successful, you should see these logs in order:

```
[Information] Order status updated to Paid for ExternalId: {externalId}
[Information] Product quantities decreased for order ID: {orderId}
[Information] Sending SignalR notification for order: {orderNumber} (ID: {orderId})
[Information] Successfully sent SignalR notification for order: {orderNumber}
```

**If you don't see these logs:**
- Payment webhook may not be reaching your server
- Check the payment provider's webhook configuration

### Step 3: Test SignalR Manually

Use the test endpoint to verify SignalR is working:

#### Using PowerShell:
```powershell
# First, get an order ID from the database (status = Paid)
# Then test the notification:
Invoke-RestMethod -Uri "https://localhost:PORT/api/payment/test-notification/ORDER_ID" -Method POST
```

#### Using curl:
```bash
curl -X POST https://localhost:PORT/api/payment/test-notification/ORDER_ID
```

#### Using browser:
Navigate to: `https://localhost:PORT/api/payment/test-notification/ORDER_ID`
(Replace PORT with your port number and ORDER_ID with an actual order ID)

**Expected result:** The order should appear on the Order Management page immediately without refresh.

### Step 4: Check Browser Console

Open browser Developer Tools (F12) and check the Console tab for errors:

**What to look for:**
```
Received new order notification: ORD-20241014-XXXXX (ID: 123)
```

**Common errors:**
- SignalR connection failed
- CORS errors (check if your app is running on the correct URL)
- WebSocket connection refused

### Step 5: Verify Order Status

Check the database to ensure:
1. Order exists in the `Orders` table
2. Order status is `Paid` (value = 1)
3. Order has an `ExternalId` that matches the payment

**SQL Query:**
```sql
SELECT Id, OrderNumber, Status, ExternalId, CreatedAt, PaidAt
FROM Orders
ORDER BY CreatedAt DESC
LIMIT 10;
```

### Step 6: Test End-to-End Flow

1. **Open Order Management page** in one browser window/tab
2. **Open Kiosk** in another browser window/tab
3. **Place a test order** using test payment credentials
4. **Watch the Order Management page** - the order should appear automatically

### Common Issues and Solutions

#### Issue: Connection shows "Connected" but orders don't appear

**Solution:**
1. Check that orders have status = `Paid` (not `Pending`)
2. Verify the SignalR event name is exactly "NewOrder" (case-sensitive)
3. Check browser console for JavaScript errors
4. Try refreshing the Order Management page

#### Issue: SignalR keeps disconnecting

**Solution:**
1. Check server logs for errors
2. Verify application pool settings (if using IIS)
3. Ensure the application isn't being restarted frequently
4. Check memory/resource usage

#### Issue: Notifications delayed by several seconds

**Solution:**
1. This is normal - there's a 100ms delay after DB commit
2. Network latency can add additional delay
3. Consider reducing the delay in PaymentController if needed

#### Issue: Multiple duplicate orders appearing

**Solution:**
1. This might indicate multiple webhook deliveries
2. Add idempotency handling for webhooks
3. Check webhook provider settings

### Debug Mode

To enable detailed logging, update `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "Microsoft.AspNetCore.Http.Connections": "Debug",
      "RestaurantKiosk.Hubs": "Debug",
      "RestaurantKiosk.Controllers.PaymentController": "Debug"
    }
  }
}
```

### Testing Checklist

- [ ] Order Management page shows "Connected" status
- [ ] Server logs show SignalR notification being sent
- [ ] Browser console shows notification being received
- [ ] Order has status = Paid in database
- [ ] Test endpoint successfully triggers notification
- [ ] No errors in browser console
- [ ] No errors in server logs
- [ ] WebSockets are enabled and not blocked

### Still Having Issues?

1. **Restart the application** - Sometimes SignalR needs a fresh start
2. **Clear browser cache** - Old JavaScript may be cached
3. **Try a different browser** - Rule out browser-specific issues
4. **Check firewall settings** - Ensure WebSocket connections are allowed
5. **Verify database connections** - Ensure orders are being saved correctly

### Advanced Debugging

#### Check SignalR Hub Endpoint
Navigate to: `https://localhost:PORT/orderhub/negotiate`
You should get a JSON response with connection info.

#### Monitor Network Tab
Open Developer Tools → Network tab → Filter by "WS" (WebSocket)
You should see an active WebSocket connection to `/orderhub`

#### Check Blazor Circuit
The Order Management page uses Blazor Server, which also uses SignalR.
Ensure the Blazor circuit is active (check for `_blazor` connections in Network tab).

### Key Changes Made

The following changes were made to fix threading issues with SignalR:

1. **OrderManagement.razor** - SignalR event handlers now use `InvokeAsync()`:
   ```csharp
   hubConnection.On<int, string>("NewOrder", async (orderId, orderNumber) =>
   {
       await InvokeAsync(async () =>
       {
           await RefreshOrders();
           StateHasChanged();
       });
   });
   ```

2. **PaymentController.cs** - Added delay before sending notification:
   ```csharp
   await Task.Delay(100); // Ensure DB commit
   await _orderHubContext.Clients.All.SendAsync("NewOrder", order.Id, order.OrderNumber);
   ```

These changes ensure that:
- SignalR callbacks execute on the correct Blazor component thread
- UI updates properly when notifications are received
- Database commits complete before notifications are sent

## Need More Help?

Check the application logs at:
- Development: Console output
- Production: Application logs folder

Look for entries with these categories:
- `RestaurantKiosk.Hubs.OrderHub`
- `RestaurantKiosk.Controllers.PaymentController`
- `Microsoft.AspNetCore.SignalR`

