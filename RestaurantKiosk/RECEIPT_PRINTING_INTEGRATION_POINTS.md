# Receipt Printing Integration Points

This document lists all the places where receipt printing is triggered after successful payment.

## ✅ Payment Completion Points (All Integrated)

### 1. Cash Payment Completion
**File**: `Controllers/CashPaymentController.cs`
**Method**: `CompletePayment(string orderNumber)`
**Line**: ~291-302

```csharp
// Print receipt
_ = Task.Run(async () =>
{
    try
    {
        await _receiptService.PrintOrderReceiptAsync(order, amountPaid, change);
        _logger.LogInformation("Receipt printed for cash payment order: {OrderNumber}", orderNumber);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to print receipt for order: {OrderNumber}", orderNumber);
    }
});
```

**Triggered When**: 
- Customer inserts sufficient cash into bill/coin acceptor
- Arduino sends cash updates via Python script
- Total amount inserted >= order total

**Receipt Includes**:
- Amount paid (cash inserted)
- Change to return

---

### 2. Invoice Payment (Xendit) - Webhook
**File**: `Controllers/PaymentController.cs`
**Method**: `HandleInvoicePaid(JsonElement webhookData)`
**Line**: ~294-306

```csharp
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
```

**Triggered When**:
- Xendit sends `invoice.paid` webhook event
- Customer completes payment via invoice link

**Receipt Includes**:
- Standard receipt without cash details

---

### 3. E-Wallet Payment (GCash/Maya) - Webhook
**File**: `Controllers/PaymentController.cs`
**Method**: `HandleEWalletChargeSucceeded(JsonElement webhookData)`
**Line**: ~351-363

```csharp
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
```

**Triggered When**:
- Xendit sends `ewallet.charge.succeeded` webhook event
- Customer completes GCash or Maya payment

**Receipt Includes**:
- Standard receipt without cash details

---

### 4. Payment Callback - GET (Browser Redirect)
**File**: `Controllers/CallbackController.cs`
**Method**: `PaymentCallback([FromQuery] string external_id)`
**Line**: ~100-117

```csharp
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
```

**Triggered When**:
- Payment gateway redirects customer back to callback URL
- Customer completes GCash/Maya payment and is redirected
- This happens BEFORE webhook in most cases

**Receipt Includes**:
- Standard receipt without cash details

---

### 5. Payment Callback - POST (Webhook Alternative)
**File**: `Controllers/CallbackController.cs`
**Method**: `PaymentCallbackPost()`
**Line**: ~193-210

```csharp
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
```

**Triggered When**:
- Payment gateway sends POST webhook to callback URL
- Alternative to dedicated webhook endpoint

**Receipt Includes**:
- Standard receipt without cash details

---

## Receipt Printing Flow

### Polling Mode (Recommended - Default)

```
┌────────────────────────────────────────────────────────────┐
│ 1. Payment Completed (any method above)                    │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 2. ReceiptService.PrintOrderReceiptAsync()                 │
│    - Generates receipt data                                │
│    - If UsePollingMode=true, queues the job                │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 3. PrintQueueService.QueuePrintJobAsync()                  │
│    - Creates job ID: PRINT-20250115-xxx                    │
│    - Adds to in-memory queue                               │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 4. Raspberry Pi Polls (every 2 seconds)                    │
│    GET /api/receipt/queue/next                             │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 5. VPS Returns Print Job                                   │
│    - Job ID + Receipt Data (JSON)                          │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 6. Raspberry Pi Prints Receipt                             │
│    - Formats via ESC/POS commands                          │
│    - Sends to thermal printer                              │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 7. Raspberry Pi Confirms                                   │
│    POST /api/receipt/queue/complete/{jobId}                │
└────────────────────────────────────────────────────────────┘
```

### Direct HTTP Mode

```
┌────────────────────────────────────────────────────────────┐
│ 1. Payment Completed                                        │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 2. ReceiptService.PrintOrderReceiptAsync()                 │
│    - Generates receipt data                                │
│    - If UsePollingMode=false, sends HTTP request           │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 3. HTTP POST to Raspberry Pi                               │
│    POST http://raspberry-pi-ip:5001/api/receipt/print      │
└────────────────┬───────────────────────────────────────────┘
                 │
                 ▼
┌────────────────────────────────────────────────────────────┐
│ 4. Raspberry Pi Prints Receipt                             │
│    (receipt_printer.py Flask service)                      │
└────────────────────────────────────────────────────────────┘
```

---

## Common Characteristics

All integration points follow these patterns:

### 1. Async Execution
```csharp
_ = Task.Run(async () => { ... });
```
- Receipts print in background
- Order completion is not blocked
- Payment success doesn't depend on printer

