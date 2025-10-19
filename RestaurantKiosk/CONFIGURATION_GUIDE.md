# Kiosk Peripherals - Configuration Guide

## Configuration File: `cash_reader_config.json`

This file configures both the Arduino cash reader and thermal receipt printer.

---

## Your Current Configuration

```json
{
  "vps_api_url": "https://bochogs-kiosk.store",
  "api_key": null,
  "environment": "production",
  
  "enable_cash_reader": true,
  "enable_printer": true,
  
  "arduino_port": "/dev/ttyACM0",
  "arduino_baud_rate": 9600,
  "cash_poll_interval": 5,
  
  "printer_type": "serial",
  "printer_serial_port": "/dev/ttyUSB0",
  "printer_serial_baudrate": 9600,
  "printer_usb_vendor_id": "0x04b8",
  "printer_usb_product_id": "0x0e15",
  
  "printer_poll_interval": 2,
  "reconnect_delay_seconds": 5,
  "connection_timeout_seconds": 10,
  "retry_attempts": 3
}
```

---

## Configuration Options

### General Settings

| Option | Value | Description |
|--------|-------|-------------|
| `vps_api_url` | `https://bochogs-kiosk.store` | Your VPS server URL |
| `api_key` | `null` | Optional API key for security (set this in production!) |
| `environment` | `production` | Environment name (development/production) |

### Module Control

| Option | Value | Description |
|--------|-------|-------------|
| `enable_cash_reader` | `true` | Enable/disable cash reader module |
| `enable_printer` | `true` | Enable/disable receipt printer module |

**Use Cases:**
- Test cash reader only: Set `"enable_printer": false`
- Test printer only: Set `"enable_cash_reader": false`

### Arduino Cash Reader Settings

| Option | Value | Description |
|--------|-------|-------------|
| `arduino_port` | `/dev/ttyACM0` | Serial port for Arduino (USB connection) |
| `arduino_baud_rate` | `9600` | Communication speed (must match Arduino) |
| `cash_poll_interval` | `5` | Seconds between polling VPS for payment sessions |

**Common Arduino Port Names:**
- `/dev/ttyACM0` - Arduino Uno (USB native)
- `/dev/ttyUSB0` - Arduino with USB-to-Serial adapter
- Check with: `ls -l /dev/tty*`

### Receipt Printer Settings

| Option | Value | Description |
|--------|-------|-------------|
| `printer_type` | `serial` | Connection type: serial, usb, network, or file |
| `printer_serial_port` | `/dev/ttyUSB0` | Serial port for printer |
| `printer_serial_baudrate` | `9600` | Printer communication speed (SHK24 default) |
| `printer_poll_interval` | `2` | Seconds between polling VPS for print jobs |

**Printer Types:**
- `serial` - USB-to-TTL adapter (most common for SHK24)
- `usb` - Direct USB connection (requires vendor/product ID)
- `network` - Network printer (requires IP address)
- `file` - Debug mode (prints to file instead)

### Advanced Settings

| Option | Value | Description |
|--------|-------|-------------|
| `reconnect_delay_seconds` | `5` | Wait time before reconnecting after error |
| `connection_timeout_seconds` | `10` | HTTP request timeout |
| `retry_attempts` | `3` | Number of retries for failed API calls |

---

## Finding Your Serial Ports

### List All USB Devices
```bash
# On Raspberry Pi
ls -l /dev/tty*

# More detailed info
dmesg | grep tty

# Check what's connected
lsusb
```

### Identify Arduino Port
```bash
# Unplug Arduino, then run:
ls /dev/tty* > before.txt

# Plug in Arduino, then run:
ls /dev/tty* > after.txt

# Compare to find new port
diff before.txt after.txt
```

**Expected Result:**
- Arduino Uno: `/dev/ttyACM0`
- Arduino with USB adapter: `/dev/ttyUSB0`

### Identify Printer Port
```bash
# Same process - unplug, run ls, plug in, run ls, compare
# Printer usually shows as: /dev/ttyUSB0 or /dev/ttyUSB1
```

### If Both Use Serial
If Arduino is on `/dev/ttyACM0` and Printer is on `/dev/ttyUSB0`, that's perfect!
If both want the same port, you'll need to identify them by USB ID.

---

## Security: Setting API Key

### Why Use an API Key?

Without an API key, anyone who can reach your VPS can send cash updates or print jobs. This is OK for testing but not recommended for production.

### Step 1: Generate API Key

```bash
# Generate a secure random key (32 characters)
openssl rand -base64 32

# Example output:
# 8N9bXvYcR4mK7pL2qWsT3uZaE5fH6jD1
```

### Step 2: Configure VPS (Blazor)

Edit `appsettings.json` on VPS:
```json
{
  "CashPayment": {
    "ApiKey": "8N9bXvYcR4mK7pL2qWsT3uZaE5fH6jD1"
  }
}
```

Restart VPS service:
```bash
sudo systemctl restart restaurant-kiosk
```

### Step 3: Configure Raspberry Pi

Edit `cash_reader_config.json`:
```json
{
  "api_key": "8N9bXvYcR4mK7pL2qWsT3uZaE5fH6jD1"
}
```

Restart kiosk peripherals:
```bash
sudo systemctl restart kiosk-peripherals.service
```

### Verify API Key Works

