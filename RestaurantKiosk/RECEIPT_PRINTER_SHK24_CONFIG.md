# SHK24 Receipt Printer Configuration

## Printer Specifications

**Model**: SHK24 Thermal Receipt Printer
**Type**: ESC/POS compatible thermal printer
**Connection**: USB-to-TTL (Serial)
**Paper Width**: 58mm or 80mm (depending on variant)
**Interface**: RS-232 Serial (via USB-to-TTL adapter)

## Recommended Settings

### For SHK24 Printer

```python
# Configuration for SHK24
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"
SERIAL_BAUDRATE = 9600  # SHK24 default baud rate
```

### Serial Communication Parameters

```python
# Standard settings for SHK24
Port: /dev/ttyUSB0 (or /dev/ttyUSB1, /dev/ttyAMA0)
Baud Rate: 9600 (default for most SHK24 models)
Data Bits: 8
Parity: None
Stop Bits: 1
Flow Control: None
```

## SHK24-Specific Notes

### Paper Width
The SHK24 comes in two variants:
- **58mm** - Narrower receipts (most common)
- **80mm** - Wider receipts

Both work with the same configuration. The python-escpos library automatically handles paper width.

### Character Width
- **58mm paper**: ~32 characters per line
- **80mm paper**: ~48 characters per line

The receipt scripts are already configured for both:
```python
self.printer.text('=' * 32 + '\n')  # Works for 58mm
```

### Supported Commands
SHK24 supports standard ESC/POS commands:
- ✅ Text formatting (bold, underline, size)
- ✅ Character encoding
- ✅ Paper cutting
- ✅ QR codes
- ✅ Barcodes
- ✅ Line spacing

## Quick Setup for SHK24

### 1. Physical Connection

```
Raspberry Pi → USB → USB-to-TTL Adapter → RX/TX/GND → SHK24 Printer
```

**Wiring**:
```
USB-to-TTL Adapter    →    SHK24 Printer
TX (White/Orange)     →    RX (Receive)
RX (Green)            →    TX (Transmit)
GND (Black)           →    GND (Ground)
```

