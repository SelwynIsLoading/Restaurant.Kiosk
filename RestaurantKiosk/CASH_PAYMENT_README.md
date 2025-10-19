# Cash Payment System - Quick Reference

## 🎯 Overview

A complete real-time cash payment system for your restaurant kiosk that integrates with Arduino-based bill and coin acceptors on Raspberry Pi.

## 📁 Files Structure

```
RestaurantKiosk/
├── Hubs/
│   └── CashPaymentHub.cs              # SignalR hub for real-time updates
├── Controllers/
│   └── CashPaymentController.cs       # REST API for payment processing
├── Components/Pages/
│   ├── CashPayment.razor              # Real-time payment UI
│   └── CashPayment.razor.css          # Styling
├── arduino_cash_reader.py             # Python API for Arduino integration
├── arduino_cash_acceptor.ino          # Arduino firmware
├── requirements.txt                   # Python dependencies
├── CASH_PAYMENT_SETUP.md             # Full setup & deployment guide
├── CASH_PAYMENT_TESTING.md           # Testing guide & troubleshooting
└── ARDUINO_PROTOCOL.md               # Serial protocol documentation
```

## 🚀 Quick Start

### 1. Test Without Hardware (5 minutes)

```bash
# Start the application
cd RestaurantKiosk
dotnet run
```

1. Navigate to `http://localhost:5000/kiosk`
2. Add items and proceed to checkout
3. Select "Cash" payment
4. In a new terminal, simulate cash insertion:

```powershell
# PowerShell - Replace order number from URL
$order = "ORD-20250114-ABC123"
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/test/simulate" `
    -Method Post -ContentType "application/json" `
    -Body (@{orderNumber=$order; amount=100} | ConvertTo-Json)
```

Watch the UI update in real-time! 🎉

### 2. With Arduino Hardware

```bash
# Install Python dependencies
pip install -r requirements.txt

# Update configuration in arduino_cash_reader.py
# - Set ARDUINO_PORT (e.g., "COM3" or "/dev/ttyUSB0")
# - Set KIOSK_API_URL if not localhost

# Run the Python API
python arduino_cash_reader.py
```

## 🔌 Hardware Requirements

- **Arduino Uno/Mega** (any compatible board)
- **Bill Acceptor** (e.g., JY-15A, ICT A7+)
- **Coin Acceptor** (e.g., CH-923, 616)
- **Raspberry Pi** (for production deployment)
- USB cable (Arduino to Pi/PC)
- 12V power supply (for acceptors)

## 💡 Key Features

✅ **Real-time Updates**: SignalR broadcasts cash insertions instantly  
✅ **Automatic Completion**: Payment completes when sufficient cash received  
✅ **Change Calculation**: Automatically calculates and displays change  
✅ **Cancel Anytime**: Cancel payment and return inserted cash  
✅ **Kitchen Integration**: Notifies kitchen staff when payment complete  
✅ **Inventory Management**: Decreases product quantities automatically  
✅ **Progress Tracking**: Visual progress bar and status updates  
✅ **Error Handling**: Robust error handling and reconnection logic  

## 📡 API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/cash-payment/init` | POST | Initialize payment session |
| `/api/cash-payment/update` | POST | Update cash amount (from Python) |
| `/api/cash-payment/status/{orderNumber}` | GET | Get payment status |
| `/api/cash-payment/cancel/{orderNumber}` | POST | Cancel payment |
| `/api/cash-payment/test/simulate` | POST | Simulate cash (testing) |
| `/cashpaymenthub` | SignalR | Real-time communication hub |

## 🎨 User Interface

The cash payment page shows:
- **Total Amount Due**: Clear display of payment required
- **Amount Inserted**: Real-time updates as cash is received
- **Remaining Balance**: How much more is needed
- **Progress Bar**: Visual representation of payment progress
- **Change Display**: Shows change amount when overpayment occurs
- **Status Messages**: Clear feedback at each step

## 🔄 Payment Flow

```
1. Customer selects Cash payment
   ↓
2. Order created in database (Status: Pending)
   ↓
3. Redirect to /cash-payment page
   ↓
4. SignalR connection established
   ↓
5. Payment session initialized
   ↓
6. Customer inserts cash
   ↓
7. Arduino detects → Python sends update → SignalR broadcasts
   ↓
8. UI updates in real-time
   ↓
9. When total reached:
   - Order status → Paid
   - Product quantities decreased
   - Kitchen notified
   - Change calculated
   ↓
10. Customer collects change and order
```

## 🐍 Python API Integration

The Python script acts as a bridge:

```
Arduino (Hardware) → Python API → ASP.NET Core → Blazor UI
```

Key responsibilities:
- Read serial data from Arduino
- Parse bill/coin insertions
- POST updates to REST API
- Handle reconnection and errors
- Log all transactions

