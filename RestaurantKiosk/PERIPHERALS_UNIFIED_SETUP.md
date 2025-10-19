# Unified Peripherals Manager

This document explains the unified `kiosk_peripherals.py` script that combines both the Arduino cash reader and receipt printer functionality into a single process.

## Overview

The unified script (`kiosk_peripherals.py`) replaces:
- `arduino_cash_reader.py` - Arduino cash acceptor reader
- `receipt_printer_client.py` - Receipt printer client

## Benefits

### 1. **Simplified Deployment**
- Single Python script to deploy instead of two
- Single configuration file for all peripherals
- Single systemd service to manage

### 2. **Shared Resources**
- Shared HTTP session (better connection pooling)
- Shared logging configuration
- Shared VPS API configuration

### 3. **Easier Maintenance**
- One script to update instead of two
- Consistent error handling and logging patterns
- Single point of configuration

### 4. **Flexible Module Control**
- Enable/disable cash reader independently
- Enable/disable printer independently
- Useful for testing or partial deployments

### 5. **Better Resource Management**
- Both modules run in the same process
- Efficient threading model
- Lower memory footprint

## Configuration

### Configuration File

Create `cash_reader_config.json` (or use the existing one) with unified settings:

```json
{
  "vps_api_url": "https://bochogs-kiosk.store",
  "api_key": "your-api-key-here",
  "environment": "production",
  
  "enable_cash_reader": true,
  "enable_printer": true,
  
  "arduino_port": "/dev/ttyUSB0",
  "arduino_baud_rate": 9600,
  "cash_poll_interval": 5,
  
  "printer_type": "serial",
  "printer_serial_port": "/dev/ttyUSB1",
  "printer_serial_baudrate": 9600,
  "printer_poll_interval": 2,
  
  "reconnect_delay_seconds": 5,
  "connection_timeout_seconds": 10,
  "retry_attempts": 3
}
```

### Configuration Options

| Option | Description | Default |
|--------|-------------|---------|
| `vps_api_url` | VPS API endpoint | `http://localhost:5000` |
| `api_key` | Optional API key for authentication | `null` |
| `environment` | Environment name (development/production) | `development` |
| `enable_cash_reader` | Enable Arduino cash reader module | `true` |
| `enable_printer` | Enable receipt printer module | `true` |
| `arduino_port` | Serial port for Arduino | `/dev/ttyUSB0` |
| `arduino_baud_rate` | Arduino baud rate | `9600` |
| `cash_poll_interval` | Seconds between polling for payment sessions | `5` |
| `printer_type` | Printer type (serial, usb, file) | `serial` |
| `printer_serial_port` | Serial port for printer | `/dev/ttyUSB0` |
| `printer_serial_baudrate` | Printer baud rate | `9600` |
| `printer_poll_interval` | Seconds between polling for print jobs | `2` |

### Important Notes

1. **Different Serial Ports**: If both Arduino and printer use serial/USB connections, ensure they're on different ports (e.g., `/dev/ttyUSB0` and `/dev/ttyUSB1`)

2. **Selective Module Enabling**: You can disable modules you don't need:
   - Testing cash reader only: Set `"enable_printer": false`
   - Testing printer only: Set `"enable_cash_reader": false`

## Installation

### 1. Install Dependencies

**Option A: Using apt (Recommended for Raspberry Pi - Global Installation)**
```bash
# Install Python packages via apt (system-wide)
sudo apt update
sudo apt install -y python3-serial python3-requests

# Install python-escpos (not available in apt, use pip with sudo)
sudo pip3 install python-escpos --break-system-packages

# Or install to system Python without breaking system packages warning
sudo pip3 install python-escpos
```

**Option B: Using pip (Alternative - User Installation)**
```bash
# Install Python packages via pip (user-level)
pip3 install pyserial requests python-escpos

# Or use requirements.txt
pip3 install -r requirements.txt
```

**Why use apt?**
- ✅ System-wide installation (works for systemd service)
- ✅ Managed by system package manager
- ✅ Automatic updates via `apt upgrade`
- ✅ No virtual environment needed

**Package Mapping:**
- `pyserial` → `python3-serial`
- `requests` → `python3-requests`
- `python-escpos` → Not in apt, use pip with `--break-system-packages` flag (Python 3.11+)

### 2. Copy Files to Raspberry Pi

```bash
# From your development machine
scp kiosk_peripherals.py pi@raspberrypi:~/kiosk/
scp cash_reader_config.json pi@raspberrypi:~/kiosk/
scp deployment/kiosk-peripherals.service pi@raspberrypi:~/
```

