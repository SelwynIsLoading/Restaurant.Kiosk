# Cash Payment System Setup Guide

## 🚀 Quick Links

| Scenario | Guide |
|----------|-------|
| **Quick Setup (Any scenario)** | [CASH_PAYMENT_QUICKSTART.md](CASH_PAYMENT_QUICKSTART.md) ⚡ |
| **VPS + Home Raspberry Pi** | [CASH_PAYMENT_VPS_DEPLOYMENT.md](CASH_PAYMENT_VPS_DEPLOYMENT.md) ☁️ |
| **Detailed Setup (This file)** | Continue reading below 📖 |
| **Testing Procedures** | [CASH_PAYMENT_TESTING.md](CASH_PAYMENT_TESTING.md) 🧪 |

## Overview

The cash payment system integrates with Arduino-based bill and coin acceptors via a Python API to provide real-time cash payment processing for the restaurant kiosk.

### ⚠️ Important: VPS Deployment with Dynamic Home IP

If your **ASP.NET Core app is on a cloud VPS** and your **Raspberry Pi with Arduino is on home internet (dynamic IP)**, this setup works perfectly! See [CASH_PAYMENT_VPS_DEPLOYMENT.md](CASH_PAYMENT_VPS_DEPLOYMENT.md) for detailed instructions.

**Why it works:** The Python script makes OUTGOING connections to the VPS (not incoming), so your dynamic home IP is not a problem. ✅

## Architecture

```
Arduino (Bill/Coin Acceptors)
        ↓
Python API (Raspberry Pi)
        ↓
ASP.NET Core API (SignalR)
        ↓
Blazor UI (Real-time Updates)
```

## Components

### 1. Hardware Setup
- **Arduino**: Connected to bill and coin acceptors
- **Bill Acceptor**: Reads paper currency
- **Coin Acceptor**: Reads coins
- **Raspberry Pi**: Hosts both the Python API and Blazor application

### 2. Software Components

#### Backend (ASP.NET Core)
- **CashPaymentHub** (`/cashpaymenthub`): SignalR hub for real-time communication
- **CashPaymentController** (`/api/cash-payment`): REST API endpoints for payment processing

#### Frontend (Blazor)
- **CashPayment.razor**: Real-time cash payment UI

## API Endpoints

### Initialize Payment Session
```http
POST /api/cash-payment/init
Content-Type: application/json

{
  "orderNumber": "ORD-20250114-ABC123",
  "totalAmount": 450.00
}

Response:
{
  "success": true,
  "orderNumber": "ORD-20250114-ABC123",
  "totalRequired": 450.00,
  "sessionId": "ORD-20250114-ABC123"
}
```

### Update Cash Amount (Called by Python API)
```http
POST /api/cash-payment/update
Content-Type: application/json

{
  "orderNumber": "ORD-20250114-ABC123",
  "amountAdded": 100.00
}

Response:
{
  "success": true,
  "orderNumber": "ORD-20250114-ABC123",
  "amountInserted": 100.00,
  "totalRequired": 450.00,
  "remainingAmount": 350.00,
  "isComplete": false
}
```

When payment is complete (amountInserted >= totalRequired), the system will:
1. Update order status to "Paid"
2. Decrease product quantities
3. Notify kitchen staff via SignalR
4. Broadcast payment completion to the UI
5. Calculate and display change (if overpayment)

### Get Payment Status
```http
GET /api/cash-payment/status/{orderNumber}

Response:
{
  "success": true,
  "orderNumber": "ORD-20250114-ABC123",
  "amountInserted": 450.00,
  "totalRequired": 450.00,
  "remainingAmount": 0.00,
  "change": 0.00,
  "status": "Completed",
  "startedAt": "2025-01-14T10:30:00Z",
  "completedAt": "2025-01-14T10:31:25Z"
}
```

### Cancel Payment
```http
POST /api/cash-payment/cancel/{orderNumber}

Response:
{
  "success": true,
  "orderNumber": "ORD-20250114-ABC123",
  "amountReturned": 200.00,
  "message": "Payment cancelled. Returning ₱200.00"
}
```

### Test Endpoint (Development Only)
```http
POST /api/cash-payment/test/simulate
Content-Type: application/json

{
  "orderNumber": "ORD-20250114-ABC123",
  "amount": 100.00
}
```
This endpoint simulates cash insertion for testing without hardware.

## SignalR Events

The system broadcasts real-time events via SignalR:

### CashAmountUpdated
Triggered when cash is inserted.
```javascript
connection.on("CashAmountUpdated", (orderNumber, amountInserted, totalRequired) => {
    console.log(`Order ${orderNumber}: ₱${amountInserted} / ₱${totalRequired}`);
});
```

### PaymentCompleted
Triggered when sufficient cash is received.
```javascript
connection.on("PaymentCompleted", (orderNumber, amountPaid, change) => {
    console.log(`Payment complete! Change: ₱${change}`);
});
```

### PaymentCancelled
Triggered when payment is cancelled.
```javascript
connection.on("PaymentCancelled", (orderNumber, amountReturned) => {
    console.log(`Payment cancelled. Returning: ₱${amountReturned}`);
});
```

## Python API Integration

See `python-api/arduino_cash_reader.py` for a complete example of integrating with Arduino hardware.

