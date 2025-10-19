# Receipt Printer Setup Guide

This guide explains how to set up and configure the thermal receipt printer system for the Restaurant Kiosk application.

## Overview

The receipt printing system consists of:
- **Python Service** (`receipt_printer.py`) - Runs on Raspberry Pi and interfaces with the thermal printer
- **.NET Receipt Service** - Generates receipt data and sends print requests
- **API Controller** - Provides endpoints for printing receipts

## Architecture

```
┌─────────────────────┐         ┌──────────────────┐         ┌─────────────────┐
│   VPS Server        │         │  Raspberry Pi    │         │  Thermal        │
│   (.NET App)        │ ──HTTP─>│  (Python Service)│ ──USB──>│  Printer        │
│                     │         │                  │         │                 │
└─────────────────────┘         └──────────────────┘         └─────────────────┘
```

## Prerequisites

### Hardware Requirements
- Thermal receipt printer (ESC/POS compatible)
- Raspberry Pi (any model with USB or network connectivity)
- USB cable (for USB printers) or network connection (for network printers)

### Software Requirements (Raspberry Pi)
- Python 3.7 or higher
- pip package manager

## Installation

### 1. Install Python Dependencies on Raspberry Pi

```bash
# Update system packages
sudo apt-get update
sudo apt-get upgrade -y

# Install Python and pip
sudo apt-get install python3 python3-pip -y

# Install required Python packages
pip3 install python-escpos Flask requests

# For USB printers, install additional dependencies
sudo apt-get install libusb-1.0-0-dev -y
```

### 2. Configure Printer Connection

#### For USB Printers

1. Connect the printer via USB to your Raspberry Pi

2. Find the printer's Vendor ID and Product ID:
```bash
lsusb
```

Example output:
```
Bus 001 Device 004: ID 04b8:0e15 Seiko Epson Corp. TM-T20
```

In this example:
- Vendor ID: `0x04b8`
- Product ID: `0x0e15`

3. Update `receipt_printer.py` with your printer IDs:
```python
USB_VENDOR_ID = 0x04b8   # Replace with your vendor ID
USB_PRODUCT_ID = 0x0e15  # Replace with your product ID
```

4. Set up USB permissions:
```bash
# Create udev rule for printer access
sudo nano /etc/udev/rules.d/99-escpos.rules
```

Add this line (replace with your IDs):
```
SUBSYSTEM=="usb", ATTRS{idVendor}=="04b8", ATTRS{idProduct}=="0e15", MODE="0666"
```

Reload udev rules:
```bash
sudo udevadm control --reload-rules
sudo udevadm trigger
```

#### For Network Printers

Update `receipt_printer.py`:
```python
PRINTER_TYPE = "network"
NETWORK_HOST = "192.168.1.100"  # Your printer's IP address
NETWORK_PORT = 9100              # Usually 9100 for ESC/POS
```

#### For Serial Printers

Update `receipt_printer.py`:
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"     # Your serial port
SERIAL_BAUDRATE = 9600           # Your printer's baud rate
```

### 3. Configure .NET Application

Edit `appsettings.json` or `appsettings.Development.json`:

```json
{
  "Receipt": {
    "PrinterApiUrl": "http://localhost:5001",
    "RestaurantName": "Your Restaurant Name",
    "RestaurantAddress": "123 Main Street, City, Country",
    "RestaurantPhone": "+63 XXX XXX XXXX",
    "RestaurantEmail": "info@restaurant.com"
  }
}
```

For production (when .NET app is on VPS and Python service on Raspberry Pi):
```json
{
  "Receipt": {
    "PrinterApiUrl": "http://192.168.1.100:5001",  // Raspberry Pi IP
    "RestaurantName": "Your Restaurant Name",
    "RestaurantAddress": "123 Main Street, City, Country",
    "RestaurantPhone": "+63 XXX XXX XXXX",
    "RestaurantEmail": "info@restaurant.com"
  }
}
```

## Running the Services

### Start Python Receipt Printer Service on Raspberry Pi

#### Manual Start
```bash
cd /path/to/RestaurantKiosk
python3 receipt_printer.py
```

#### Run as a System Service

1. Create a systemd service file:
```bash
sudo nano /etc/systemd/system/receipt-printer.service
```

2. Add this content:
```ini
[Unit]
Description=Restaurant Kiosk Receipt Printer Service
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/RestaurantKiosk
ExecStart=/usr/bin/python3 /home/pi/RestaurantKiosk/receipt_printer.py
Restart=always
RestartSec=10
StandardOutput=append:/var/log/receipt-printer.log
StandardError=append:/var/log/receipt-printer-error.log

