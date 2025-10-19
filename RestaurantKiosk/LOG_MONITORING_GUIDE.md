# Log Monitoring Guide - Kiosk Peripherals

## Overview

The kiosk peripherals system includes comprehensive logging for monitoring both the cash reader and receipt printer operations.

---

## Log Files

### Main Log File
**Location:** `kiosk_peripherals.log` (in script directory)

**Rotation:** Automatic when file reaches 10MB
- Maximum file size: 10MB
- Backup files: 5 (kiosk_peripherals.log.1 through .5)
- Oldest logs are automatically deleted

### Log Format
```
YYYY-MM-DD HH:MM:SS - module_name - LEVEL - [MODULE] Message
```

**Example:**
```
2025-10-17 16:34:20 - __main__ - INFO - [CASH] Bill inserted: ₱100 for order ORD-12345
2025-10-17 16:34:21 - __main__ - INFO - [PRINTER] Printing receipt for order: ORD-12345
```

---

## Quick Log Viewing

### Using the Log Checker Script

```bash
# Make executable (first time only)
chmod +x check_kiosk_logs.sh

# Follow logs in real-time (like tail -f)
./check_kiosk_logs.sh

# Or explicitly
./check_kiosk_logs.sh tail
```

### Available Commands

| Command | Description | Example |
|---------|-------------|---------|
| `tail` or `-f` | Follow logs in real-time | `./check_kiosk_logs.sh tail` |
| `all` | View all logs (paginated) | `./check_kiosk_logs.sh all` |
| `errors` | Show only errors | `./check_kiosk_logs.sh errors` |
| `cash` | Show cash reader logs only | `./check_kiosk_logs.sh cash` |
| `printer` | Show printer logs only | `./check_kiosk_logs.sh printer` |
| `today` | Show today's logs | `./check_kiosk_logs.sh today` |
| `last <n>` | Show last N lines | `./check_kiosk_logs.sh last 100` |
| `systemd` | Show systemd service logs | `./check_kiosk_logs.sh systemd` |
| `status` | Show service status + log info | `./check_kiosk_logs.sh status` |
| `summary` | Show log summary stats | `./check_kiosk_logs.sh summary` |
| `clear` | Clear log file | `./check_kiosk_logs.sh clear` |
| `help` | Show help | `./check_kiosk_logs.sh help` |

---

## Log Levels

### INFO (Normal Operation)
```
[CASH] Starting cash reader loop...
[CASH] New payment session detected: ORD-12345
[CASH] Bill inserted: ₱100 for order ORD-12345
[PRINTER] Printing receipt for order: ORD-12345
[PRINTER] Receipt printed successfully
```

### WARNING (Potential Issues)
```
[CASH] Received cash data but no active order session
[PRINTER] Unexpected status code: 500
```

### ERROR (Failures)
```
[CASH] Failed to connect to Arduino: [Errno 2] No such file or directory
[CASH] Failed to send cash update: 500 - Internal Server Error
[PRINTER] Failed to connect to printer: Permission denied
```

### DEBUG (Detailed Information)
```
[CASH] Received from Arduino: BILL:100
[CASH] Arduino info: # Heartbeat - Bills:5 Coins:2
[PRINTER] Error checking for print jobs: Connection timeout
```

---

## Monitoring Scenarios

### 1. Monitor Cash Payments in Real-Time

```bash
# Follow cash reader logs only
./check_kiosk_logs.sh cash

# Or filter while following
./check_kiosk_logs.sh tail | grep "\[CASH\]"
```

**What to look for:**
- ✅ `New payment session detected` - Order waiting for cash
- ✅ `Bill inserted` - Customer inserting money
- ✅ `Cash update successful` - VPS acknowledged
- ✅ `Payment completed` - Order paid in full
- ❌ `Failed to send cash update` - Network/API issue
- ⚠️ `no active order session` - Cash inserted with no order

### 2. Monitor Receipt Printing

```bash
# Follow printer logs only
./check_kiosk_logs.sh printer
```