**Important**: Do NOT connect VCC/5V unless your SHK24 needs power from the adapter (most don't - they have their own power supply).

### 2. Find Serial Port

```bash
# Before plugging adapter
ls /dev/tty*

# Plug in USB-to-TTL adapter
ls /dev/tty*

# New device should appear (usually /dev/ttyUSB0)
```

### 3. Verify Connection

```bash
# Check adapter is detected
lsusb

# Should see something like:
# Bus 001 Device 005: ID 1a86:7523 QinHeng CH340 serial converter
# or
# Bus 001 Device 006: ID 0403:6001 FTDI FT232 Serial
```

### 4. Set Permissions

```bash
# Add user to dialout group
sudo usermod -a -G dialout $USER
sudo usermod -a -G tty $USER

# Apply changes
newgrp dialout

# Set port permissions
sudo chmod 666 /dev/ttyUSB0

# Create persistent rule
echo 'KERNEL=="ttyUSB[0-9]*", MODE="0666"' | sudo tee /etc/udev/rules.d/99-usb-serial.rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### 5. Configure Python Scripts

Both scripts are already configured correctly:

**`receipt_printer.py`**:
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"
SERIAL_BAUDRATE = 9600
```

**`receipt_printer_client.py`**:
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"
SERIAL_BAUDRATE = 9600
```

### 6. Test Printer

```bash
# Quick serial test
echo "Hello SHK24" > /dev/ttyUSB0

# If nothing prints, try:
stty -F /dev/ttyUSB0 9600
echo "Hello SHK24" > /dev/ttyUSB0
```

### 7. Run Receipt Service

```bash
python3 receipt_printer.py
```

Expected output:
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

### 8. Print Test Receipt

```bash
curl -X POST http://localhost:5001/api/receipt/test
```

Your SHK24 should print a test receipt! 🎉

## Troubleshooting SHK24

### Printer Powers On But Won't Print

**Check Serial Settings**:
```bash
# Test with stty
stty -F /dev/ttyUSB0 9600
echo -e "\x1B\x40Hello\n\n\n" > /dev/ttyUSB0
# (\x1B\x40 is ESC @ - initialize printer command)
```

**Try Different Baud Rate**:
Some SHK24 models might use different rates:
```python
SERIAL_BAUDRATE = 19200  # Try this if 9600 doesn't work
```

### Garbled Output

**Wrong Baud Rate** - Edit `receipt_printer.py`:
```python
# Try these in order:
SERIAL_BAUDRATE = 9600   # Most common
SERIAL_BAUDRATE = 19200  # Some models
SERIAL_BAUDRATE = 38400  # Less common
```

**Wrong Serial Port**:
```bash
# List all ports
ls -la /dev/tty*

# Try different port in script
SERIAL_PORT = "/dev/ttyUSB1"  # or /dev/ttyAMA0
```

### Characters Cut Off

**Paper Width Mismatch** - The script uses 32 characters per line (for 58mm):
```python
# For 58mm paper (default)
self.printer.text('=' * 32 + '\n')

# For 80mm paper, edit receipt_printer.py:
self.printer.text('=' * 48 + '\n')
```

### Printer Not Cutting Paper

Some SHK24 models don't have auto-cutter. If `self.printer.cut()` causes errors:

**Edit `receipt_printer.py`**:
```python
# Replace:
self.printer.cut()

# With:
try:
    self.printer.cut()
except:
    # Manual cut - just add extra lines
    self.printer.text('\n\n\n\n')
```

### Permission Errors

```bash
# Check permissions
ls -l /dev/ttyUSB0

# Should show: crw-rw-rw-
# If not:
sudo chmod 666 /dev/ttyUSB0

# Check group membership
groups
# Should include: dialout tty

# Add if missing
sudo usermod -a -G dialout $USER
sudo usermod -a -G tty $USER
newgrp dialout
```

## SHK24 Receipt Format Optimization

### For 58mm Paper (32 chars/line)

```python
# Optimal formatting for SHK24 58mm
self.printer.text(f"{'Item':<20} {'Qty':>4} {'Price':>7}\n")
self.printer.text('-' * 32 + '\n')
```

### For 80mm Paper (48 chars/line)

If you have 80mm variant, update in `receipt_printer.py`:

```python
# Change all instances of 32 to 48
self.printer.text('=' * 48 + '\n')
self.printer.text('-' * 48 + '\n')
self.printer.text(f"{'Item':<30} {'Qty':>6} {'Amount':>10}\n")
```

## Testing Checklist for SHK24

- [ ] USB-to-TTL adapter connected
- [ ] Adapter detected in `lsusb`
- [ ] Serial port exists (`/dev/ttyUSB0`)
- [ ] User in dialout group
- [ ] Port permissions set (666)
- [ ] Baud rate 9600 configured
- [ ] Printer powered on (indicator light)
- [ ] Paper loaded correctly
- [ ] Paper cover closed
- [ ] Test command works: `echo "test" > /dev/ttyUSB0`
- [ ] Python script connects successfully
- [ ] Test receipt prints

## Advanced: Direct SHK24 Testing

### Test with Python Serial

```python
import serial
import time

# Open serial port
ser = serial.Serial('/dev/ttyUSB0', 9600, timeout=1)
time.sleep(0.5)

# Initialize printer (ESC @)
ser.write(b'\x1B\x40')

# Print text
ser.write(b'Hello SHK24!\n')
ser.write(b'This is a test.\n')
ser.write(b'\n\n\n')

# Close
ser.close()
print("Test sent to SHK24!")
```

Run with:
```bash
python3 test_shk24.py
```

### Test with ESC/POS Commands

```python
from escpos.printer import Serial

# Connect to SHK24
p = Serial('/dev/ttyUSB0', baudrate=9600)

# Print test
p.text("SHK24 Test Receipt\n")
p.text("==================\n")
p.text("Item 1         5.00\n")
p.text("Item 2        10.00\n")
p.text("------------------\n")
p.text("Total:        15.00\n")
p.text("\n\n\n")

# Try to cut (if auto-cutter available)
try:
    p.cut()
except:
    pass

p.close()
print("SHK24 test complete!")
```

## SHK24 Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Printer doesn't respond | Check RX/TX wiring (should be crossed) |
| Permission denied | Add to dialout group: `sudo usermod -a -G dialout $USER` |
| Garbled output | Try different baud rate: 9600, 19200, 38400 |
| Text too wide | Using 80mm paper width but script is for 58mm |
| No paper cut | SHK24 may not have auto-cutter, add extra newlines |
| Random characters | Check GND connection |
| Intermittent printing | Check USB cable quality |

## Production Configuration

Once working, create systemd service:

```bash
# Copy service file
sudo cp deployment/receipt-printer-client.service /etc/systemd/system/

# Edit if needed to match your setup
sudo nano /etc/systemd/system/receipt-printer-client.service

# Enable and start
sudo systemctl enable receipt-printer-client
sudo systemctl start receipt-printer-client

# Check status
sudo systemctl status receipt-printer-client

# View logs
sudo journalctl -u receipt-printer-client -f
```

## SHK24 Specifications Reference

| Specification | Value |
|--------------|-------|
| Print Method | Direct thermal |
| Paper Width | 58mm or 80mm |
| Print Width | 48mm (58mm) / 72mm (80mm) |
| Paper Thickness | 0.06-0.08mm |
| Print Speed | 50-90 mm/sec |
| Interface | RS-232 Serial |
| Baud Rate | 9600 (default) |
| Character Set | ESC/POS compatible |
| Voltage | DC 5V-9V (via power adapter) |

## Additional Resources

- [python-escpos Documentation](https://python-escpos.readthedocs.io/)
- [ESC/POS Command Reference](https://reference.epson-biz.com/modules/ref_escpos/)
- [SHK24 Manual](https://www.google.com/search?q=SHK24+thermal+printer+manual)

## Summary

Your SHK24 printer is fully supported! The configuration is:

✅ **Connection**: USB-to-TTL (Serial)
✅ **Port**: `/dev/ttyUSB0`
✅ **Baud Rate**: `9600`
✅ **Paper Width**: 58mm (32 chars/line)
✅ **Protocol**: ESC/POS
✅ **Scripts**: Already configured correctly!

Just set up permissions and test it! 🖨️

