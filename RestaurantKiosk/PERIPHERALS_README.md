# Kiosk Peripherals - Unified Manager

## Overview

This directory contains the **unified peripherals manager** for the Restaurant Kiosk system. It combines both the Arduino cash acceptor reader and the receipt printer client into a single, efficient Python script.

## Files

### Core Script
- **`kiosk_peripherals.py`** - Main unified script (replaces `arduino_cash_reader.py` + `receipt_printer_client.py`)

### Configuration
- **`cash_reader_config.json`** - Configuration file (supports both modules)
- **`kiosk_peripherals_config.example.json`** - Example configuration template

### Deployment
- **`deployment/kiosk-peripherals.service`** - Systemd service file
- **`deployment/install-unified-peripherals.sh`** - Automated installation script

### Documentation
- **`PERIPHERALS_UNIFIED_SETUP.md`** - Complete setup guide
- **`PERIPHERALS_MIGRATION_GUIDE.md`** - Migration instructions from separate scripts
- **`PERIPHERALS_README.md`** - This file

### Legacy Scripts (Optional Backup)
- `arduino_cash_reader.py` - Original cash reader (can be removed)
- `receipt_printer_client.py` - Original printer client (can be removed)

## Quick Start

### New Installation

```bash
# 1. Copy files to Raspberry Pi
scp kiosk_peripherals.py pi@raspberrypi:~/kiosk/
scp deployment/kiosk-peripherals.service pi@raspberrypi:~/kiosk/
scp deployment/install-unified-peripherals.sh pi@raspberrypi:~/kiosk/

# 2. SSH to Pi
ssh pi@raspberrypi

# 3. Run installer
cd ~/kiosk
chmod +x install-unified-peripherals.sh
./install-unified-peripherals.sh
```

### Existing Installation (Migration)

See `PERIPHERALS_MIGRATION_GUIDE.md` for detailed migration steps.

## Features

### ✓ Unified Management
- Single script handles both cash reader and receipt printer
- One systemd service to manage
- One configuration file

### ✓ Efficient Resource Usage
- Shared HTTP session for better connection pooling
- Runs in single process with two threads
- Lower memory footprint than separate processes

### ✓ Flexible Configuration
- Enable/disable modules independently
- Adjust polling intervals per module
- Support for multiple printer types (USB, Serial, Network)

### ✓ Robust Error Handling
- Auto-reconnection for serial ports
- Retry logic for API calls
- Graceful degradation (one module failure doesn't affect the other)

### ✓ Comprehensive Logging
- Module-prefixed logs (`[CASH]`, `[PRINTER]`)
- Both file and console output
- Systemd journal integration

## Architecture

```
┌────────────────────────────────────────────┐
│      Raspberry Pi                          │
│                                            │
│  ┌──────────────────────────────────────┐ │
│  │   kiosk_peripherals.py               │ │
│  │                                      │ │
│  │  ┌──────────┐      ┌──────────┐    │ │
│  │  │  Cash    │      │ Printer  │    │ │
│  │  │  Reader  │      │ Client   │    │ │
│  │  │  Thread  │      │ Thread   │    │ │
│  │  └────┬─────┘      └────┬─────┘    │ │
│  └───────┼─────────────────┼──────────┘ │
│          │                 │            │
│     ┌────▼──┐         ┌───▼────┐       │
│     │Arduino│         │Thermal │       │
│     │  Cash │         │Printer │       │
│     │Accept.│         │        │       │
│     └───────┘         └────────┘       │
│          ▲                 ▲            │
└──────────┼─────────────────┼────────────┘
           │                 │
           └────────┬────────┘
                    │ HTTPS/Polling
                    ▼
         ┌──────────────────────┐
         │     VPS Server       │
         │  bochogs-kiosk.store │
         │                      │
         │  - Payment Sessions  │
         │  - Print Queue       │
         └──────────────────────┘
```

## Configuration Example

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

## Common Commands

```bash
# Check service status
sudo systemctl status kiosk-peripherals.service

# View live logs
sudo journalctl -u kiosk-peripherals.service -f

# Restart service
sudo systemctl restart kiosk-peripherals.service

# Stop service
sudo systemctl stop kiosk-peripherals.service

# View log file
tail -f ~/kiosk/kiosk_peripherals.log

# Test manually
cd ~/kiosk
python3 kiosk_peripherals.py
```

## Testing Individual Modules

Edit `cash_reader_config.json`:

```json
{
  "enable_cash_reader": true,
  "enable_printer": false  // Test cash reader only
}
```

Or:

```json
{
  "enable_cash_reader": false,  // Test printer only
  "enable_printer": true
}
```

Then restart:
```bash
sudo systemctl restart kiosk-peripherals.service
```

## Advantages Over Separate Scripts

| Aspect | Separate Scripts | Unified Script |
|--------|-----------------|----------------|
| **Processes** | 2 processes | 1 process (2 threads) |
| **Services** | 2 systemd services | 1 systemd service |
| **Config Files** | 1-2 files | 1 file |
| **Memory Usage** | Higher (2 Python processes) | Lower (1 Python process) |
| **HTTP Sessions** | 2 separate sessions | 1 shared session |
| **Logging** | 2 separate logs | 1 unified log |
| **Deployment** | Deploy 2 scripts + 2 services | Deploy 1 script + 1 service |
| **Monitoring** | Monitor 2 services | Monitor 1 service |
| **Updates** | Update 2 scripts | Update 1 script |
| **Module Control** | Start/stop separate services | Enable/disable in config |

## Hardware Requirements

- Raspberry Pi (any model with USB ports)
- Arduino with cash/coin acceptor
- Thermal receipt printer (58mm or 80mm)
- USB cables or USB-to-Serial adapters

## Dependencies

**Recommended (System-wide via apt):**
```bash
sudo apt update
sudo apt install -y python3-serial python3-requests
sudo pip3 install python-escpos --break-system-packages
```

**Alternative (User-level via pip):**
```bash
pip3 install pyserial requests python-escpos
```

**See:** `PYTHON_DEPENDENCIES_GUIDE.md` for detailed comparison of installation methods

## Troubleshooting

### Service won't start
```bash
# Test manually to see errors
python3 ~/kiosk/kiosk_peripherals.py
```

### Serial port not found
```bash
# List USB devices
ls -l /dev/ttyUSB*

# Check permissions
sudo usermod -a -G dialout pi
sudo reboot
```

### Can't connect to VPS
```bash
# Test connection
curl https://bochogs-kiosk.store/api/cash-payment/active-sessions

# Check configuration
cat ~/kiosk/cash_reader_config.json
```

### High CPU usage
Increase polling intervals in config:
```json
{
  "cash_poll_interval": 10,
  "printer_poll_interval": 5
}
```

## Documentation

- **Setup**: See `PERIPHERALS_UNIFIED_SETUP.md`
- **Migration**: See `PERIPHERALS_MIGRATION_GUIDE.md`
- **Arduino Protocol**: See `ARDUINO_PROTOCOL.md`
- **Cash Payment System**: See `CASH_PAYMENT_README.md`
- **Receipt Printing**: See `RECEIPT_PRINTER_SETUP.md`

## Support

For issues or questions:
1. Check logs first: `sudo journalctl -u kiosk-peripherals.service -f`
2. Test individual modules (disable one in config)
3. Review VPS API connectivity
4. Check serial port connections and permissions

## License

Part of the Restaurant Kiosk project.