**What to look for:**
- ✅ `Successfully connected to printer` - Printer ready
- ✅ `Printing receipt for order` - Print job started
- ✅ `Receipt printed successfully` - Print complete
- ❌ `Failed to connect to printer` - Printer offline
- ❌ `Error printing receipt` - Hardware/paper issue

### 3. Check for Errors

```bash
# Show recent errors
./check_kiosk_logs.sh errors

# Or show last 100 lines and filter
./check_kiosk_logs.sh last 100 | grep ERROR
```

### 4. Monitor System Health

```bash
# Show service status and log summary
./check_kiosk_logs.sh status

# Show log statistics
./check_kiosk_logs.sh summary
```

**Output example:**
```
=== LOG SUMMARY ===

Total lines: 1523
Errors: 3
Warnings: 12
Cash reader logs: 847
Printer logs: 523

Recent errors:
[CASH] Failed to connect to Arduino: [Errno 2] No such file or directory
```

### 5. Troubleshoot Connection Issues

```bash
# Check last 50 lines for connection errors
./check_kiosk_logs.sh last 50 | grep -i "connect\|timeout\|refused"
```

---

## Direct Log Commands

### Using tail (Manual)

```bash
# Follow logs
tail -f kiosk_peripherals.log

# Last 100 lines
tail -n 100 kiosk_peripherals.log

# Follow with search
tail -f kiosk_peripherals.log | grep "\[CASH\]"
```

### Using grep (Search)

```bash
# Find all errors
grep "ERROR" kiosk_peripherals.log

# Find cash reader logs
grep "\[CASH\]" kiosk_peripherals.log

# Find specific order
grep "ORD-12345" kiosk_peripherals.log

# Count errors
grep -c "ERROR" kiosk_peripherals.log
```

### Using less (Paginated View)

```bash
# View with search
less kiosk_peripherals.log

# Start at end (most recent)
less +G kiosk_peripherals.log
```

---

## Systemd Service Logs

If running as a systemd service:

```bash
# Follow systemd logs
sudo journalctl -u kiosk-peripherals.service -f

# Last 100 lines
sudo journalctl -u kiosk-peripherals.service -n 100

# Today's logs
sudo journalctl -u kiosk-peripherals.service --since today

# Logs since specific time
sudo journalctl -u kiosk-peripherals.service --since "2025-10-17 16:00:00"

# Logs with errors only
sudo journalctl -u kiosk-peripherals.service -p err
```

---

## Log Analysis

### Find Most Common Errors

```bash
grep "ERROR" kiosk_peripherals.log | cut -d'-' -f4- | sort | uniq -c | sort -rn
```

### Count Operations by Type

```bash
echo "Bills inserted: $(grep -c "Bill inserted" kiosk_peripherals.log)"
echo "Coins inserted: $(grep -c "Coin inserted" kiosk_peripherals.log)"
echo "Receipts printed: $(grep -c "Receipt printed successfully" kiosk_peripherals.log)"
```

### Check Payment Session Activity

```bash
echo "Sessions started: $(grep -c "New payment session detected" kiosk_peripherals.log)"
echo "Payments completed: $(grep -c "Payment completed" kiosk_peripherals.log)"
```

### Find Slow Operations

```bash
# Look for timeout errors
grep -i "timeout" kiosk_peripherals.log

# Look for retry attempts
grep "Attempt [2-3]/" kiosk_peripherals.log
```

---

## Log Rotation

### Automatic Rotation (Built-in)

The Python script automatically rotates logs:
- **Trigger:** When log file reaches 10MB
- **Backup files:** 5 (keeps last 50MB total)
- **Files created:**
  - `kiosk_peripherals.log` (current)
  - `kiosk_peripherals.log.1` (previous)
  - `kiosk_peripherals.log.2` (older)
  - ... up to `.5`

### Manual Rotation

```bash
# Archive current log
mv kiosk_peripherals.log kiosk_peripherals.log.$(date +%Y%m%d_%H%M%S)

# Script will create new log automatically
```

### Clear Logs

