# Before & After: Separate vs. Unified Peripherals

This document provides a visual comparison of the old separate scripts approach vs. the new unified approach.

## File Structure Comparison

### BEFORE (Separate Scripts)
```
RestaurantKiosk/
├── arduino_cash_reader.py          (470 lines)
│   └── cash_reader_config.json
├── receipt_printer_client.py       (280 lines)
│   └── (optional separate config)
├── requirements.txt
└── deployment/
    ├── receipt-printer-client.service
    └── arduino-cash-reader.service   (if using systemd)
```

### AFTER (Unified Script)
```
RestaurantKiosk/
├── kiosk_peripherals.py            (730 lines - all functionality)
│   └── cash_reader_config.json     (unified config)
├── requirements.txt                (unchanged)
├── deployment/
│   ├── kiosk-peripherals.service   (single service)
│   └── install-unified-peripherals.sh
└── docs/
    ├── PERIPHERALS_README.md
    ├── PERIPHERALS_UNIFIED_SETUP.md
    └── PERIPHERALS_MIGRATION_GUIDE.md
```

## Process Architecture

### BEFORE
```
┌─────────────────────────────────┐
│     Raspberry Pi                │
│                                 │
│  ┌───────────────────────────┐ │
│  │  Process 1                │ │
│  │  arduino_cash_reader.py   │ │
│  │                           │ │
│  │  - HTTP Session           │ │
│  │  - Logging                │ │
│  │  - Config Parsing         │ │
│  │  - Error Handling         │ │
│  └─────────┬─────────────────┘ │
│            │                   │
│      ┌─────▼─────┐             │
│      │  Arduino  │             │
│      └───────────┘             │
│                                 │
│  ┌───────────────────────────┐ │
│  │  Process 2                │ │
│  │  receipt_printer_client   │ │
│  │                           │ │
│  │  - HTTP Session           │ │  ← Duplicate code
│  │  - Logging                │ │  ← Duplicate resources
│  │  - Config Parsing         │ │  ← Duplicate overhead
│  │  - Error Handling         │ │
│  └─────────┬─────────────────┘ │
│            │                   │
│      ┌─────▼─────┐             │
│      │  Printer  │             │
│      └───────────┘             │
└─────────────────────────────────┘

2 Processes × Python Overhead = Higher Memory
2 HTTP Sessions = More Connections
2 Services to Monitor
```

### AFTER
```
┌─────────────────────────────────┐
│     Raspberry Pi                │
│                                 │
│  ┌───────────────────────────┐ │
│  │  Single Process           │ │
│  │  kiosk_peripherals.py     │ │
│  │                           │ │
│  │  Shared:                  │ │
│  │  - HTTP Session ──────────┼─┐
│  │  - Logging                │ │
│  │  - Config Parsing         │ │
│  │  - Error Handling         │ │
│  │                           │ │
│  │  ┌────────┐  ┌─────────┐ │ │
│  │  │Thread 1│  │Thread 2 │ │ │
│  │  │ Cash   │  │ Printer │ │ │
│  │  │ Reader │  │ Client  │ │ │
│  │  └───┬────┘  └────┬────┘ │ │
│  └──────┼────────────┼──────┘ │
│         │            │        │
│    ┌────▼──┐    ┌───▼────┐   │
│    │Arduino│    │Printer │   │
│    └───────┘    └────────┘   │
└─────────────────────────────────┘

1 Process = Lower Memory
1 Shared HTTP Session = Efficient
1 Service to Monitor
```

## Service Management Comparison

### BEFORE - Managing Two Services

```bash
# Start both services
sudo systemctl start receipt-printer-client.service
sudo systemctl start arduino-cash-reader.service

# Check status of both
sudo systemctl status receipt-printer-client.service
sudo systemctl status arduino-cash-reader.service

# View logs from both
sudo journalctl -u receipt-printer-client.service -f
sudo journalctl -u arduino-cash-reader.service -f    # In another terminal

# Restart both
sudo systemctl restart receipt-printer-client.service
sudo systemctl restart arduino-cash-reader.service

# Enable at boot
sudo systemctl enable receipt-printer-client.service
sudo systemctl enable arduino-cash-reader.service
```

### AFTER - Managing One Service

