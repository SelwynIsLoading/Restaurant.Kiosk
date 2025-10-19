# Receipt Printing Implementation Summary

## Overview

A complete receipt printing system has been implemented for the Restaurant Kiosk application, enabling automatic receipt printing via a thermal printer connected to a Raspberry Pi.

## Architecture

```
┌─────────────────────────────────────────┐
│          VPS Server                      │
│  ┌────────────────────────────────────┐ │
│  │  .NET Application (Port 5000)      │ │
│  │  ┌──────────────────────────────┐  │ │
│  │  │  Controllers:                │  │ │
│  │  │  - ReceiptController         │  │ │
│  │  │  - PaymentController         │  │ │
│  │  │  - CashPaymentController     │  │ │
│  │  └──────────────────────────────┘  │ │
│  │  ┌──────────────────────────────┐  │ │
│  │  │  Services:                   │  │ │
│  │  │  - ReceiptService            │  │ │
│  │  │  - IReceiptService           │  │ │
│  │  └──────────────────────────────┘  │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
              │ HTTP Request
              │ (Receipt Data JSON)
              ▼
┌─────────────────────────────────────────┐
│       Raspberry Pi                       │
│  ┌────────────────────────────────────┐ │
│  │  Python Service (Port 5001)        │ │
│  │  ┌──────────────────────────────┐  │ │
│  │  │  receipt_printer.py          │  │ │
│  │  │  - Flask REST API            │  │ │
│  │  │  - ESC/POS Printer Driver    │  │ │
│  │  └──────────────────────────────┘  │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
              │ USB/Network/Serial
              ▼
┌─────────────────────────────────────────┐
│      Thermal Receipt Printer             │
│      (ESC/POS Compatible)                │
└─────────────────────────────────────────┘
```

## Components Implemented

### 1. Python Receipt Printer Service (`receipt_printer.py`)

**Location**: `RestaurantKiosk/receipt_printer.py`

**Purpose**: Interfaces with thermal printer hardware and provides REST API for printing

**Features**:
- Flask REST API server (Port 5001)
- ESC/POS protocol support
- Multiple printer connection types:
  - USB (most common)
  - Network (IP-based)
  - Serial (RS-232)
  - File (testing without hardware)
- Receipt formatting and printing
- Health check and status endpoints
- Test printing functionality

**Key Endpoints**:
- `GET /health` - Service health check
- `POST /api/receipt/print` - Print receipt from JSON data
- `POST /api/receipt/test` - Print test receipt
- `GET /api/receipt/status` - Get printer connection status

### 2. .NET Receipt Service

**Files**:
- `RestaurantKiosk/Data/Services/IReceiptService.cs` - Interface definition
- `RestaurantKiosk/Data/Services/ReceiptService.cs` - Implementation

**Purpose**: Generate receipt data and communicate with Python printer service

**Key Methods**:
- `GenerateReceiptDataAsync()` - Convert Order entity to receipt format
- `PrintReceiptAsync()` - Send receipt data to printer service
- `PrintOrderReceiptAsync()` - Generate and print in one operation
- `TestPrinterAsync()` - Test printer connectivity

**Features**:
- Automatic receipt data formatting
- Configurable restaurant details
- HTTP client for printer communication
- Error handling and logging
- Support for cash payment details (amount paid, change)

### 3. Receipt Controller API

**File**: `RestaurantKiosk/Controllers/ReceiptController.cs`

**Purpose**: Provide HTTP endpoints for receipt printing operations