### Key Requirements:
1. Read pulse/serial data from Arduino
2. Convert pulses to currency values
3. POST updates to `/api/cash-payment/update`
4. Handle errors and reconnection

## User Flow

1. Customer completes order and selects "Cash" payment method
2. System creates order in database with "Pending" status
3. User is redirected to `/cash-payment` page
4. Payment session is initialized
5. UI displays:
   - Total amount due
   - Amount inserted (updates in real-time)
   - Remaining balance
   - Progress bar
6. Customer inserts bills/coins into acceptors
7. Arduino detects currency and sends signal to Python API
8. Python API calculates amount and calls `/api/cash-payment/update`
9. SignalR broadcasts update to UI
10. UI updates display in real-time
11. When sufficient cash is received:
    - Order status updated to "Paid"
    - Product quantities decreased
    - Kitchen notified
    - Change calculated and displayed
    - Customer can collect change (if any)
12. Customer returns to menu

## Configuration

### Arduino Configuration
```cpp
// Bill denominations (PHP)
const int billValues[] = {20, 50, 100, 200, 500, 1000};

// Coin denominations (PHP)
const float coinValues[] = {0.25, 1.00, 5.00, 10.00, 20.00};
```

### Python API Configuration
```python
# API Configuration
KIOSK_API_URL = "http://localhost:5000"  # Update for production
ARDUINO_PORT = "/dev/ttyUSB0"  # Or COM port on Windows
BAUD_RATE = 9600
```

### ASP.NET Core Configuration
No additional configuration needed. The hub is automatically registered in `Program.cs`.

## Testing

### Without Hardware (Development)
Use the simulate endpoint to test the UI:

```bash
# Start a payment session
curl -X POST http://localhost:5000/api/cash-payment/init \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","totalAmount":450.00}'

# Simulate inserting ₱100
curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","amount":100.00}'

# Simulate inserting another ₱100
curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","amount":100.00}'

# Continue until total is reached
```

### With Hardware
1. Connect Arduino to Raspberry Pi via USB
2. Update the Python script with correct port and baud rate
3. Run the Python API: `python3 arduino_cash_reader.py`
4. Test with actual bills and coins

## Security Considerations

### For Production:
1. **API Authentication**: Add authentication to cash payment endpoints
   ```csharp
   [Authorize] // Add to controller
   ```

2. **API Key**: Require API key from Python API
   ```csharp
   var apiKey = Request.Headers["X-API-Key"].FirstOrDefault();
   if (apiKey != _configuration["CashPayment:ApiKey"])
       return Unauthorized();
   ```

3. **Rate Limiting**: Prevent abuse
   ```csharp
   services.AddRateLimiter(/* configuration */);
   ```

4. **HTTPS**: Always use HTTPS in production
5. **IP Whitelisting**: Restrict API access to Raspberry Pi IP

## Troubleshooting

### Issue: SignalR connection fails
**Solution**: Check that the hub is registered in `Program.cs` and the URL is correct (`/cashpaymenthub`)

### Issue: Python API can't reach ASP.NET Core
**Solution**: 
- Ensure firewall allows connections
- Check that ASP.NET Core is bound to correct network interface
- Update `launchSettings.json` to bind to `0.0.0.0` instead of `localhost`

### Issue: Arduino not detected
**Solution**:
- Check USB connection
- Verify correct COM port/device in Python script
- Install Arduino drivers if needed
- Check permissions: `sudo usermod -a -G dialout $USER`

### Issue: Payment not completing automatically
**Solution**:
- Check that `amountInserted >= totalRequired`
- Verify no errors in server logs
- Ensure SignalR connection is active
- Check database for order status

## Deployment to Raspberry Pi

1. **Install .NET Runtime**:
   ```bash
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 9.0 --runtime aspnetcore
   ```

2. **Configure Service**:
   ```bash
   sudo nano /etc/systemd/system/kiosk.service
   ```
   ```ini
   [Unit]
   Description=Restaurant Kiosk
   
   [Service]
   WorkingDirectory=/home/pi/kiosk
   ExecStart=/home/pi/.dotnet/dotnet RestaurantKiosk.dll
   Restart=always
   RestartSec=10
   
   [Install]
   WantedBy=multi-user.target
   ```

3. **Enable and Start**:
   ```bash
   sudo systemctl enable kiosk
   sudo systemctl start kiosk
   ```

4. **Setup Python API**:
   ```bash
   sudo nano /etc/systemd/system/cash-reader.service
   ```
   ```ini
   [Unit]
   Description=Cash Reader API
   After=kiosk.service
   
   [Service]
   WorkingDirectory=/home/pi/kiosk/python-api
   ExecStart=/usr/bin/python3 arduino_cash_reader.py
   Restart=always
   RestartSec=10
   
   [Install]
   WantedBy=multi-user.target
   ```

5. **Enable and Start**:
   ```bash
   sudo systemctl enable cash-reader
   sudo systemctl start cash-reader
   ```

## Monitoring

Check service status:
```bash
sudo systemctl status kiosk
sudo systemctl status cash-reader
```

View logs:
```bash
sudo journalctl -u kiosk -f
sudo journalctl -u cash-reader -f
```

## Support

For issues or questions, refer to the main documentation or contact your system administrator.