### 2. Error Handling
```csharp
try {
    await _receiptService.PrintOrderReceiptAsync(order);
    _logger.LogInformation("Receipt printed...");
}
catch (Exception ex) {
    _logger.LogError(ex, "Failed to print receipt...");
}
```
- Errors are logged but don't fail the payment
- Payment always completes even if printer fails

### 3. Order Reloading
```csharp
var orderForReceipt = await _orderRepository.GetOrderByExternalIdAsync(externalId);
```
- Ensures we have latest order data
- Includes all order items for receipt

---

## Testing Each Integration Point

### Test Cash Payment
```bash
# 1. Create order with cash payment method
# 2. Simulate cash insertion via test endpoint
curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d '{"orderNumber": "ORD-TEST-001", "amount": 100}'

# 3. Check logs for "Receipt printed for cash payment order"
```

### Test Invoice Payment
```bash
# 1. Create order and generate invoice
# 2. Pay via invoice link
# 3. Xendit sends webhook
# 4. Check logs for "Receipt printed for invoice payment order"
```

### Test E-Wallet Payment
```bash
# 1. Create order and initiate GCash/Maya
# 2. Complete payment in mobile app
# 3. Xendit sends webhook
# 4. Check logs for "Receipt printed for e-wallet payment order"
```

### Test Callback (GET)
```bash
# 1. Complete GCash/Maya payment
# 2. Browser redirects to /api/callback/payment/callback?external_id=xxx
# 3. Check logs for "Receipt printed for callback payment order"
```

### Test Callback (POST)
```bash
# Simulate webhook POST
curl -X POST http://localhost:5000/api/callback/payment/callback \
  -H "Content-Type: application/json" \
  -d '{"data": {"external_id": "order_xxx"}}'
```

---

## Duplicate Prevention

### Why Multiple Integration Points?

Different payment methods use different callback mechanisms:

1. **Cash**: Only uses `CashPaymentController`
2. **Invoice**: Uses webhook (`PaymentController`)
3. **GCash/Maya**: Uses BOTH callback redirect AND webhook

### Duplicate Receipt Prevention

Built-in checks prevent duplicate printing:

```csharp
if (order.Status != OrderStatus.Paid)
{
    // Only process if not already paid
    // This prevents duplicate receipts
}
```

The first successful callback wins:
- If callback redirect comes first → Prints receipt
- Webhook arrives later → Order already marked Paid → Skips

---

## Monitoring Receipt Printing

### Check VPS Logs
```bash
# Search for receipt printing
grep -i "receipt printed" /path/to/app/logs/*.log

# Search for failures
grep -i "failed to print receipt" /path/to/app/logs/*.log

# Check queue activity
grep -i "queued print job" /path/to/app/logs/*.log
```

### Check Raspberry Pi Logs
```bash
# Client logs
tail -f receipt_printer_client.log

# Service logs
sudo journalctl -u receipt-printer-client -f

# Look for
# - "Received print job"
# - "Printing receipt for order"
# - "Receipt printed successfully"
```

### Check Print Queue
```bash
# Get next job (should return 204 if empty)
curl http://your-vps.com/api/receipt/queue/next

# Check specific job status
curl http://your-vps.com/api/receipt/queue/status/PRINT-20250115-xxx
```

---

## Troubleshooting

### Receipts Not Printing

**Check if jobs are being queued:**
```bash
# VPS logs should show:
grep "Queued print job" /path/to/logs
```

**Check if Raspberry Pi is polling:**
```bash
# Pi logs should show every 2 seconds:
grep "Checking for print jobs" receipt_printer_client.log
```

**Check for printer errors:**
```bash
# Pi logs will show:
grep -i "error" receipt_printer_client.log
```

### Duplicate Receipts

If you see duplicate receipts:

1. Check if `order.Status` is being checked before processing
2. Add additional logging to see which callback arrives first
3. Consider adding a distributed lock if using multiple servers

### Missing Receipt for Specific Payment Method

1. Check the specific controller for that payment method
2. Verify `_receiptService` is injected
3. Check logs for errors in that specific flow

---

## Configuration

### Enable/Disable Polling Mode

`appsettings.json`:
```json
{
  "Receipt": {
    "UsePollingMode": true,  // true = queue (polling), false = direct HTTP
    "PrinterApiUrl": "http://localhost:5001"
  }
}
```

### Adjust Polling Interval

`receipt_printer_client.py`:
```python
POLL_INTERVAL = 2  # seconds between polls
```

---

## Summary

✅ **5 Integration Points** - All payment methods covered
✅ **Async Processing** - Non-blocking receipt printing
✅ **Error Handling** - Failures don't affect payments
✅ **Duplicate Prevention** - Status checks prevent double printing
✅ **Polling Support** - Works through NAT/firewalls
✅ **Direct HTTP Support** - For local network deployments
✅ **Comprehensive Logging** - Easy to debug and monitor

Every successful payment now triggers a receipt print job! 🎉