```bash
# Start service
sudo systemctl start kiosk-peripherals.service

# Check status
sudo systemctl status kiosk-peripherals.service

# View logs (both modules in one stream)
sudo journalctl -u kiosk-peripherals.service -f

# Restart
sudo systemctl restart kiosk-peripherals.service

# Enable at boot
sudo systemctl enable kiosk-peripherals.service
```

**Commands reduced by 50%!**

## Configuration Comparison

### BEFORE - Separate Configurations

**cash_reader_config.json:**
```json
{
  "vps_api_url": "https://bochogs-kiosk.store",
  "arduino_port": "/dev/ttyUSB0",
  "baud_rate": 9600,
  "reconnect_delay_seconds": 5,
  "api_key": "your-api-key"
}
```

**printer_config.json (or hardcoded):**
```python
VPS_API_URL = "https://bochogs-kiosk.store"  # Hardcoded!
PRINTER_TYPE = "serial"                       # Hardcoded!
SERIAL_PORT = "/dev/ttyUSB0"                  # Hardcoded!
POLL_INTERVAL = 2                             # Hardcoded!
```

**Issues:**
- VPS URL duplicated
- Some settings hardcoded
- API key might be in different places
- Need to update two files for VPS changes

### AFTER - Unified Configuration

**cash_reader_config.json:**
```json
{
  "vps_api_url": "https://bochogs-kiosk.store",
  "api_key": "your-api-key",
  
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

**Benefits:**
- Single source of truth
- All settings configurable (no hardcoded values)
- Can enable/disable modules
- Change VPS URL once, affects both
- Easy to version control

## Log Output Comparison

### BEFORE - Separate Logs

**Terminal 1: cash_reader.log**
```
2025-10-17 10:30:15 - INFO - Polling VPS for active sessions...
2025-10-17 10:30:15 - INFO - New payment session detected: ORD-12345
2025-10-17 10:30:20 - INFO - Bill inserted: ₱100 for order ORD-12345
2025-10-17 10:30:25 - INFO - Cash update successful
```

**Terminal 2: receipt_printer_client.log**
```
2025-10-17 10:30:30 - INFO - Checking for print jobs...
2025-10-17 10:30:45 - INFO - Received print job: job-67890
2025-10-17 10:30:46 - INFO - Receipt printed successfully
```

**Problem:** Need to correlate logs across two files to see complete order flow!

### AFTER - Unified Logs

**Single File: kiosk_peripherals.log**
```
2025-10-17 10:30:15 - [CASH] Polling VPS for active sessions...
2025-10-17 10:30:15 - [CASH] New payment session detected: ORD-12345
2025-10-17 10:30:20 - [CASH] Bill inserted: ₱100 for order ORD-12345
2025-10-17 10:30:25 - [CASH] Cash update successful
2025-10-17 10:30:30 - [PRINTER] Checking for print jobs...
2025-10-17 10:30:45 - [PRINTER] Received print job for ORD-12345
2025-10-17 10:30:46 - [PRINTER] Receipt printed successfully for order: ORD-12345
```

**Benefit:** See complete order flow in chronological order with module prefixes!

## Deployment Comparison

### BEFORE - Manual Multi-Step Deployment

```bash
# Step 1: Deploy cash reader
scp arduino_cash_reader.py pi@raspberrypi:~/kiosk/
scp cash_reader_config.json pi@raspberrypi:~/kiosk/
scp arduino-cash-reader.service pi@raspberrypi:~/

# Step 2: Deploy printer
scp receipt_printer_client.py pi@raspberrypi:~/kiosk/
scp receipt-printer-client.service pi@raspberrypi:~/

# Step 3: SSH and set up
ssh pi@raspberrypi

# Step 4: Install dependencies
pip3 install pyserial requests python-escpos

# Step 5: Set up first service
sudo mv ~/arduino-cash-reader.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable arduino-cash-reader.service
sudo systemctl start arduino-cash-reader.service

# Step 6: Set up second service
sudo mv ~/receipt-printer-client.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable receipt-printer-client.service
sudo systemctl start receipt-printer-client.service