**Endpoints**:

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/receipt/print/{orderNumber}` | Print receipt by order number |
| POST | `/api/receipt/print/id/{orderId}` | Print receipt by order ID |
| POST | `/api/receipt/reprint/{orderNumber}` | Reprint existing receipt |
| POST | `/api/receipt/test` | Test printer connection |
| GET | `/api/receipt/preview/{orderNumber}` | Preview receipt data (no printing) |
| GET | `/api/receipt/status` | Get printer status |

### 4. Payment Integration

**Modified Files**:
- `RestaurantKiosk/Controllers/CashPaymentController.cs`
- `RestaurantKiosk/Controllers/PaymentController.cs`
- `RestaurantKiosk/Program.cs`

**Integration Points**:

1. **Cash Payment Completion**
   - Triggers when sufficient cash is inserted
   - Prints receipt with amount paid and change
   - Located in: `CashPaymentController.CompletePayment()`

2. **Invoice Payment (Webhook)**
   - Triggers on invoice payment success
   - Located in: `PaymentController.HandleInvoicePaid()`

3. **E-Wallet Payment (Webhook)**
   - Triggers on GCash/Maya payment success
   - Located in: `PaymentController.HandleEWalletChargeSucceeded()`

All integrations use asynchronous printing to avoid blocking the main payment flow.

### 5. Configuration

**Files Modified**:
- `RestaurantKiosk/appsettings.json`
- `RestaurantKiosk/appsettings.Development.json`

**Configuration Structure**:
```json
{
  "Receipt": {
    "PrinterApiUrl": "http://localhost:5001",
    "RestaurantName": "Restaurant Name",
    "RestaurantAddress": "123 Main Street, City, Country",
    "RestaurantPhone": "+63 XXX XXX XXXX",
    "RestaurantEmail": "info@restaurant.com"
  }
}
```

### 6. Service Registration

**File**: `RestaurantKiosk/Program.cs`

Added:
```csharp
builder.Services.AddScoped<IReceiptService, ReceiptService>();
```

### 7. Documentation

**Files Created**:
- `RECEIPT_PRINTER_SETUP.md` - Comprehensive setup guide
- `RECEIPT_PRINTER_QUICKSTART.md` - Quick start guide
- `RECEIPT_PRINTING_IMPLEMENTATION.md` - This file
- `requirements.txt` - Python dependencies
- `deployment/receipt-printer.service` - Systemd service file

## Receipt Format

The printed receipt includes:

1. **Header**
   - Restaurant name (large, bold)
   - Address, phone, email

2. **Order Details**
   - Order number (bold)
   - Date and time
   - Customer name (if available)

3. **Items List**
   - Product name, quantity, line total
   - Special notes (if any)

4. **Totals Section**
   - Subtotal
   - VAT (12%)
   - Service charge (if applicable)
   - Total amount (large, bold)

5. **Payment Information**
   - Payment method
   - Amount paid (for cash)
   - Change given (for cash)

6. **Footer**
   - Thank you message
   - Status indicator
   - QR code (optional - order number)

## Automatic Printing Triggers

Receipts are automatically printed when:

1. **Cash payment is completed**
   - Includes amount paid and change
   - Triggered in `CashPaymentController.CompletePayment()`

2. **Online payment succeeds (Invoice)**
   - Triggered via webhook in `PaymentController.HandleInvoicePaid()`

3. **E-wallet payment succeeds (GCash/Maya)**
   - Triggered via webhook in `PaymentController.HandleEWalletChargeSucceeded()`

All automatic printing is done asynchronously to ensure payment processing is not blocked if the printer fails.

## Error Handling

The system includes comprehensive error handling:

1. **Network Errors**
   - HTTP timeouts (10 seconds)
   - Connection failures
   - Logged but doesn't block order completion

2. **Printer Errors**
   - Paper out
   - Printer offline
   - USB disconnection
   - All logged for troubleshooting

3. **Data Errors**
   - Missing order data
   - Invalid receipt data
   - Validation and logging

## Deployment

### Development Environment
1. Run Python service on local machine/Raspberry Pi:
   ```bash
   python3 receipt_printer.py
   ```

2. .NET application connects to `http://localhost:5001`

### Production Environment (VPS + Raspberry Pi)
1. Python service runs on Raspberry Pi as systemd service
2. .NET application on VPS connects to Raspberry Pi IP
3. Configuration: `"PrinterApiUrl": "http://192.168.1.100:5001"`

### Systemd Service Setup
```bash
# Copy service file
sudo cp deployment/receipt-printer.service /etc/systemd/system/

# Enable and start
sudo systemctl enable receipt-printer
sudo systemctl start receipt-printer
```

## Testing

### Test Printer Connection
```bash
# From Raspberry Pi
curl -X POST http://localhost:5001/api/receipt/test

# From VPS
curl -X POST http://raspberry-pi-ip:5001/api/receipt/test
```

### Test via .NET API
```bash
# Test printer
curl -X POST http://your-vps:5000/api/receipt/test

# Print specific order
curl -X POST http://your-vps:5000/api/receipt/print/ORD-20250115-001

# Reprint
curl -X POST http://your-vps:5000/api/receipt/reprint/ORD-20250115-001
```

### Integration Testing
1. Create an order in the kiosk
2. Complete payment (cash or online)
3. Verify receipt prints automatically
4. Check logs for any errors

## Monitoring

