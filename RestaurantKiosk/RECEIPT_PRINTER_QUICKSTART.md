# Receipt Printer Quick Start Guide

Get your receipt printer up and running in 5 minutes!

## Prerequisites
- Raspberry Pi with Python 3 installed
- Thermal receipt printer (ESC/POS compatible)
- USB cable connecting printer to Raspberry Pi

## Quick Setup

### 1. Install Dependencies (Raspberry Pi)
```bash
# Install Python packages
pip3 install python-escpos Flask requests

# For USB printers
sudo apt-get install libusb-1.0-0-dev -y
```

### 2. Find Your Serial Port (USB-to-TTL)
```bash
# Plug in your USB-to-TTL adapter and check:
ls /dev/tty*

# Most common: /dev/ttyUSB0
# Also check: /dev/ttyAMA0, /dev/serial0
```

### 3. Configure the Script
Edit `receipt_printer.py` (already set for serial):
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"  # Your serial port
SERIAL_BAUDRATE = 9600        # Try 9600, 19200, or 38400
```

### 4. Set Serial Permissions
```bash
# Add user to dialout group
sudo usermod -a -G dialout $USER

# Set port permissions
sudo chmod 666 /dev/ttyUSB0

# Create udev rule for persistence
echo 'KERNEL=="ttyUSB[0-9]*", MODE="0666"' | sudo tee /etc/udev/rules.d/99-usb-serial.rules
sudo udevadm control --reload-rules

# Log out and back in, or:
newgrp dialout
```

### 5. Start the Service
```bash
python3 receipt_printer.py
```

You should see:
```
============================================================
Restaurant Kiosk - Receipt Printer Service
============================================================
Printer Type: serial
Flask API: http://0.0.0.0:5001
============================================================
INFO - Connecting to serial printer...
INFO - Successfully connected to printer
 * Running on http://0.0.0.0:5001
```

### 6. Test the Printer
Open another terminal and run:
```bash
curl -X POST http://localhost:5001/api/receipt/test
```

If successful, your printer should print a test receipt! 🎉

## Configure .NET Application

Edit `appsettings.json`:
```json
{
  "Receipt": {
    "PrinterApiUrl": "http://localhost:5001",
    "RestaurantName": "My Restaurant",
    "RestaurantAddress": "123 Main Street",
    "RestaurantPhone": "+63 XXX XXX XXXX",
    "RestaurantEmail": "info@myrestaurant.com"
  }
}
```

## Run as Background Service (Optional)

Create service file:
```bash
sudo nano /etc/systemd/system/receipt-printer.service
```

Add:
```ini
[Unit]
Description=Receipt Printer Service
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/RestaurantKiosk
ExecStart=/usr/bin/python3 /home/pi/RestaurantKiosk/receipt_printer.py
Restart=always

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable receipt-printer
sudo systemctl start receipt-printer
sudo systemctl status receipt-printer
```

## Troubleshooting

### "Printer not found" or "Permission denied"
```bash
# Check if serial device exists
ls -l /dev/ttyUSB0

# Check permissions
groups  # Should include 'dialout'

# Add to dialout group if missing
sudo usermod -a -G dialout $USER
newgrp dialout

# Try running with sudo (temporary test)
sudo python3 receipt_printer.py

# If using different port, update SERIAL_PORT in the script
```

### Port 5001 already in use
```bash
# Find and kill the process
sudo lsof -i :5001
sudo kill -9 <PID>
```

### Permission denied
```bash
# Check if in dialout group
groups

# Add to group
sudo usermod -a -G dialout $USER
newgrp dialout

# Verify serial rules
cat /etc/udev/rules.d/99-usb-serial.rules

# Reload rules
sudo udevadm control --reload-rules
sudo udevadm trigger

# Reboot if needed
sudo reboot
```

### Wrong baud rate / Garbage output
```bash
# Edit receipt_printer.py and try different baud rates:
SERIAL_BAUDRATE = 19200  # Try: 9600, 19200, 38400, 115200

# Common thermal printer baud rates: 9600 (most common), 19200
```

## Test with an Order

Once both services are running, create an order in the kiosk and complete payment. The receipt should print automatically!

You can also manually print a receipt:
```bash
curl -X POST http://localhost:5000/api/receipt/print/ORDER-NUMBER
```

## Next Steps

- See [RECEIPT_PRINTER_SETUP.md](RECEIPT_PRINTER_SETUP.md) for detailed configuration
- Customize receipt format in `receipt_printer.py`
- Set up automatic service start on boot
- Configure for production deployment

## Your Setup: USB-to-TTL (Serial)

Your printer is connected via USB-to-TTL adapter, so you're using **serial communication** instead of direct USB.

**Configuration** (already set in scripts):
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"  # Most common
SERIAL_BAUDRATE = 9600        # Most common
```

**Common serial ports**:
- `/dev/ttyUSB0` - USB-to-TTL adapters (most common)
- `/dev/ttyAMA0` - Raspberry Pi GPIO UART
- `/dev/serial0` - Raspberry Pi serial link

**Common baud rates**:
- `9600` - Most thermal printers (try this first!)
- `19200` - Some models
- `38400` - Less common
- `115200` - Rare for thermal printers

**See `RECEIPT_PRINTER_SERIAL_SETUP.md` for detailed USB-to-TTL setup!**

---

## Your Printer: SHK24

You're using an **SHK24 thermal printer** - great choice! It's fully supported.

**Your configuration** (already set):
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"
SERIAL_BAUDRATE = 9600  # SHK24 default
```

**Paper width**: 58mm (most common) = 32 characters per line

**See `RECEIPT_PRINTER_SHK24_CONFIG.md` for SHK24-specific setup guide!**