[Install]
WantedBy=multi-user.target
```

3. Enable and start the service:
```bash
sudo systemctl daemon-reload
sudo systemctl enable receipt-printer
sudo systemctl start receipt-printer
```

4. Check service status:
```bash
sudo systemctl status receipt-printer
```

5. View logs:
```bash
sudo journalctl -u receipt-printer -f
```

## Testing

### Test Printer Connection

1. Test from Raspberry Pi directly:
```bash
curl -X POST http://localhost:5001/api/receipt/test
```

2. Test from .NET application:
```bash
curl -X POST http://your-vps-ip:5000/api/receipt/test
```

3. Test via browser:
Navigate to the admin panel and use the test receipt function (if available).

### Print a Test Order

```bash
curl -X POST http://your-vps-ip:5000/api/receipt/print/ORD-20250115-001
```

## API Endpoints

### Python Service Endpoints (Port 5001)

#### Health Check
```http
GET /health
```

#### Print Receipt
```http
POST /api/receipt/print
Content-Type: application/json

{
  "restaurantName": "Restaurant Name",
  "orderNumber": "ORD-20250115-001",
  "orderDate": "2025-01-15 10:30:00",
  "items": [...],
  "totalAmount": 250.00,
  "paymentMethod": "Cash"
}
```

#### Test Print
```http
POST /api/receipt/test
```

#### Printer Status
```http
GET /api/receipt/status
```

### .NET Application Endpoints (Port 5000)

#### Print Receipt by Order Number
```http
POST /api/receipt/print/{orderNumber}
Content-Type: application/json

{
  "amountPaid": 300.00,
  "change": 50.00
}
```

#### Reprint Receipt
```http
POST /api/receipt/reprint/{orderNumber}
```

#### Test Printer
```http
POST /api/receipt/test
```

#### Get Printer Status
```http
GET /api/receipt/status
```

#### Preview Receipt Data
```http
GET /api/receipt/preview/{orderNumber}
```

## Troubleshooting

### Printer Not Found (USB)

1. Check if printer is detected:
```bash
lsusb
```

2. Check permissions:
```bash
ls -l /dev/usb/lp*
```

3. Try running as root (temporary test):
```bash
sudo python3 receipt_printer.py
```

4. Check if another process is using the printer:
```bash
sudo lsof | grep usb
```

### Connection Refused

1. Check if Python service is running:
```bash
sudo systemctl status receipt-printer
# or
ps aux | grep receipt_printer
```

2. Check if port 5001 is open:
```bash
sudo netstat -tuln | grep 5001
```

3. Check firewall:
```bash
sudo ufw status
# Allow port if needed
sudo ufw allow 5001
```

### Receipt Not Printing

1. Check Python service logs:
```bash
tail -f receipt_printer.log
# or if running as service
sudo journalctl -u receipt-printer -f
```

2. Test printer directly:
```python
from escpos.printer import Usb
p = Usb(0x04b8, 0x0e15)  # Your IDs
p.text("Test\n")
p.cut()
```

3. Check paper and printer status

### Network Issues (VPS to Raspberry Pi)

1. Test connectivity:
```bash
ping raspberry-pi-ip
```

2. Test HTTP connection:
```bash
curl http://raspberry-pi-ip:5001/health
```

3. Check if Raspberry Pi firewall is blocking:
```bash
# On Raspberry Pi
sudo ufw allow 5001
```

4. Update .NET configuration with correct IP:
```json
"PrinterApiUrl": "http://192.168.1.100:5001"
```

## Automatic Receipt Printing

Receipts are automatically printed when:

1. **Cash Payment Completed**: When sufficient cash is inserted and payment is complete
2. **Online Payment Successful**: When payment via GCash, Maya, or Invoice is confirmed
3. **Webhook Received**: When payment gateway confirms successful payment

You can also manually print/reprint receipts via the API endpoints.

## Receipt Customization

To customize the receipt format, edit the methods in `receipt_printer.py`:

- `_print_header()`: Restaurant name and address
- `_print_order_details()`: Order number and date
- `_print_items()`: Order items list
- `_print_totals()`: Subtotal, tax, and total
- `_print_payment_info()`: Payment method and change
- `_print_footer()`: Thank you message and QR code

## Performance Considerations

1. **Async Printing**: Receipts are printed asynchronously to avoid blocking the main application
2. **Error Handling**: Print failures are logged but don't affect order completion
3. **Timeout**: HTTP requests have a 10-second timeout
4. **Retry Logic**: Consider implementing retry logic for critical receipts

## Security

1. The Python service binds to `0.0.0.0:5001` by default
2. For production, consider:
   - Using a reverse proxy (nginx)
   - Implementing authentication
   - Restricting access by IP
   - Using HTTPS

## Additional Resources

- [python-escpos Documentation](https://python-escpos.readthedocs.io/)
- [ESC/POS Command Reference](https://reference.epson-biz.com/modules/ref_escpos/)
- Raspberry Pi Documentation: https://www.raspberrypi.org/documentation/

## Support

For issues or questions:
1. Check the logs: `receipt_printer.log`
2. Verify printer hardware connection
3. Test with the provided API endpoints
4. Check network connectivity between VPS and Raspberry Pi

