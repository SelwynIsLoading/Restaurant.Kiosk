# Your Setup Checklist - administrator@pythonscripts

## Your Configuration

**User:** `administrator`  
**Script Directory:** `/home/administrator/pythonscripts`  
**VPS URL:** `https://bochogs-kiosk.store`  
**Arduino Port:** `/dev/ttyACM0`  
**Printer Port:** `/dev/ttyUSB0`

---

## Quick Setup Commands

### 1. Create Directory Structure

```bash
# SSH to your server
ssh administrator@your-server

# Create directory
mkdir -p /home/administrator/pythonscripts
cd /home/administrator/pythonscripts
```

### 2. Upload All Files

**From your Windows machine, run these commands:**

```powershell
# Change to your project directory
cd "C:\Users\cauba\Documents\selwyn dev\2025\Restaurant\RestaurantKiosk\RestaurantKiosk"

# Upload Python script
scp kiosk_peripherals.py administrator@your-server:/home/administrator/pythonscripts/

# Upload configuration file (IMPORTANT!)
scp cash_reader_config.json administrator@your-server:/home/administrator/pythonscripts/

# Upload log checker script
scp check_kiosk_logs.sh administrator@your-server:/home/administrator/pythonscripts/

# Upload service file
scp deployment/kiosk-peripherals.service.corrected administrator@your-server:/home/administrator/
```

### 3. Install Dependencies

```bash
# SSH to server
ssh administrator@your-server

# Install Python packages system-wide
sudo apt update
sudo apt install -y python3-serial python3-requests
sudo pip3 install python-escpos --break-system-packages
```

### 4. Set Permissions

```bash
# Add user to dialout group (for serial port access)
sudo usermod -a -G dialout administrator

# Make scripts executable
chmod +x /home/administrator/pythonscripts/kiosk_peripherals.py
chmod +x /home/administrator/pythonscripts/check_kiosk_logs.sh

# Verify dialout group
groups administrator
# Should show: administrator adm dialout ...
```

### 5. Verify Files Are in Place

```bash
cd /home/administrator/pythonscripts

# Check all files exist
ls -la

# You should see:
# kiosk_peripherals.py          ✓
# cash_reader_config.json       ✓
# check_kiosk_logs.sh           ✓
```

**CRITICAL:** Make sure `cash_reader_config.json` is in the same directory as the script!

### 6. Test Configuration

```bash
cd /home/administrator/pythonscripts

# Validate JSON
python3 -m json.tool cash_reader_config.json

# Should output formatted JSON without errors
```

### 7. Install Service File

```bash
# Copy corrected service file
sudo cp /home/administrator/kiosk-peripherals.service.corrected /etc/systemd/system/kiosk-peripherals.service

# Set permissions
sudo chmod 644 /etc/systemd/system/kiosk-peripherals.service

# Create log file
sudo touch /var/log/kiosk-peripherals.log
sudo chown administrator:administrator /var/log/kiosk-peripherals.log

# Reload systemd
sudo systemctl daemon-reload
```

### 8. Test Manually First

```bash
cd /home/administrator/pythonscripts

# Run script manually to check for errors
python3 kiosk_peripherals.py

# You should see:
# ============================================================
# Restaurant Kiosk - Peripherals Manager (Unified)
# ============================================================
# Environment: production
# VPS API URL: https://bochogs-kiosk.store
# ...
# [CASH] Starting cash reader loop...
# [PRINTER] Starting receipt printer loop...

# Press Ctrl+C to stop if working
```

**If you see errors:**
- Check config file is in the directory
- Check serial ports exist: `ls -l /dev/ttyACM0 /dev/ttyUSB0`
- Check Python packages installed
- Check user in dialout group (may need logout/login)

### 9. Start as Service

```bash
# Enable service to start on boot
sudo systemctl enable kiosk-peripherals.service

# Start service
sudo systemctl start kiosk-peripherals.service

# Check status
sudo systemctl status kiosk-peripherals.service
```

**Expected:**
```
● kiosk-peripherals.service - Restaurant Kiosk Peripherals Manager
   Loaded: loaded
   Active: active (running)
```

### 10. Monitor Logs

```bash
cd /home/administrator/pythonscripts

# Follow logs in real-time
./check_kiosk_logs.sh

# Check for errors
./check_kiosk_logs.sh errors

# Check service status
./check_kiosk_logs.sh status
```

---

## Your cash_reader_config.json

Make sure this file is in `/home/administrator/pythonscripts/`:

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

## Troubleshooting Your Setup

### Config File Not Found

```bash
# Check current directory
pwd
# Should be: /home/administrator/pythonscripts

# List files
ls -la

# If cash_reader_config.json is missing:
# Upload it again or create it:
nano cash_reader_config.json
# Paste the JSON content above
```

### Permission Denied on Serial Ports

```bash
# Check if ports exist
ls -l /dev/ttyACM0
ls -l /dev/ttyUSB0

# Should show:
# crw-rw---- 1 root dialout ... /dev/ttyACM0

# Verify user is in dialout group
groups administrator

# If not in dialout:
sudo usermod -a -G dialout administrator

# IMPORTANT: Logout and login or reboot
sudo reboot
```

### Service Won't Start

```bash
# Check detailed error logs
sudo journalctl -u kiosk-peripherals.service -n 50

# Common issues:
# 1. Config file missing → Upload cash_reader_config.json
# 2. Python packages missing → sudo apt install python3-serial python3-requests
# 3. User not in dialout → sudo usermod -a -G dialout administrator
# 4. Script not executable → chmod +x kiosk_peripherals.py
```

---

## Quick Reference

### File Locations

| File | Location |
|------|----------|
| Script | `/home/administrator/pythonscripts/kiosk_peripherals.py` |
| Config | `/home/administrator/pythonscripts/cash_reader_config.json` |
| Log Checker | `/home/administrator/pythonscripts/check_kiosk_logs.sh` |
| Service File | `/etc/systemd/system/kiosk-peripherals.service` |
| Application Log | `/home/administrator/pythonscripts/kiosk_peripherals.log` |
| Systemd Log | `/var/log/kiosk-peripherals.log` |

### Commands

```bash
# Go to script directory
cd /home/administrator/pythonscripts

# Check logs
./check_kiosk_logs.sh

# Check service status
sudo systemctl status kiosk-peripherals.service

# Restart service
sudo systemctl restart kiosk-peripherals.service

# View systemd logs
sudo journalctl -u kiosk-peripherals.service -f
```

---

## Next Steps

1. ✅ Upload corrected service file
2. ✅ Install service
3. ✅ Make sure config file is in `/home/administrator/pythonscripts/`
4. ✅ Add administrator to dialout group
5. ✅ Test manually first
6. ✅ Start service
7. ✅ Monitor logs

**Everything is configured for your specific setup!** 🚀


