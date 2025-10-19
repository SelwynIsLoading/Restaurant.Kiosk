# Receipt Printer Setup - USB to TTL (Serial)

Your thermal printer is connected via a **USB-to-TTL adapter** (serial connection). This guide covers setup for this specific configuration.

## What is USB-to-TTL?

USB-to-TTL adapters (like FTDI FT232, CH340, CP2102) convert USB to serial (UART/TTL) signals:

```
┌─────────────┐    USB    ┌──────────────┐   TTL/Serial   ┌─────────────┐
│ Raspberry Pi│───────────│ USB-to-TTL   │────RX/TX───────│   Thermal   │
│             │           │   Adapter    │────GND─────────│   Printer   │
└─────────────┘           └──────────────┘                └─────────────┘
```

## Quick Setup

### 1. Identify Your Serial Port

```bash
# Before plugging in the adapter
ls /dev/tty*

# Plug in the USB-to-TTL adapter
ls /dev/tty*

# Look for new device, usually:
# /dev/ttyUSB0  (most common for USB adapters)
# /dev/ttyAMA0  (Raspberry Pi GPIO UART)
# /dev/serial0  (Raspberry Pi serial link)
```

**Most likely**: `/dev/ttyUSB0`

### 2. Check Adapter Details

```bash
# View USB devices
lsusb

# Example output:
# Bus 001 Device 005: ID 1a86:7523 QinHeng Electronics CH340 serial converter
# Bus 001 Device 004: ID 0403:6001 Future Technology Devices International FT232 Serial

# Get detailed info
dmesg | grep tty

# Example output:
# [12345.123456] usb 1-1.2: ch341-uart converter now attached to ttyUSB0
```

### 3. Find Your Printer's Baud Rate

Common baud rates for thermal printers:
- **9600** (most common)
- 19200
- 38400
- 115200

Check your printer manual or try each one.

### 4. Configure Python Scripts

Both scripts are **already configured** for serial/TTL:

**`receipt_printer.py`**:
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"  # Your serial port
SERIAL_BAUDRATE = 9600         # Your printer's baud rate
```

**`receipt_printer_client.py`**:
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"
SERIAL_BAUDRATE = 9600
```

If your port or baud rate is different, update these values.

### 5. Set Permissions

```bash
# Add user to dialout group (for serial port access)
sudo usermod -a -G dialout $USER
sudo usermod -a -G tty $USER

# Log out and back in, or:
newgrp dialout

# Alternatively, set permissions directly
sudo chmod 666 /dev/ttyUSB0
```

### 6. Install Serial Library

```bash
pip3 install pyserial
```

### 7. Test Connection

```bash
# Quick test - send test string
echo "Hello Printer" > /dev/ttyUSB0

# If nothing happens, try different baud rate:
stty -F /dev/ttyUSB0 9600
echo "Hello Printer" > /dev/ttyUSB0

# Test with Python
python3 << EOF
import serial
ser = serial.Serial('/dev/ttyUSB0', 9600, timeout=1)
ser.write(b'Hello from Python\n')
ser.close()
print("Test sent!")
EOF
```

### 8. Run Receipt Printer Service

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
```

### 9. Test Print

```bash
curl -X POST http://localhost:5001/api/receipt/test
```

Your printer should print a test receipt!

## Troubleshooting USB-to-TTL

### "Permission denied" on /dev/ttyUSB0

```bash
# Check current permissions
ls -l /dev/ttyUSB0

# Solution 1: Add to dialout group
sudo usermod -a -G dialout $USER
# Log out and back in

# Solution 2: Direct permission
sudo chmod 666 /dev/ttyUSB0

# Solution 3: Create udev rule (permanent)
echo 'KERNEL=="ttyUSB[0-9]*", MODE="0666"' | sudo tee /etc/udev/rules.d/99-usb-serial.rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### "Serial port not found"

```bash
# Check if adapter is detected
lsusb

# Check kernel messages
dmesg | tail -20

# List all serial devices
ls -la /dev/tty*

# Check if driver is loaded
lsmod | grep -E "usbserial|ch341|ftdi"

# Load driver manually if needed
sudo modprobe usbserial
sudo modprobe ftdi_sio  # For FTDI adapters
sudo modprobe ch341     # For CH340 adapters
```

### "Device or resource busy"

```bash
# Check what's using the port
sudo lsof | grep ttyUSB0

# Kill the process if needed
sudo kill -9 <PID>

# Or disconnect and reconnect the adapter
```

### "Printer not responding" or "Garbage output"

**Wrong Baud Rate** - Try different rates:

```python
# Edit receipt_printer.py
SERIAL_BAUDRATE = 19200  # Try: 9600, 19200, 38400, 115200
```

**Wrong Serial Settings** - Update connection parameters:

```python
# In receipt_printer.py or receipt_printer_client.py
self.printer = Serial(
    SERIAL_PORT,
    baudrate=SERIAL_BAUDRATE,
    bytesize=serial.EIGHTBITS,
    parity=serial.PARITY_NONE,
    stopbits=serial.STOPBITS_ONE,
    timeout=1
)
```

**Hardware Wiring** - Check connections:
- RX (adapter) → TX (printer)
- TX (adapter) → RX (printer)
- GND (adapter) → GND (printer)
- ⚠️ Don't connect VCC if printer has its own power!

### Adapter Not Detected

```bash
# Check USB connection
lsusb

# If not showing, try:
# - Different USB port
# - Different cable
# - Test adapter on another computer

# Install drivers if needed (CH340 example)
sudo apt-get update
sudo apt-get install ch341-dkms
```

