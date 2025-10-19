# Cash Payment System - Polling Architecture

## Overview

The cash payment system has been refactored from **SignalR (WebSocket)** to **HTTP Polling** for simpler, more reliable communication.

## Architecture Changes

### Before (SignalR/WebSocket)
```
Arduino → Python (Pi) → POST to VPS → SignalR Hub → WebSocket → Browser
                                    ↓
                              Update _activeSessions
```

### After (Polling)
```
Arduino → Python (Pi) → POST to VPS → Update _activeSessions
                                            ↑
Browser ← GET /api/cash-payment/status/{order} (every 1 second)
```

## Benefits of Polling

1. ✅ **Simpler** - Just HTTP GET requests, no WebSocket connection management
2. ✅ **More Reliable** - No connection drops or reconnection issues
3. ✅ **Works Everywhere** - No proxy/firewall issues with WebSockets
4. ✅ **Easier to Debug** - Standard HTTP requests visible in network tab
5. ✅ **Acceptable Latency** - 1-second polling is fast enough for cash insertion feedback

## Changes Made

### Backend Changes

**File: `CashPaymentController.cs`**
- ✅ Removed `IHubContext<CashPaymentHub>` dependency
- ✅ Removed all `SignalR` broadcast calls (`SendAsync`)
- ✅ Kept `/api/cash-payment/status/{orderNumber}` endpoint (already existed)
- ✅ Added comments explaining polling architecture

**File: `Program.cs`**
- ✅ Removed `CashPaymentHub` mapping
- ✅ Added comment explaining the change

**File: `Hubs/CashPaymentHub.cs`**
- ✅ Deleted (no longer needed)

### Frontend Changes

**File: `Components/Pages/CashPayment.razor`**
- ✅ Removed `Microsoft.AspNetCore.SignalR.Client` using statement
- ✅ Added `System.Timers` using statement
- ✅ Removed `HubConnection` field
- ✅ Added `System.Timers.Timer` field for polling
- ✅ Removed `InitializeSignalR()` method
- ✅ Added `StartPolling()` method - polls every 1 second
- ✅ Added `PollPaymentStatus()` method - calls status API
- ✅ Added `StopPolling()` method - stops timer on completion/cancellation
- ✅ Updated `DisposeAsync()` to stop polling timer
- ✅ Added `PaymentStatusResponse` DTO class

### Python Script

**File: `arduino_cash_reader.py`**
- ✅ **No changes needed** - already just POSTs to VPS endpoint

## How It Works

### 1. Payment Initialization
```csharp
// Browser calls on page load
POST /api/cash-payment/init
{
  "orderNumber": "ORD-123",
  "totalAmount": 500
}
```

### 2. Start Polling
```csharp
// Browser starts polling every 1 second
GET /api/cash-payment/status/ORD-123

Response:
{
  "success": true,
  "orderNumber": "ORD-123",
  "amountInserted": 0,
  "totalRequired": 500,
  "remainingAmount": 500,
  "change": 0,
  "status": "Active"
}
```

### 3. Customer Inserts Cash
```python
# Python script on Raspberry Pi
POST /api/cash-payment/update
{
  "orderNumber": "ORD-123",
  "amountAdded": 100
}
```

### 4. Browser Detects Update
```csharp
// Next polling cycle (within 1 second)
GET /api/cash-payment/status/ORD-123

Response:
{
  "amountInserted": 100,  // Updated!
  "remainingAmount": 400,
  "status": "Active"
}
```

### 5. Payment Completion
```csharp
// When amountInserted >= totalRequired
GET /api/cash-payment/status/ORD-123

Response:
{
  "amountInserted": 500,
  "remainingAmount": 0,
  "change": 0,
  "status": "Completed"  // Changed!
}

// Browser stops polling when status is "Completed" or "Cancelled"
```

## Testing

### 1. Start the Application
```bash
cd RestaurantKiosk
dotnet run
```

### 2. Navigate to Cash Payment Page
```
http://localhost:5000/cash-payment?orderNumber=TEST-001&totalAmount=500
```

### 3. Simulate Cash Insertion (Without Arduino)
```bash
# Use the test endpoint to simulate cash insertion
curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d '{"orderNumber": "TEST-001", "amount": 100}'
```

