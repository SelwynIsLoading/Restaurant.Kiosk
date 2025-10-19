# Arduino Cash Acceptor - Protocol Quick Reference

## Serial Settings
```
Baud Rate: 9600
Data Bits: 8
Parity: None
Stop Bits: 1
Line Ending: \n (newline)
```

## Arduino → Raspberry Pi Messages

### Cash Detection (Primary Functions)
```
BILL:20      ₱20 bill inserted
BILL:50      ₱50 bill inserted
BILL:100     ₱100 bill inserted
BILL:200     ₱200 bill inserted
BILL:500     ₱500 bill inserted
BILL:1000    ₱1000 bill inserted

COIN:1       ₱1 coin inserted
COIN:5       ₱5 coin inserted
COIN:10      ₱10 coin inserted
COIN:20      ₱20 coin inserted
```

### System Messages
```
READY        Arduino started/ready
PONG         Response to PING (health check)
# ...        Comment/debug messages (ignored)
```

### Error Messages
```
ERROR:xxx    Error description
```

## Raspberry Pi → Arduino Commands (Optional)

### Diagnostic Commands
```
PING                 Health check → Arduino responds with PONG
STATUS               Get statistics → Arduino responds with bill/coin counts
RESET                Reset counters to zero
```

### Testing Commands (Without Hardware)
```
TEST:BILL:100        Simulate ₱100 bill insertion
TEST:BILL:500        Simulate ₱500 bill insertion
TEST:COIN:5          Simulate ₱5 coin insertion
TEST:COIN:10         Simulate ₱10 coin insertion
```

## Python Parsing Rules

| Message | Action |
|---------|--------|
| `BILL:xxx` | Parse amount, associate with current order, send to VPS |
| `COIN:xxx` | Parse amount, associate with current order, send to VPS |
| `READY` | Log "Arduino ready" at INFO level |
| `PONG` | Log "Arduino responding" at INFO level |
| `# ...` | Log at DEBUG level, ignore |
| Others | Log at DEBUG level, ignore |

## Configuration Files

### Arduino (arduino_cash_acceptor.ino)
```cpp
const int SERIAL_BAUD = 9600;  // Line 91
```

### Python (cash_reader_config.json)
```json
{
  "arduino_port": "/dev/ttyUSB0",
  "arduino_baud_rate": 9600
}
```

## Testing Examples

### Test in Arduino Serial Monitor
```
Open Serial Monitor (9600 baud)
Send: TEST:BILL:100
Expect: BILL:100

Send: PING
Expect: PONG

Send: STATUS
Expect: # Status: Bills=1 Coins=0
```

### Test in Python
```bash
# Start the script
python3 kiosk_peripherals.py

# Look for Arduino connection:
# [CASH] Connecting to Arduino on /dev/ttyUSB0 at 9600 baud...
# [CASH] Successfully connected to Arduino
# [CASH] Arduino status: READY

# Insert cash or send TEST commands
```

## Message Flow Example

```
1. Customer creates order on kiosk
   → VPS creates payment session

2. Python polls VPS every 5 seconds
   → Detects active payment session for Order #12345
   → Sets current_order = "12345"

3. Customer inserts ₱100 bill
   → Arduino detects pulses
   → Arduino sends: "BILL:100\n"

4. Python receives "BILL:100"
   → Parses amount: 100.0
   → Associates with current order: 12345
   → Creates CashUpdate(order_number="12345", amount_added=100.0)

5. Python POSTs to VPS
   → POST /api/cash-payment/update
   → Body: {"orderNumber": "12345", "amountAdded": 100.0}

6. VPS updates session
   → Returns: {"success": true, "totalInserted": 100.0, ...}

7. Browser polls VPS
   → Shows updated amount to customer
```

## Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| No data received | Check USB connection, verify port, check permissions |
| Wrong baud rate | Both must be 9600 |
| Logs flooded with warnings | Update to latest Python code (handles comments) |
| "Permission denied" error | `sudo usermod -a -G dialout pi && sudo reboot` |
| Arduino not responding | Send PING command, check USB cable |

## Port Finding Commands

```bash
# Linux/Raspberry Pi
ls -l /dev/ttyUSB*
ls -l /dev/ttyACM*
dmesg | grep tty

# Windows
# Check Device Manager → Ports (COM & LPT)

# macOS
ls -l /dev/tty.usb*
```

## Quick Verification

```bash
# 1. Check Arduino is connected
ls -l /dev/ttyUSB0

# 2. Test serial communication
python3 -c "import serial; s = serial.Serial('/dev/ttyUSB0', 9600, timeout=2); print('Connected'); import time; time.sleep(1); print('Received:', s.readline())"

# 3. Start full script
python3 kiosk_peripherals.py

# 4. In another terminal, monitor logs
tail -f kiosk_peripherals.log
```

## Architecture Notes

✅ **Arduino is stateless** - doesn't track orders  
✅ **Python manages order association** - polls VPS for active sessions  
✅ **VPS manages payment state** - tracks amount inserted per order  
✅ **Simple text protocol** - easy to debug with serial monitor  
✅ **Robust error handling** - invalid messages don't crash system  

## Version Compatibility

| Component | Version | Status |
|-----------|---------|--------|
| Arduino Code | v2.0 | ✅ Current |
| Python Script (Unified) | v1.0 | ✅ Current |
| Python Script (Legacy) | v1.x | ⚠️ Deprecated |
| Protocol | Text-based | ✅ Stable |

---

**Last Updated:** October 17, 2025  
**Compatible With:** `kiosk_peripherals.py` v1.0+  
**Arduino Firmware:** `arduino_cash_acceptor.ino` v2.0