# Step 7: Verify both
sudo systemctl status arduino-cash-reader.service
sudo systemctl status receipt-printer-client.service
```

**~15 commands, error-prone**

### AFTER - Automated Single-Step Deployment

```bash
# Step 1: Copy files
scp kiosk_peripherals.py deployment/*.sh deployment/*.service pi@raspberrypi:~/kiosk/

# Step 2: Run installer
ssh pi@raspberrypi
cd ~/kiosk
chmod +x install-unified-peripherals.sh
./install-unified-peripherals.sh
```

**The script automatically:**
1. ✓ Stops old services
2. ✓ Backs up old scripts
3. ✓ Installs dependencies
4. ✓ Sets up service
5. ✓ Starts and enables service
6. ✓ Verifies operation

**~4 commands, automated and safe**

## Testing/Debugging Comparison

### BEFORE

**Test cash reader only:**
```bash
sudo systemctl stop receipt-printer-client.service  # Stop printer
python3 arduino_cash_reader.py                      # Test cash reader
sudo systemctl start receipt-printer-client.service # Restart printer
```

**Test printer only:**
```bash
sudo systemctl stop arduino-cash-reader.service     # Stop cash reader
python3 receipt_printer_client.py                   # Test printer
sudo systemctl start arduino-cash-reader.service    # Restart cash reader
```

### AFTER

**Test cash reader only:**
```json
// Edit config
{
  "enable_cash_reader": true,
  "enable_printer": false
}
```
```bash
sudo systemctl restart kiosk-peripherals.service
```

**Test printer only:**
```json
// Edit config
{
  "enable_cash_reader": false,
  "enable_printer": true
}
```
```bash
sudo systemctl restart kiosk-peripherals.service
```

**Or test manually:**
```bash
python3 kiosk_peripherals.py  # Tests both at once
```

## Resource Usage Comparison

| Metric | Before (Separate) | After (Unified) | Improvement |
|--------|------------------|-----------------|-------------|
| **Processes** | 2 | 1 | 50% reduction |
| **Python Interpreters** | 2 | 1 | 50% reduction |
| **Memory (estimate)** | ~80-100 MB | ~40-50 MB | ~50% reduction |
| **HTTP Connections** | 2 pools | 1 shared pool | More efficient |
| **File Descriptors** | 2 sets | 1 shared set | More efficient |
| **systemd Services** | 2 | 1 | 50% reduction |
| **Log Files** | 2 | 1 | 50% reduction |
| **Config Files** | 1-2 | 1 | Simplified |

## Code Maintenance Comparison

### BEFORE - Duplicate Code

**HTTP retry logic duplicated:**
- `arduino_cash_reader.py` lines 188-245 (57 lines)
- `receipt_printer_client.py` similar logic

**Logging setup duplicated:**
- Both scripts have identical logging configuration

**Config loading duplicated:**
- Both scripts need to load configuration

**Total maintenance burden:** Update 2 scripts for common changes

### AFTER - Shared Code

**Single HTTP retry implementation:**
- Used by both modules

**Single logging setup:**
- Shared by both threads

**Single config loader:**
- One function, loaded once

**Total maintenance burden:** Update 1 script

## Summary

| Aspect | Separate Scripts | Unified Script | Winner |
|--------|-----------------|----------------|---------|
| **Number of files** | 2+ | 1 | ✓ Unified |
| **Services to manage** | 2 | 1 | ✓ Unified |
| **Memory usage** | Higher | Lower | ✓ Unified |
| **Configuration** | Split/duplicated | Centralized | ✓ Unified |
| **Logs** | Separate files | One unified log | ✓ Unified |
| **Deployment complexity** | High | Low | ✓ Unified |
| **Testing flexibility** | Service-level | Config-level | ✓ Unified |
| **Code duplication** | High | None | ✓ Unified |
| **Maintenance burden** | 2 scripts | 1 script | ✓ Unified |
| **Resource efficiency** | Lower | Higher | ✓ Unified |

## Conclusion

The unified `kiosk_peripherals.py` script provides:

✅ **Simpler** - One script, one service, one config
✅ **Faster** - Shared resources, efficient pooling
✅ **Cheaper** - Less memory, fewer resources
✅ **Easier** - Automated deployment, unified logs
✅ **Better** - Consistent error handling, flexible testing

**Recommendation:** Migrate to unified script for production deployments.