### 3. Set Up Systemd Service

```bash
# On Raspberry Pi
sudo mv ~/kiosk-peripherals.service /etc/systemd/system/
sudo chmod 644 /etc/systemd/system/kiosk-peripherals.service
sudo systemctl daemon-reload
sudo systemctl enable kiosk-peripherals.service
sudo systemctl start kiosk-peripherals.service
```

### 4. Verify Operation

```bash
# Check service status
sudo systemctl status kiosk-peripherals.service

# View logs
sudo journalctl -u kiosk-peripherals.service -f

# Or view the log file
tail -f /var/log/kiosk-peripherals.log
```

## Manual Testing

### Test Locally

```bash
cd ~/kiosk
python3 kiosk_peripherals.py
```

### Test Individual Modules

Edit `cash_reader_config.json` to enable only the module you want to test:

```json
{
  "enable_cash_reader": true,
  "enable_printer": false
}
```

## Logging

The unified script creates a single log file: `kiosk_peripherals.log`

Log entries are prefixed with module identifiers:
- `[CASH]` - Cash reader module
- `[PRINTER]` - Printer module

Example log output:
```
2025-10-17 10:30:15 - [CASH] New payment session detected: ORD-12345
2025-10-17 10:30:20 - [CASH] Bill inserted: ₱100 for order ORD-12345
2025-10-17 10:30:45 - [PRINTER] Received print job: job-67890
2025-10-17 10:30:46 - [PRINTER] Receipt printed successfully for order: ORD-12345
```

## Troubleshooting

### Both modules fail to start

**Issue**: Neither cash reader nor printer starts
**Solution**: Check VPS connectivity:
```bash
curl https://bochogs-kiosk.store/api/cash-payment/active-sessions
```

### Serial port conflicts

**Issue**: "Port already in use" error
**Solution**: Ensure different serial ports for Arduino and printer:
```bash
# List connected USB devices
ls -l /dev/ttyUSB*

# Update config with correct ports
```

### One module crashes

**Benefit of unified approach**: If one module crashes, the other continues to run. The crashed module will be logged and the service will restart both.

### High CPU usage

**Issue**: CPU usage is high
**Solution**: Check polling intervals - increase them if needed:
```json
{
  "cash_poll_interval": 10,
  "printer_poll_interval": 5
}
```

## Migration from Separate Scripts

If you're currently using the separate scripts:

### 1. Stop Old Services

```bash
sudo systemctl stop receipt-printer-client.service
sudo systemctl stop arduino-cash-reader.service  # if running as service
sudo systemctl disable receipt-printer-client.service
sudo systemctl disable arduino-cash-reader.service
```

### 2. Update Configuration

Merge your existing configurations:
- `cash_reader_config.json` (cash reader settings)
- Receipt printer settings (if separate)

Into the unified `cash_reader_config.json`

### 3. Install Unified Service

Follow the installation steps above.

### 4. Cleanup (Optional)

```bash
# Remove old service files
sudo rm /etc/systemd/system/receipt-printer-client.service
sudo rm /etc/systemd/system/arduino-cash-reader.service
sudo systemctl daemon-reload

# Keep old scripts as backup or remove them
# mv arduino_cash_reader.py arduino_cash_reader.py.backup
# mv receipt_printer_client.py receipt_printer_client.py.backup
```

## Architecture

```
┌─────────────────────────────────────────────┐
│         kiosk_peripherals.py                │
│                                             │
│  ┌─────────────────┐  ┌─────────────────┐  │
│  │  Cash Reader    │  │ Printer Client  │  │
│  │  Thread         │  │ Thread          │  │
│  │                 │  │                 │  │
│  │  ↓ Arduino      │  │  ↓ Printer     │  │
│  │  ↑ VPS API      │  │  ↑ VPS API     │  │
│  └─────────────────┘  └─────────────────┘  │
│                                             │
│         Shared: Config, Logging,            │
│         HTTP Session, Error Handling        │
└─────────────────────────────────────────────┘
         ↕                        ↕
    [Arduino]              [Thermal Printer]
```

## Performance Considerations

- **Threading**: Both modules run in separate threads, so they don't block each other
- **Polling Intervals**: Adjust based on your needs (lower = more responsive, higher = less network traffic)
- **Connection Pooling**: Shared HTTP session improves connection reuse
- **Memory**: Single process is more memory-efficient than two separate processes

## Support

For issues or questions:
1. Check logs: `tail -f /var/log/kiosk-peripherals.log`
2. Check service status: `sudo systemctl status kiosk-peripherals.service`
3. Test individual modules by disabling one in config
4. Review VPS API connectivity