### 4. Verify Polling
- Open browser DevTools → Network tab
- Filter by "status"
- You should see GET requests to `/api/cash-payment/status/TEST-001` every 1 second
- After simulating cash insertion, the response should show updated `amountInserted`
- UI should update within 1 second

### 5. Test with Real Arduino
```bash
# On Raspberry Pi
cd /path/to/project
python3 arduino_cash_reader.py

# Insert cash into bill/coin acceptor
# Arduino → Python → POST to VPS → Browser polls and updates UI
```

## Polling Configuration

Current settings:
- **Polling Interval:** 1 second (1000ms)
- **Concurrent Requests:** Prevented with `_isPolling` flag
- **Stop Conditions:** Status is "Completed" or "Cancelled"

To change polling interval:
```csharp
// File: CashPayment.razor, line 273
_pollingTimer = new System.Timers.Timer(1000); // Change to desired ms
```

## Performance Comparison

| Metric | SignalR | Polling |
|--------|---------|---------|
| Update Latency | 10-100ms | 500-1000ms (avg) |
| Connection Overhead | WebSocket handshake | None |
| Bandwidth per minute | ~1-2 KB | ~12-24 KB |
| Reliability | Connection drops possible | Very reliable |
| Debugging | Complex | Simple HTTP requests |
| Proxy/Firewall Issues | Common | Rare |

**Conclusion:** 1-second latency is acceptable for cash payment UI. Polling is simpler and more reliable.

## Troubleshooting

### Issue: UI Not Updating
**Check:**
1. Browser console for JavaScript errors
2. Network tab - are GET requests to `/api/cash-payment/status/...` happening?
3. Response from status endpoint - is `amountInserted` changing?

**Solution:**
- Check polling timer is running (`StartPolling()` called)
- Check `_isPolling` flag isn't stuck
- Check payment session exists in `_activeSessions`

### Issue: Polling Continues After Completion
**Check:**
- `StopPolling()` is called when status changes to "Completed" or "Cancelled"
- Timer is disposed properly

**Solution:**
```csharp
// Ensure StopPolling() is called
if (PaymentStatus == CashPaymentStatus.Completed)
{
    StopPolling();
}
```

### Issue: Multiple Concurrent Requests
**Check:**
- `_isPolling` flag prevents concurrent calls

**Solution:**
```csharp
if (_isPolling) return; // Already polling
_isPolling = true;
// ... make request ...
_isPolling = false;
```

## Migration Notes

### For Existing Deployments

1. **No database changes required** - `_activeSessions` is in-memory (same as before)
2. **Python script unchanged** - still POSTs to same endpoint
3. **Arduino unchanged** - no changes needed
4. **Backend compatible** - `/api/cash-payment/status` endpoint already existed

### Rollback Plan

If you need to rollback to SignalR:

1. Restore `CashPaymentHub.cs` from git history
2. Add back to `Program.cs`: `app.MapHub<CashPaymentHub>("/cashpaymenthub");`
3. Restore `CashPaymentController.cs` SignalR broadcasts
4. Restore `CashPayment.razor` SignalR connection code

## Future Enhancements

### Option 1: Adjust Polling Interval Based on Status
```csharp
// Poll faster when payment is active
if (PaymentStatus == CashPaymentStatus.Active)
    _pollingTimer.Interval = 500;  // 0.5 seconds
else
    _pollingTimer.Interval = 2000; // 2 seconds
```

### Option 2: Use Server-Sent Events (SSE)
- Lighter than WebSocket
- Unidirectional (server → client)
- Simpler than SignalR
- Browser support is excellent

### Option 3: Long Polling
- Request waits on server until update available
- Reduces network traffic
- More complex server-side implementation

## Summary

✅ **Architecture Change:** SignalR → HTTP Polling  
✅ **Polling Interval:** 1 second  
✅ **Latency:** 0.5-1 second (acceptable for cash payments)  
✅ **Reliability:** Improved (no WebSocket connection issues)  
✅ **Simplicity:** Much simpler codebase  
✅ **Backward Compatible:** Python script unchanged  
✅ **Production Ready:** Yes!

---

**Version:** 2.0  
**Date:** 2025-01-15  
**Architecture:** VPS + Raspberry Pi + Polling  
**Status:** ✅ Implemented and Tested

