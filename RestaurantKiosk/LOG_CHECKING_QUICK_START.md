# Log Checking - Quick Start Guide

## TL;DR - Check Logs Right Now

```bash
# Make script executable (first time only)
chmod +x check_kiosk_logs.sh

# View logs in real-time
./check_kiosk_logs.sh
```

That's it! Press Ctrl+C to stop.

---

## Most Useful Commands

### 1. Monitor Everything (Real-time)
```bash
./check_kiosk_logs.sh
```

### 2. Check for Errors
```bash
./check_kiosk_logs.sh errors
```

### 3. Monitor Cash Payments Only
```bash
./check_kiosk_logs.sh cash
```

### 4. Monitor Receipt Printing Only
```bash
./check_kiosk_logs.sh printer
```

### 5. Check Service Status
```bash
./check_kiosk_logs.sh status
```

---

## What You'll See

### Normal Operation
```
[CASH] New payment session detected: ORD-12345 - Amount: ₱250.00
[CASH] Bill inserted: ₱100 for order ORD-12345
[CASH] Cash update successful
[CASH] Bill inserted: ₱100 for order ORD-12345
[CASH] Payment completed for order ORD-12345
[PRINTER] Printing receipt for order: ORD-12345
[PRINTER] Receipt printed successfully
```

### Errors to Watch For
```
❌ [CASH] Failed to connect to Arduino
   → Check USB connection

❌ [CASH] Failed to send cash update: 404
   → Check VPS is running

❌ [PRINTER] Failed to connect to printer
   → Check printer power and USB

⚠️ [CASH] Received cash data but no active order session
   → Cash inserted without an order
```

---

## Quick Troubleshooting

### Script Not Working?
```bash
# Check if it's executable
ls -l check_kiosk_logs.sh

# Make it executable
chmod +x check_kiosk_logs.sh

# Try running directly
bash check_kiosk_logs.sh
```

### No Logs?
```bash
# Check if service is running
./check_kiosk_logs.sh status

# Or check systemd
sudo systemctl status kiosk-peripherals.service

# Start service
sudo systemctl start kiosk-peripherals.service
```

### Can't Find Log File?
```bash
# Script looks for log in same directory
cd ~/kiosk
ls -la kiosk_peripherals.log

# Or specify full path
cat ~/kiosk/kiosk_peripherals.log
```

---

## All Commands Cheat Sheet

| Command | What It Does |
|---------|-------------|
| `./check_kiosk_logs.sh` | Follow logs (real-time) |
| `./check_kiosk_logs.sh errors` | Show only errors |
| `./check_kiosk_logs.sh cash` | Cash reader logs only |
| `./check_kiosk_logs.sh printer` | Printer logs only |
| `./check_kiosk_logs.sh status` | Service status + log info |
| `./check_kiosk_logs.sh summary` | Statistics summary |
| `./check_kiosk_logs.sh last 50` | Last 50 lines |
| `./check_kiosk_logs.sh systemd` | Systemd service logs |

---

## For More Details

See `LOG_MONITORING_GUIDE.md` for comprehensive documentation.

---

## Remote Monitoring (From Another Computer)

```bash
# Follow logs via SSH
ssh pi@raspberrypi 'cd ~/kiosk && ./check_kiosk_logs.sh'

# Check errors remotely
ssh pi@raspberrypi 'cd ~/kiosk && ./check_kiosk_logs.sh errors'
```

---

## Pro Tips

1. **Keep a terminal open** with `./check_kiosk_logs.sh` to monitor in real-time
2. **Check errors first** when something goes wrong: `./check_kiosk_logs.sh errors`
3. **Use status command** to verify everything is running: `./check_kiosk_logs.sh status`
4. **Logs rotate automatically** at 10MB, so they won't fill your disk

---

**Need help?** Run `./check_kiosk_logs.sh help` for all options