```bash
# Using script (with confirmation)
./check_kiosk_logs.sh clear

# Manual (immediate)
> kiosk_peripherals.log

# Or delete and restart service
rm kiosk_peripherals.log
sudo systemctl restart kiosk-peripherals.service
```

---

## Remote Monitoring

### SSH + Log Viewing

```bash
# SSH and follow logs
ssh pi@raspberrypi 'cd ~/kiosk && tail -f kiosk_peripherals.log'

# SSH and check errors
ssh pi@raspberrypi 'cd ~/kiosk && grep ERROR kiosk_peripherals.log | tail -20'
```

### Copy Logs for Analysis

```bash
# Copy log file from Raspberry Pi
scp pi@raspberrypi:~/kiosk/kiosk_peripherals.log ./kiosk_logs/

# Copy all rotated logs
scp pi@raspberrypi:~/kiosk/kiosk_peripherals.log* ./kiosk_logs/
```

---

## Debugging Common Issues

### Issue: No Logs Being Generated

**Check:**
```bash
# 1. Is script running?
ps aux | grep kiosk_peripherals

# 2. Check systemd status
sudo systemctl status kiosk-peripherals.service

# 3. Check file permissions
ls -la kiosk_peripherals.log

# 4. Check disk space
df -h
```

### Issue: Logs Growing Too Fast

**Solution 1: Reduce Log Level** (Edit script)
```python
root_logger.setLevel(logging.WARNING)  # Only warnings and errors
```

**Solution 2: Increase Rotation Size**
```python
file_handler = RotatingFileHandler(
    'kiosk_peripherals.log',
    maxBytes=50*1024*1024,  # 50MB instead of 10MB
    backupCount=10  # Keep more backups
)
```

### Issue: Can't Find Specific Event

**Use grep with context:**
```bash
# Show 5 lines before and after match
grep -C 5 "ORDER-12345" kiosk_peripherals.log

# Show 10 lines after error
grep -A 10 "ERROR" kiosk_peripherals.log
```

---

## Best Practices

### 1. Regular Monitoring

```bash
# Add to crontab for daily email summary
0 9 * * * cd /home/pi/kiosk && ./check_kiosk_logs.sh summary | mail -s "Kiosk Daily Summary" admin@example.com
```

### 2. Alert on Errors

```bash
# Check for errors every 5 minutes
*/5 * * * * cd /home/pi/kiosk && [ $(grep -c ERROR kiosk_peripherals.log) -gt 10 ] && echo "Too many errors" | mail -s "Kiosk Alert" admin@example.com
```

### 3. Archive Old Logs

```bash
# Weekly archive
0 0 * * 0 cd /home/pi/kiosk && tar -czf logs_$(date +%Y%m%d).tar.gz kiosk_peripherals.log.* && rm kiosk_peripherals.log.*
```

### 4. Monitor Disk Space

```bash
# Check if log directory is over 100MB
du -sm /home/pi/kiosk | awk '$1 > 100 {print "Log directory too large"}'
```

---

## Quick Reference Card

```bash
# Real-time monitoring
./check_kiosk_logs.sh                    # Follow all logs
./check_kiosk_logs.sh cash               # Cash reader only
./check_kiosk_logs.sh printer            # Printer only

# Troubleshooting
./check_kiosk_logs.sh errors             # Show errors
./check_kiosk_logs.sh status             # Service status
./check_kiosk_logs.sh summary            # Statistics

# History
./check_kiosk_logs.sh last 100           # Last 100 lines
./check_kiosk_logs.sh today              # Today's logs
./check_kiosk_logs.sh all                # All logs (paginated)

# System logs
./check_kiosk_logs.sh systemd            # Systemd logs

# Maintenance
./check_kiosk_logs.sh clear              # Clear logs
```

---

## Support

For issues with logging:
1. Check file permissions: `ls -la kiosk_peripherals.log`
2. Check disk space: `df -h`
3. Check script status: `./check_kiosk_logs.sh status`
4. Check systemd logs: `./check_kiosk_logs.sh systemd`

**Log file location:** `~/kiosk/kiosk_peripherals.log`  
**Script location:** `~/kiosk/check_kiosk_logs.sh`