## Common USB-to-TTL Adapters

### FTDI FT232RL
```bash
# Usually auto-detected as /dev/ttyUSB0
# Driver: ftdi_sio (built-in to Raspberry Pi OS)
# No additional setup needed
```

### CH340/CH341
```bash
# Usually /dev/ttyUSB0
# Driver: ch341 (built-in to modern kernels)
# Older systems may need: sudo apt-get install ch341-dkms
```

### CP2102/CP2104 (Silicon Labs)
```bash
# Usually /dev/ttyUSB0
# Driver: cp210x (built-in)
# Very reliable, recommended
```

### PL2303 (Prolific)
```bash
# Usually /dev/ttyUSB0
# Driver: pl2303 (built-in)
# Note: Many cheap clones exist, may have issues
```

## Wiring Guide

### Standard Connection

```
USB-to-TTL Adapter          Thermal Printer
┌────────────────┐          ┌──────────────┐
│ TX  (White)    │─────────>│ RX (Receive) │
│ RX  (Green)    │<─────────│ TX (Transmit)│
│ GND (Black)    │──────────│ GND (Ground) │
│ VCC (Red)      │  (Usually not connected) │
└────────────────┘          └──────────────┘
```

### Important Notes:
1. **Cross connection**: TX → RX, RX → TX
2. **GND must be connected**
3. **VCC**: Only connect if printer needs 5V power from adapter (rare)
4. **Most thermal printers have their own power supply**

## Printer Settings

### Verify Serial Settings

Most thermal printers use:
- **Baud Rate**: 9600
- **Data Bits**: 8
- **Parity**: None
- **Stop Bits**: 1
- **Flow Control**: None

Some printers have DIP switches to configure these. Check your printer manual.

### Testing Serial Communication

```bash
# Install screen
sudo apt-get install screen

# Connect to serial port
screen /dev/ttyUSB0 9600

# Type some text and press Enter
# If printer responds, connection is good!

# Exit screen: Ctrl+A then K
```

## Configuration Summary

### For Your Setup (USB-to-TTL)

**Python Configuration** (already set in scripts):
```python
PRINTER_TYPE = "serial"
SERIAL_PORT = "/dev/ttyUSB0"
SERIAL_BAUDRATE = 9600
```

**System Configuration**:
```bash
# 1. User permissions
sudo usermod -a -G dialout pi

# 2. Port permissions
sudo chmod 666 /dev/ttyUSB0

# 3. Persistent permissions
echo 'KERNEL=="ttyUSB[0-9]*", MODE="0666"' | sudo tee /etc/udev/rules.d/99-usb-serial.rules
sudo udevadm control --reload-rules
```

## Persistent Port Name (Optional)

USB devices can change ports (ttyUSB0 → ttyUSB1). Create a persistent name:

```bash
# Find adapter's serial number
udevadm info -a -n /dev/ttyUSB0 | grep serial

# Create udev rule
sudo nano /etc/udev/rules.d/99-usb-serial.rules
```

Add:
```
SUBSYSTEM=="tty", ATTRS{idVendor}=="1a86", ATTRS{idProduct}=="7523", SYMLINK+="thermal-printer"
```

Then use `/dev/thermal-printer` instead of `/dev/ttyUSB0`

## Testing Checklist

- [ ] Adapter appears in `lsusb`
- [ ] Serial port exists (`/dev/ttyUSB0`)
- [ ] User has dialout/tty permissions
- [ ] Python serial library installed (`pip3 install pyserial`)
- [ ] Correct baud rate configured
- [ ] Wiring is correct (TX↔RX crossed)
- [ ] GND connected
- [ ] Test with `echo "test" > /dev/ttyUSB0`
- [ ] Python script connects successfully
- [ ] Test print works

## Production Setup

Once everything works:

```bash
# Install as service
sudo cp deployment/receipt-printer-client.service /etc/systemd/system/
sudo systemctl enable receipt-printer-client
sudo systemctl start receipt-printer-client

# Check status
sudo systemctl status receipt-printer-client

# View logs
sudo journalctl -u receipt-printer-client -f
```

## Comparison: USB vs Serial

| Aspect | Direct USB | USB-to-TTL (Serial) |
|--------|-----------|---------------------|
| Configuration | Vendor/Product IDs | Serial port + baud rate |
| Connection | `Usb(0x04b8, 0x0e15)` | `Serial('/dev/ttyUSB0', 9600)` |
| Reliability | Very High | High |
| Setup Complexity | Medium | Low |
| Wiring | USB cable only | USB + RX/TX/GND wires |
| Compatibility | Direct USB printers | Serial printers |

## Additional Resources

- [pyserial Documentation](https://pyserial.readthedocs.io/)
- [Raspberry Pi Serial Configuration](https://www.raspberrypi.org/documentation/configuration/uart.md)
- [ESC/POS Commands](https://reference.epson-biz.com/modules/ref_escpos/)

## Getting Help

If you're still having issues:

1. **Check adapter is working**:
   ```bash
   lsusb  # Should see your adapter
   ls -l /dev/ttyUSB0  # Should exist
   ```

2. **Test basic communication**:
   ```bash
   echo "test" > /dev/ttyUSB0
   ```

3. **Try different baud rates**: 9600, 19200, 38400, 115200

4. **Check wiring**: TX ↔ RX must be crossed, GND connected

5. **View logs**: Look for specific error messages

Your setup is ready! The scripts are already configured for serial (USB-to-TTL) connection. Just verify your serial port and baud rate, then test it! 🖨️