## 🔧 Configuration

### Arduino
```cpp
const int BILL_ACCEPTOR_PIN = 2;  // Interrupt pin
const int COIN_ACCEPTOR_PIN = 3;  // Interrupt pin
const int BAUD_RATE = 9600;
```

### Python
```python
ARDUINO_PORT = "/dev/ttyUSB0"  # Or "COM3" on Windows
KIOSK_API_URL = "http://localhost:5000"
BAUD_RATE = 9600
```

### ASP.NET Core
No configuration needed - works out of the box!

## 🧪 Testing Checklist

- [ ] UI loads correctly
- [ ] SignalR connection established
- [ ] Simulate endpoint works
- [ ] Real-time updates display
- [ ] Progress bar animates
- [ ] Payment completes automatically
- [ ] Change calculated correctly
- [ ] Cancel payment works
- [ ] Order sent to kitchen
- [ ] Product quantities updated

See `CASH_PAYMENT_TESTING.md` for detailed testing procedures.

## 🚨 Troubleshooting

### UI Not Updating
- Check browser console for SignalR errors
- Verify hub URL: `/cashpaymenthub`
- Ensure order number matches

### Python Can't Connect to Arduino
- Check USB cable and port
- Verify baud rate (9600)
- Install drivers if needed
- Check permissions (Linux: add user to dialout group)

### Wrong Amounts Detected
- Check pulse mapping in Arduino code
- Consult your acceptor's datasheet
- Adjust timeout values

### API Connection Failed
- Ensure .NET app is running
- Check correct port (5000 or 5001)
- Verify firewall settings
- Update API URL in Python script

## 📚 Documentation

| Document | Purpose |
|----------|---------|
| `CASH_PAYMENT_SETUP.md` | Complete setup, deployment, and production guide |
| `CASH_PAYMENT_TESTING.md` | Step-by-step testing instructions |
| `ARDUINO_PROTOCOL.md` | Serial protocol specification |
| `CASH_PAYMENT_README.md` | This file - quick reference |

## 🔐 Security (Production)

Before going live:
1. ✅ Enable API authentication
2. ✅ Use HTTPS only
3. ✅ Add rate limiting
4. ✅ Validate all amounts
5. ✅ Enable comprehensive logging
6. ✅ Setup monitoring/alerts
7. ✅ IP whitelist Python API
8. ✅ Use system services (systemd)

See `CASH_PAYMENT_SETUP.md` for security implementation details.

## 🌐 Production Deployment

### Raspberry Pi Setup

1. **Install .NET Runtime**
2. **Configure systemd service for .NET app**
3. **Configure systemd service for Python script**
4. **Setup autostart on boot**
5. **Configure firewall**
6. **Enable monitoring**

See detailed instructions in `CASH_PAYMENT_SETUP.md`.

## 💬 Protocol Example

```
# Raspberry Pi → Arduino
ORDER:ORD-20250114-ABC123

# Arduino → Raspberry Pi
ORDER:ORD-20250114-ABC123
BILL:100
BILL:50
COIN:5.00
```

The Python script receives these messages and forwards them to the ASP.NET Core API.

## 📊 Real-time Events

SignalR broadcasts these events:

- **CashAmountUpdated**: When cash is inserted
- **PaymentCompleted**: When payment is complete
- **PaymentCancelled**: When payment is cancelled

The Blazor UI listens to these events and updates instantly.

## 🎓 Learning Resources

- **SignalR**: Real-time web functionality
- **Arduino**: Embedded programming
- **Serial Communication**: UART protocol
- **Interrupt Handling**: Hardware event detection
- **Blazor Server**: Server-side rendering with WebSocket

## 🤝 Support

For issues or questions:
1. Check the troubleshooting sections in the documentation
2. Review server logs: `journalctl -u kiosk -f`
3. Check Python logs: `tail -f cash_reader.log`
4. Verify hardware connections
5. Test with simulate endpoint first

## 📈 Future Enhancements

Potential improvements:
- [ ] Cash dispenser for automatic change
- [ ] Multiple kiosk support
- [ ] Counterfeit detection (hardware dependent)
- [ ] SMS notifications on completion
- [ ] Admin dashboard for cash tracking
- [ ] Receipt printer integration
- [ ] Customer display (dual screen)

## ✨ Credits

Built with:
- **ASP.NET Core 9.0** - Backend framework
- **Blazor Server** - Interactive UI
- **SignalR** - Real-time communication
- **MudBlazor** - UI components
- **Python 3** - Hardware integration
- **Arduino** - Embedded control

---

**Version**: 1.0  
**Last Updated**: January 2025  
**License**: Internal Use Only

For complete documentation, see the other markdown files in this directory.