### Python Service Logs
```bash
# Direct run
tail -f receipt_printer.log

# Systemd service
sudo journalctl -u receipt-printer -f
```

### .NET Application Logs
```bash
# Check for receipt-related logs
grep -i "receipt" /path/to/app/logs/*.log
```

### Check Service Status
```bash
# Python service
sudo systemctl status receipt-printer

# Check if running
ps aux | grep receipt_printer

# Check port
sudo netstat -tuln | grep 5001
```

## Maintenance

### Update Printer Configuration
1. Edit `receipt_printer.py`
2. Update printer type, IDs, or connection details
3. Restart service:
   ```bash
   sudo systemctl restart receipt-printer
   ```

### Update Restaurant Information
1. Edit `appsettings.json` Receipt section
2. Restart .NET application

### Paper Replacement
1. Receipt printing will fail gracefully
2. Errors logged
3. Orders still complete successfully
4. Can reprint after paper is replaced

## Troubleshooting Quick Reference

| Issue | Solution |
|-------|----------|
| Printer not found | Check USB connection, verify IDs in config |
| Permission denied | Set up udev rules, check file permissions |
| Connection refused | Verify service is running, check firewall |
| Receipt not printing | Check printer status, verify paper, check logs |
| Network timeout | Verify Raspberry Pi IP, check network connectivity |
| Service won't start | Check Python dependencies, verify script path |

## Performance

- **Async Printing**: Non-blocking, doesn't delay order completion
- **Timeout**: 10-second HTTP timeout
- **Print Time**: ~2-3 seconds per receipt (depends on printer)
- **Resource Usage**: Minimal CPU/memory on Raspberry Pi

## Security Considerations

1. **Network Security**
   - Python service accessible on local network
   - Consider firewall rules
   - Use VPN or SSH tunnel for remote access

2. **Authentication**
   - Currently no authentication on Python service
   - Add API key if exposed to internet
   - Restrict access by IP if possible

3. **Data Privacy**
   - Receipt data contains customer information
   - Transmitted over HTTP (not HTTPS by default)
   - Consider encryption for production

## Future Enhancements

Potential improvements:
- [ ] Add receipt template customization
- [ ] Support multiple receipt formats
- [ ] Add receipt preview on screen before printing
- [ ] Implement print queue for high volume
- [ ] Add printer status monitoring dashboard
- [ ] Support for logo/image printing
- [ ] Email receipt as backup
- [ ] SMS receipt notification
- [ ] Cloud print support
- [ ] Multi-language receipts

## Dependencies

### Python (Raspberry Pi)
- `python-escpos==3.0` - ESC/POS printer driver
- `Flask==3.0.0` - REST API server
- `requests==2.31.0` - HTTP client

### .NET (VPS)
- `Microsoft.AspNetCore` (built-in)
- `System.Net.Http` (built-in)

## Files Modified/Created

### Created Files:
1. `RestaurantKiosk/receipt_printer.py`
2. `RestaurantKiosk/Data/Services/IReceiptService.cs`
3. `RestaurantKiosk/Data/Services/ReceiptService.cs`
4. `RestaurantKiosk/Controllers/ReceiptController.cs`
5. `RestaurantKiosk/RECEIPT_PRINTER_SETUP.md`
6. `RestaurantKiosk/RECEIPT_PRINTER_QUICKSTART.md`
7. `RestaurantKiosk/RECEIPT_PRINTING_IMPLEMENTATION.md`
8. `RestaurantKiosk/requirements.txt`
9. `RestaurantKiosk/deployment/receipt-printer.service`

### Modified Files:
1. `RestaurantKiosk/Program.cs` - Added service registration
2. `RestaurantKiosk/Controllers/CashPaymentController.cs` - Added receipt printing
3. `RestaurantKiosk/Controllers/PaymentController.cs` - Added receipt printing
4. `RestaurantKiosk/appsettings.json` - Added receipt configuration
5. `RestaurantKiosk/appsettings.Development.json` - Added receipt configuration

## Conclusion

The receipt printing system is fully integrated and ready for deployment. It provides:
- ✅ Automatic receipt printing on payment completion
- ✅ Manual reprint capability
- ✅ Robust error handling
- ✅ Comprehensive logging
- ✅ Easy configuration
- ✅ Production-ready systemd service
- ✅ Complete documentation

The system is designed to be reliable, maintainable, and easy to troubleshoot, with receipt printing failures not affecting order completion.

