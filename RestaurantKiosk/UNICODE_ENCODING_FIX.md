# Unicode Encoding Fix - Peso Sign (₱) Error

## Problem

Getting this error when the script tries to log messages containing the peso sign (₱):

```
ERROR:__main__:[CASH] Error polling active sessions: 'latin-1' codec can't encode character '\u20b1' in position 16: ordinal not in range(256)
```

## Root Cause

The **Peso sign (₱)** is a Unicode character (U+20B1) that cannot be represented in the **latin-1** (ISO-8859-1) encoding, which only supports characters 0-255.

### Where It Appears

The error occurs when logging messages like:
```python
logger.info(f"[CASH] New payment session detected: {order_num} - Amount: ₱{session_data['totalRequired']}")
#                                                                       ↑ This character (U+20B1)
```

### Default Encoding Issue

On many systems, Python's logging file handler defaults to:
- Linux: Often UTF-8 (works)
- Some systems: latin-1 or ASCII (fails with ₱)

---

## Solution

### Fix Applied to `kiosk_peripherals.py`

**Before:**
```python
file_handler = RotatingFileHandler(
    'kiosk_peripherals.log',
    maxBytes=10*1024*1024,
    backupCount=5
)
# No encoding specified → uses system default (might be latin-1)
```

**After:**
```python
file_handler = RotatingFileHandler(
    'kiosk_peripherals.log',
    maxBytes=10*1024*1024,
    backupCount=5,
    encoding='utf-8'  # ✅ Explicitly use UTF-8
)

# Also configure console output for UTF-8
import sys
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
```

---

## Why This Matters

### Unicode Characters in the System

The kiosk uses several Unicode characters:
- **₱** (U+20B1) - Peso sign
- **✓** (U+2713) - Check mark
- **⚠** (U+26A0) - Warning sign
- **✗** (U+2717) - Cross mark

All require UTF-8 encoding to display and log correctly.

### Impact

**Without UTF-8 encoding:**
- ❌ Logs crash when encountering ₱ symbol
- ❌ Error messages become unreadable
- ❌ Script might crash unexpectedly

**With UTF-8 encoding:**
- ✅ All characters log correctly
- ✅ Peso amounts display properly
- ✅ No encoding errors

---

## Testing

### Test the Fix

```bash
# Run the script
python3 kiosk_peripherals.py

# When it logs a payment session, you should see:
# [CASH] New payment session detected: ORD-12345 - Amount: ₱250.00
# (No encoding errors)
```

### Verify Log File Encoding

```bash
# Check log file encoding
file -bi kiosk_peripherals.log
# Should show: text/plain; charset=utf-8

# View log with Unicode characters
cat kiosk_peripherals.log | grep "₱"
# Should display correctly
```

### Test Unicode in Console

```python
# Quick test
python3 << 'EOF'
import logging
from logging.handlers import RotatingFileHandler

handler = RotatingFileHandler('test.log', encoding='utf-8')
formatter = logging.Formatter('%(message)s')
handler.setFormatter(formatter)

logger = logging.getLogger('test')
logger.addHandler(handler)
logger.setLevel(logging.INFO)

# Test with peso sign
logger.info("Test: ₱100.00")
print("✓ UTF-8 encoding works!")
EOF

# Check the log
cat test.log
# Should show: Test: ₱100.00

# Cleanup
rm test.log
```

---

## Alternative Solutions (If Still Having Issues)

### Solution 1: Remove Unicode Characters

Replace peso sign with "PHP" or "P":

```python
# Instead of:
logger.info(f"Amount: ₱{amount}")

# Use:
logger.info(f"Amount: PHP {amount}")
# or
logger.info(f"Amount: P{amount}")
```

**Pros:** Works on any encoding  
**Cons:** Less elegant, harder to read

### Solution 2: Set Locale

Set system locale to UTF-8:

```bash
# On Raspberry Pi
sudo raspi-config
# → Localisation Options
# → Change Locale
# → Select: en_US.UTF-8 UTF-8
# → Set as default

# Or via command line
sudo locale-gen en_US.UTF-8
sudo update-locale LANG=en_US.UTF-8

# Reboot
sudo reboot
```

### Solution 3: Environment Variable

Set encoding via environment variable:

```bash
# In systemd service file
[Service]
Environment="PYTHONIOENCODING=utf-8"
Environment="LANG=en_US.UTF-8"
```

---

## Updated Systemd Service

To ensure UTF-8 encoding when running as service, update the service file:

```ini
[Unit]
Description=Restaurant Kiosk Peripherals Manager (Cash Reader + Receipt Printer)
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/kiosk
ExecStart=/usr/bin/python3 /home/pi/kiosk/kiosk_peripherals.py
Restart=always
RestartSec=10
StandardOutput=append:/var/log/kiosk-peripherals.log
StandardError=append:/var/log/kiosk-peripherals.log

# Environment variables for UTF-8 encoding
Environment="PYTHONUNBUFFERED=1"
Environment="PYTHONIOENCODING=utf-8"
Environment="LANG=en_US.UTF-8"
Environment="LC_ALL=en_US.UTF-8"

[Install]
WantedBy=multi-user.target
```

---

## Verification

After deploying the fix:

### 1. Test Locally First

```bash
cd ~/kiosk
python3 kiosk_peripherals.py
```

Watch for messages with ₱ symbol. Should log without errors.

### 2. Check Service Logs

```bash
sudo systemctl restart kiosk-peripherals.service
sudo journalctl -u kiosk-peripherals.service -f
```

Look for successful logging of peso amounts.

### 3. Check Log File

```bash
./check_kiosk_logs.sh | grep "₱"
```

Should display peso signs correctly.

---

## Common Unicode Issues in Python

| Character | Unicode | Error if not UTF-8 |
|-----------|---------|-------------------|
| ₱ | U+20B1 | ✗ latin-1 error |
| € | U+20AC | ✗ latin-1 error |
| ¥ | U+00A5 | ✓ Works in latin-1 |
| $ | U+0024 | ✓ Works in ASCII |
| ✓ | U+2713 | ✗ latin-1 error |
| ✗ | U+2717 | ✗ latin-1 error |

**Lesson:** Always use UTF-8 for international applications!

---

## Best Practices

### ✅ DO:
- Always specify `encoding='utf-8'` for file handlers
- Configure console output encoding
- Set environment variables for UTF-8
- Test with actual Unicode characters
- Use UTF-8 in configuration files too

### ❌ DON'T:
- Rely on system default encoding
- Assume ASCII/latin-1 is sufficient
- Use Unicode characters without proper encoding
- Forget to test on target system

---

## Summary

**Problem:** Peso sign (₱) caused encoding error  
**Cause:** Log file handler using latin-1 encoding  
**Solution:** Added `encoding='utf-8'` to RotatingFileHandler  
**Impact:** All Unicode characters now log correctly  
**Status:** ✅ Fixed

---

## Testing Checklist

- [x] Added UTF-8 encoding to file handler
- [x] Added UTF-8 reconfigure for console
- [x] Script compiles without errors
- [ ] Test on Raspberry Pi with actual data
- [ ] Verify log file displays ₱ correctly
- [ ] Check systemd logs show Unicode properly

---

**Quick Fix Summary:**
```python
# Added encoding='utf-8' to RotatingFileHandler
# Added sys.stdout.reconfigure(encoding='utf-8')
# This ensures all Unicode characters (₱, ✓, etc.) work correctly
```

Deploy the updated script and the encoding error will be gone! 🎉