```bash
# Without API key (should fail)
curl -X GET https://bochogs-kiosk.store/api/cash-payment/active-sessions

# With API key (should succeed)
curl -X GET https://bochogs-kiosk.store/api/cash-payment/active-sessions \
  -H "X-API-Key: 8N9bXvYcR4mK7pL2qWsT3uZaE5fH6jD1"
```

---

## Testing Your Configuration

### 1. Validate JSON Format

```bash
# Check if JSON is valid
python3 -m json.tool cash_reader_config.json
```

If valid, it will print the formatted JSON. If invalid, it will show an error.

### 2. Test Connection to VPS

```bash
# Test if VPS is reachable
curl -I https://bochogs-kiosk.store

# Test cash payment API
curl https://bochogs-kiosk.store/api/cash-payment/active-sessions
```

### 3. Test Arduino Connection

```bash
# Check if Arduino is connected
ls -l /dev/ttyACM0

# Test reading from Arduino
cat /dev/ttyACM0
# (Should show data when Arduino sends it)

# Press Ctrl+C to stop
```

### 4. Test Printer Connection

```bash
# Check if printer is connected
ls -l /dev/ttyUSB0

# Test printer (careful - this will print!)
echo "Test" > /dev/ttyUSB0
```

---

## Configuration Examples

### Example 1: Development (Local Testing)

```json
{
  "vps_api_url": "http://localhost:5000",
  "environment": "development",
  "enable_cash_reader": true,
  "enable_printer": false,
  "arduino_port": "/dev/ttyACM0",
  "cash_poll_interval": 2
}
```

**Use Case:** Testing cash reader on local development machine

### Example 2: Production (Both Modules)

```json
{
  "vps_api_url": "https://bochogs-kiosk.store",
  "api_key": "your-secure-key",
  "environment": "production",
  "enable_cash_reader": true,
  "enable_printer": true,
  "arduino_port": "/dev/ttyACM0",
  "printer_serial_port": "/dev/ttyUSB0"
}
```

**Use Case:** Full production deployment

### Example 3: Printer Only

```json
{
  "vps_api_url": "https://bochogs-kiosk.store",
  "environment": "production",
  "enable_cash_reader": false,
  "enable_printer": true,
  "printer_type": "serial",
  "printer_serial_port": "/dev/ttyUSB0"
}
```

**Use Case:** Separate Raspberry Pi for printing only

---

## Troubleshooting Configuration

### Issue: "Config file not found"

**Solution:**
```bash
# Make sure file is in same directory as script
cd ~/kiosk
ls -la cash_reader_config.json

# If missing, create it
nano cash_reader_config.json
# (paste configuration and save)
```

### Issue: "Invalid JSON"

**Solution:**
```bash
# Validate JSON
python3 -m json.tool cash_reader_config.json

# Common mistakes:
# - Missing comma between items
# - Trailing comma after last item
# - Missing quotes around strings
# - Using single quotes instead of double quotes
```

### Issue: "Permission denied" on serial port

**Solution:**
```bash
# Add user to dialout group
sudo usermod -a -G dialout $USER

# Or for pi user specifically
sudo usermod -a -G dialout pi

# Logout and login again, or reboot
sudo reboot
```

### Issue: Can't find serial port

**Solution:**
```bash
# List all USB devices
lsusb

# List all serial ports
ls -l /dev/tty*

# Check kernel messages
dmesg | tail -20

# Install USB utils if needed
sudo apt install usbutils
```

---

## Best Practices

### ✅ DO:
- Use HTTPS for VPS URL in production
- Set an API key in production
- Test configuration before deploying
- Keep backup of working configuration
- Document any custom settings

### ❌ DON'T:
- Use HTTP in production (use HTTPS)
- Share API keys in public repositories
- Use same API key for multiple installations
- Forget to restart service after config changes
- Edit config while service is running

---

## Configuration Checklist

Before running in production:

- [ ] VPS URL is correct and uses HTTPS
- [ ] API key is set (both VPS and Raspberry Pi)
- [ ] Arduino port is correct (`/dev/ttyACM0`)
- [ ] Printer port is correct (`/dev/ttyUSB0`)
- [ ] Both modules are enabled (or disabled as intended)
- [ ] JSON is valid (test with `python3 -m json.tool`)
- [ ] Serial port permissions are set (dialout group)
- [ ] VPS is reachable (`curl -I https://bochogs-kiosk.store`)
- [ ] Configuration file is in script directory

---

## Quick Reference

**File Location:** `~/kiosk/cash_reader_config.json`

**Validate JSON:**
```bash
python3 -m json.tool cash_reader_config.json
```

**Find Serial Ports:**
```bash
ls -l /dev/tty*
```

**Test VPS Connection:**
```bash
curl -I https://bochogs-kiosk.store
```

**Restart Service:**
```bash
sudo systemctl restart kiosk-peripherals.service
```

**Check Logs:**
```bash
./check_kiosk_logs.sh
```

---

## Support

If you're having configuration issues:

1. Validate JSON format
2. Check serial port permissions
3. Test VPS connectivity
4. Check logs for error messages
5. Verify serial ports with `ls -l /dev/tty*`

**See Also:**
- `PERIPHERALS_UNIFIED_SETUP.md` - Complete setup guide
- `LOG_MONITORING_GUIDE.md` - How to check logs
- `PYTHON_DEPENDENCIES_GUIDE.md` - Dependency installation

