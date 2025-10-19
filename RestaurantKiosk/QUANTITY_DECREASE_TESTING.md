# Product Quantity Decrease - Testing Guide

## Overview
After a successful payment, product quantities are automatically decreased based on the ordered items.

## What Was Implemented

### 1. **Product Service** (`ProductService.cs`)
- Added `DecreaseProductQuantitiesForOrderAsync()` method
- Safely decreases product quantities for each item in an order
- Prevents negative quantities
- Logs all operations for debugging

### 2. **Payment Controller** (`PaymentController.cs`)
- Webhook handlers now decrease quantities after payment confirmation
- Handles both Invoice and E-wallet (GCash/Maya) payments
- Works when Xendit webhooks are properly configured

### 3. **Payment Success Page** (`PaymentSuccess.razor`)
- **PRIMARY TRIGGER** for quantity decrease in development
- Decreases quantities when user is redirected after payment
- Prevents duplicate processing by checking order status
- Works even without webhook configuration

### 4. **Checkout Page** (`Checkout.razor`)
- Pre-order validation ensures sufficient quantity
- Cash orders decrease quantities immediately
- Prevents overselling before payment

## How It Works

### Digital Payments (GCash, Maya, Card)
```
1. User adds items to cart
2. User fills checkout form
3. System validates product quantities ✓
4. Order created with status: Pending
5. User completes payment on Xendit
6. User redirected to /payment/success
7. PaymentSuccess page:
   - Loads order by ExternalId
   - Checks status is Pending
   - Updates status to Paid
   - ✅ DECREASES PRODUCT QUANTITIES
8. User sees confirmation
```

### Cash Payments
```
1. User adds items to cart
2. User fills checkout form and selects "Cash"
3. System validates product quantities ✓
4. Order created
5. ✅ QUANTITIES DECREASED IMMEDIATELY
6. User directed to pay at counter
```

### Webhook (Production Only)
```
- Xendit sends webhook when payment succeeds
- PaymentController receives webhook
- Updates order status to Paid
- Decreases product quantities
- (Backup mechanism, may not work in development)
```

## Testing Steps

### Test 1: Cash Payment (Easiest to Test)

1. **Check Initial Quantity**
   - Go to Admin Console → Inventory Management
   - Note the quantity of a product (e.g., "Burger: 50")

2. **Make a Test Order**
   - Go to Kiosk page
   - Add the product to cart (quantity: 3)
   - Proceed to Checkout
   - Select payment method: "Cash"
   - Fill in customer details
   - Submit order

3. **Verify Quantity Decreased**
   - Go back to Inventory Management
   - Check the product quantity
   - Expected: Should be 47 (50 - 3)

### Test 2: Digital Payment (Requires Payment)

1. **Check Initial Quantity**
   - Note product quantity in Inventory Management

2. **Make a Test Order**
   - Go to Kiosk
   - Add products to cart
   - Select GCash, Maya, or Card payment
   - Complete payment on Xendit

3. **After Payment Success**
   - You'll be redirected to Payment Success page
   - Check browser console for logs
   - Go to Inventory Management
   - Verify quantities decreased

### Test 3: Validation (Prevents Overselling)

1. **Set Low Quantity**
   - Set a product to quantity: 2

2. **Try to Order More**
   - Add 5 of that product to cart
   - Proceed to checkout
   - Expected: Error message "Insufficient quantity for '{Product}'. Available: 2, Requested: 5"

## Viewing Logs

### Check Application Logs
The application logs all quantity changes. Look for:

```
Decreasing product quantities for order ID: {OrderId}
Successfully decreased product quantities for order ID: {OrderId}
Decreased quantity for product {ProductName} (ID: {ProductId}) by {Quantity}. New quantity: {NewQuantity}
```

### Console Logs (PaymentSuccess page)
Open browser Developer Tools (F12) → Console tab
Look for logged messages about order processing

## Troubleshooting

### Quantities Not Decreasing

1. **Check Order Status**
   - Go to Admin Console → Order Management
   - Find your test order
   - Status should be "Paid" (not "Pending")

2. **Check Application Logs**
   - Look for errors in the console
   - Check if `DecreaseProductQuantitiesForOrderAsync` was called

3. **Verify Database Connection**
   - Ensure PostgreSQL is running
   - Check connection string in appsettings.json

4. **Check Order Items**
   - Ensure order has OrderItems with correct ProductId

### For Cash Orders Not Working

- Check logs in Checkout.razor
- Verify `ProcessCashOrder()` is being called
- Ensure ProductService is injected properly

### For Digital Payments Not Working

- Check PaymentSuccess.razor logs
- Verify external_id is passed in URL
- Confirm order exists with that external_id
- Check order status before and after

## Database Verification

You can verify in PostgreSQL directly:

```sql
-- Check product quantities
SELECT "Id", "Name", "Quantity", "UpdatedAt" 
FROM "Products" 
ORDER BY "UpdatedAt" DESC;

-- Check order items
SELECT o."OrderNumber", o."Status", oi."ProductName", oi."Quantity"
FROM "Orders" o
JOIN "OrderItems" oi ON o."Id" = oi."OrderId"
ORDER BY o."CreatedAt" DESC;

-- Check recent orders
SELECT "OrderNumber", "Status", "PaymentMethod", "TotalAmount", "PaidAt"
FROM "Orders"
ORDER BY "CreatedAt" DESC
LIMIT 10;
```

## Expected Behavior

✅ **Quantities decrease after successful payment**
✅ **Quantities decrease immediately for cash orders**
✅ **Users cannot order more than available quantity**
✅ **Products show "X left" on Kiosk page**
✅ **Products show "Out of Stock" when quantity = 0**
✅ **Admin can see updated quantities in Inventory Management**
✅ **Prevents negative quantities (sets to 0 if insufficient)**

## Known Limitations

1. **Development Environment**: Xendit webhooks won't reach localhost
   - Solution: Quantity decrease happens in PaymentSuccess page instead

2. **Race Conditions**: If both webhook and PaymentSuccess page trigger
   - Solution: Order status check prevents duplicate processing

3. **Manual Refunds**: If payment is refunded, quantities are NOT restored
   - Future: Need to implement refund handling

## Next Steps

After verifying quantities decrease correctly, consider implementing:
- [ ] Automatic low stock alerts
- [ ] Quantity restore on order cancellation/refund
- [ ] Inventory history/audit trail
- [ ] Stock threshold warnings for admin

