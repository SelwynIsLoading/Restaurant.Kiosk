# Systemd Service File Configuration Guide

## Your Corrected Service File

```ini
[Unit]
Description=Restaurant Kiosk Peripherals Manager (Cash Reader + Receipt Printer)
After=network.target

[Service]
Type=simple
User=administrator
WorkingDirectory=/home/administrator/pythonscripts
ExecStart=/usr/bin/python3 /home/administrator/pythonscripts/kiosk_peripherals.py
Restart=always
RestartSec=10
StandardOutput=append:/var/log/kiosk-peripherals.log
StandardError=append:/var/log/kiosk-peripherals.log

# Environment variables for UTF-8 encoding (supports ₱ and other Unicode)
Environment="PYTHONUNBUFFERED=1"
Environment="PYTHONIOENCODING=utf-8"
Environment="LANG=en_US.UTF-8"
Environment="LC_ALL=en_US.UTF-8"

[Install]
WantedBy=multi-user.target
```

---

## What Was Wrong

### Issue 1: User vs Working Directory Mismatch

**Your original:**
```ini
User=pi
WorkingDirectory=/home/administrator  # ❌ Mismatch!
```

**Corrected:**
```ini
User=administrator
WorkingDirectory=/home/administrator/pythonscripts  # ✅ Matches user
```

**Why this matters:**
- Service runs as `administrator` user
- Working directory should match where script is located
- Config file (`cash_reader_config.json`) needs to be in working directory

### Issue 2: Working Directory Should Be Script Directory

**Your original:**
```ini
WorkingDirectory=/home/administrator
ExecStart=/usr/bin/python3 /home/administrator/pythonscripts/kiosk_peripherals.py
```

**Corrected:**
```ini
WorkingDirectory=/home/administrator/pythonscripts  # ✅ Script's directory
ExecStart=/usr/bin/python3 /home/administrator/pythonscripts/kiosk_peripherals.py
```

**Why:**
- Python script looks for `cash_reader_config.json` in current directory
- Log file (`kiosk_peripherals.log`) is created in working directory
- Easier to manage all related files in one place

---

## Required File Structure

Your files should be organized like this:

```
/home/administrator/pythonscripts/
├── kiosk_peripherals.py          ← Main script
├── cash_reader_config.json       ← Configuration (REQUIRED!)
├── check_kiosk_logs.sh           ← Log viewer
├── kiosk_peripherals.log         ← Log file (created automatically)
└── arduino_cash_acceptor/
    └── arduino_cash_acceptor.ino ← Arduino code
```

---

## Installation Steps

### 1. Prepare Files

```bash
# SSH to your server
ssh administrator@your-server

# Create directory (if not exists)
mkdir -p /home/administrator/pythonscripts
cd /home/administrator/pythonscripts

# Upload files here
```

### 2. Upload Files from Development Machine

```bash
# From your Windows machine
scp kiosk_peripherals.py administrator@your-server:/home/administrator/pythonscripts/
scp cash_reader_config.json administrator@your-server:/home/administrator/pythonscripts/
scp check_kiosk_logs.sh administrator@your-server:/home/administrator/pythonscripts/
scp deployment/kiosk-peripherals.service.corrected administrator@your-server:/home/administrator/
```

### 3. Install Service

```bash
# SSH to server
ssh administrator@your-server

# Copy service file to systemd
sudo cp /home/administrator/kiosk-peripherals.service.corrected /etc/systemd/system/kiosk-peripherals.service

# Set correct permissions
sudo chmod 644 /etc/systemd/system/kiosk-peripherals.service

# Reload systemd
sudo systemctl daemon-reload
```

### 4. Set Permissions

```bash
# Add administrator user to dialout group (for serial ports)
sudo usermod -a -G dialout administrator

# Verify
groups administrator
# Should include: dialout

# Make scripts executable
chmod +x /home/administrator/pythonscripts/kiosk_peripherals.py
chmod +x /home/administrator/pythonscripts/check_kiosk_logs.sh

# Create log file with correct permissions
sudo touch /var/log/kiosk-peripherals.log
sudo chown administrator:administrator /var/log/kiosk-peripherals.log
```

### 5. Verify Configuration

```bash
cd /home/administrator/pythonscripts

# Check config file exists
ls -la cash_reader_config.json

# Validate JSON
python3 -m json.tool cash_reader_config.json

# Should show your configuration without errors
```

### 6. Test Before Starting Service

```bash
cd /home/administrator/pythonscripts

# Run manually to check for errors
python3 kiosk_peripherals.py

# You should see:
# [CASH] Starting cash reader loop...
# [CASH] Connecting to Arduino on /dev/ttyACM0...
# [PRINTER] Starting receipt printer loop...

# Press Ctrl+C to stop
```

### 7. Start Service

```bash
# Enable service to start on boot
sudo systemctl enable kiosk-peripherals.service

# Start service now
sudo systemctl start kiosk-peripherals.service

# Check status
sudo systemctl status kiosk-peripherals.service
```

**Expected output:**
```
● kiosk-peripherals.service - Restaurant Kiosk Peripherals Manager
   Loaded: loaded (/etc/systemd/system/kiosk-peripherals.service; enabled)
   Active: active (running) since ...
   Main PID: xxxxx (python3)
```

---

## Configuration File Location

**IMPORTANT:** The config file must be in the working directory!

```bash
# Your working directory:
cd /home/administrator/pythonscripts

# Config file MUST be here:
ls -la cash_reader_config.json

# If missing, copy it:
# scp cash_reader_config.json administrator@server:/home/administrator/pythonscripts/
```

The script loads config like this:
```python
config_file = Path(__file__).parent / "cash_reader_config.json"
#                                        ↑ Same directory as script
```

---

## Checking Logs

### Method 1: Using check_kiosk_logs.sh

```bash
cd /home/administrator/pythonscripts
./check_kiosk_logs.sh

# Or specific views:
./check_kiosk_logs.sh errors
./check_kiosk_logs.sh status
./check_kiosk_logs.sh summary
```

### Method 2: Direct Log Files

```bash
# Application log (created by script)
tail -f /home/administrator/pythonscripts/kiosk_peripherals.log

# Systemd log (from service)
tail -f /var/log/kiosk-peripherals.log

# Or via journalctl
sudo journalctl -u kiosk-peripherals.service -f
```

---

## Common Issues

### Issue: "Config file not found"

**Check:**
```bash
cd /home/administrator/pythonscripts
ls -la cash_reader_config.json
```

**If missing:**
```bash
# Create it
nano cash_reader_config.json
# Paste your configuration and save (Ctrl+O, Enter, Ctrl+X)
```

### Issue: "Permission denied" on serial port

**Check groups:**
```bash
groups administrator
```

**If dialout not listed:**
```bash
sudo usermod -a -G dialout administrator
# Logout and login, or reboot
sudo reboot
```

### Issue: Service fails to start

**Check logs:**
```bash
sudo journalctl -u kiosk-peripherals.service -n 50
```

**Common causes:**
- Python packages not installed
- Config file missing
- Serial port doesn't exist
- User not in dialout group

### Issue: Can't find serial port

**List USB devices:**
```bash
ls -l /dev/tty*
lsusb
dmesg | grep tty
```

**Update config if different:**
```bash
nano /home/administrator/pythonscripts/cash_reader_config.json
# Update "arduino_port" and "printer_serial_port"
```

---

## Service Management Commands

```bash
# Start service
sudo systemctl start kiosk-peripherals.service

# Stop service
sudo systemctl stop kiosk-peripherals.service

# Restart service
sudo systemctl restart kiosk-peripherals.service

# Check status
sudo systemctl status kiosk-peripherals.service

# Enable at boot
sudo systemctl enable kiosk-peripherals.service

# Disable at boot
sudo systemctl disable kiosk-peripherals.service

# View logs
sudo journalctl -u kiosk-peripherals.service -f

# View last 100 lines
sudo journalctl -u kiosk-peripherals.service -n 100
```

---

## Quick Setup Script

Save this as `setup-service.sh`:

```bash
#!/bin/bash
# Quick setup script

echo "Setting up kiosk peripherals service..."

# Install dependencies
sudo apt update
sudo apt install -y python3-serial python3-requests
sudo pip3 install python-escpos --break-system-packages

# Set permissions
sudo usermod -a -G dialout administrator
chmod +x /home/administrator/pythonscripts/kiosk_peripherals.py
chmod +x /home/administrator/pythonscripts/check_kiosk_logs.sh

# Install service
sudo cp /home/administrator/kiosk-peripherals.service.corrected /etc/systemd/system/kiosk-peripherals.service
sudo chmod 644 /etc/systemd/system/kiosk-peripherals.service

# Create log file
sudo touch /var/log/kiosk-peripherals.log
sudo chown administrator:administrator /var/log/kiosk-peripherals.log

# Reload and start
sudo systemctl daemon-reload
sudo systemctl enable kiosk-peripherals.service
sudo systemctl start kiosk-peripherals.service

# Show status
echo ""
echo "Service installed! Checking status..."
sudo systemctl status kiosk-peripherals.service

echo ""
echo "Check logs with:"
echo "  cd /home/administrator/pythonscripts"
echo "  ./check_kiosk_logs.sh"
```

---

## Verification Checklist

Before starting the service:

- [ ] User is `administrator` (or change to `pi` if preferred)
- [ ] Working directory is `/home/administrator/pythonscripts`
- [ ] Script path is `/home/administrator/pythonscripts/kiosk_peripherals.py`
- [ ] Config file exists: `/home/administrator/pythonscripts/cash_reader_config.json`
- [ ] User `administrator` is in `dialout` group
- [ ] Script is executable: `chmod +x kiosk_peripherals.py`
- [ ] Python packages installed (python3-serial, python3-requests, python-escpos)
- [ ] Serial ports exist (`/dev/ttyACM0`, `/dev/ttyUSB0`)

After starting:

- [ ] Service is active: `sudo systemctl status kiosk-peripherals.service`
- [ ] No errors in logs: `./check_kiosk_logs.sh errors`
- [ ] Arduino connected: Check for "Arduino status: READY"
- [ ] Printer connected: Check for "Successfully connected to printer"
- [ ] Peso signs (₱) display correctly in logs

---

## Summary of Changes

| Field | Your Original | Corrected | Reason |
|-------|--------------|-----------|--------|
| `User` | pi | administrator | Match your actual user |
| `WorkingDirectory` | /home/administrator | /home/administrator/pythonscripts | Match script location |
| Comment | "supports ?" | "supports ₱" | Display peso sign correctly |

---

## Quick Deploy

```bash
# Copy corrected service file
sudo cp deployment/kiosk-peripherals.service.corrected /etc/systemd/system/kiosk-peripherals.service

# Reload and restart
sudo systemctl daemon-reload
sudo systemctl restart kiosk-peripherals.service

# Check status
sudo systemctl status kiosk-peripherals.service

# Monitor logs
cd /home/administrator/pythonscripts
./check_kiosk_logs.sh
```

Your service is now correctly configured! 🚀


