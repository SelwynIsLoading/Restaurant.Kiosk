# Kiosk Peripherals - Deployment Checklist

## Your Configuration

✅ **VPS URL:** `https://bochogs-kiosk.store`  
✅ **Arduino Port:** `/dev/ttyACM0` (USB native Arduino)  
✅ **Printer Port:** `/dev/ttyUSB0` (USB-to-Serial)  
✅ **Environment:** Production  
✅ **JSON Valid:** Verified

---

## Pre-Deployment Checklist

### 1. Files to Copy to Raspberry Pi

```bash
# From your development machine
cd RestaurantKiosk

# Copy main script and configuration
scp kiosk_peripherals.py pi@raspberrypi:~/kiosk/
scp cash_reader_config.json pi@raspberrypi:~/kiosk/
scp check_kiosk_logs.sh pi@raspberrypi:~/kiosk/

# Copy deployment files
scp deployment/kiosk-peripherals.service pi@raspberrypi:~/kiosk/
scp deployment/install-unified-peripherals.sh pi@raspberrypi:~/kiosk/
```

### 2. Install Dependencies on Raspberry Pi

```bash
# SSH to Raspberry Pi
ssh pi@raspberrypi

# Update system
sudo apt update && sudo apt upgrade -y

# Install Python packages (system-wide)
sudo apt install -y python3-serial python3-requests
sudo pip3 install python-escpos --break-system-packages
```

### 3. Verify Hardware Connections

```bash
# Check Arduino connection
ls -l /dev/ttyACM0
# Should show: crw-rw---- 1 root dialout ... /dev/ttyACM0

# Check printer connection
ls -l /dev/ttyUSB0
# Should show: crw-rw---- 1 root dialout ... /dev/ttyUSB0

# If not found, list all ports
ls -l /dev/tty*
```

### 4. Set Permissions

```bash
# Add user to dialout group (for serial port access)
sudo usermod -a -G dialout pi

# Verify
groups pi
# Should include: dialout

# IMPORTANT: Logout and login again (or reboot)
sudo reboot
```

### 5. Test Configuration

```bash
cd ~/kiosk

# Validate JSON
python3 -m json.tool cash_reader_config.json

# Should output formatted JSON without errors
```

---

## Deployment Steps

### Option A: Automated Installation (Recommended)

```bash
cd ~/kiosk
chmod +x install-unified-peripherals.sh
./install-unified-peripherals.sh
```

This will:
- ✅ Stop old services
- ✅ Install dependencies
- ✅ Set up systemd service
- ✅ Start the service

### Option B: Manual Installation

```bash
cd ~/kiosk

# Make script executable
chmod +x kiosk_peripherals.py
chmod +x check_kiosk_logs.sh

# Install systemd service
sudo cp kiosk-peripherals.service /etc/systemd/system/
sudo chmod 644 /etc/systemd/system/kiosk-peripherals.service

# Reload systemd
sudo systemctl daemon-reload

# Enable and start service
sudo systemctl enable kiosk-peripherals.service
sudo systemctl start kiosk-peripherals.service
```

---

## Post-Deployment Verification

### 1. Check Service Status

```bash
sudo systemctl status kiosk-peripherals.service
```

**Expected:**
```
● kiosk-peripherals.service - Restaurant Kiosk Peripherals Manager
   Loaded: loaded
   Active: active (running)
```

### 2. Check Logs

```bash
cd ~/kiosk
./check_kiosk_logs.sh
```

**Expected to see:**
```
[CASH] Starting cash reader loop...
[CASH] Connecting to Arduino on /dev/ttyACM0 at 9600 baud...
[CASH] Successfully connected to Arduino
[CASH] Arduino status: READY
[PRINTER] Starting receipt printer loop...
[PRINTER] Connecting to serial printer...
[PRINTER] Successfully connected to printer
```

### 3. Test Arduino Communication

Send test command via Arduino Serial Monitor or:

```bash
# Send PING command to Arduino
echo "PING" > /dev/ttyACM0

# Check logs for PONG response
./check_kiosk_logs.sh | grep PONG
```

### 4. Test VPS Connection

```bash
# From Raspberry Pi, test VPS API
curl -I https://bochogs-kiosk.store

# Should return: HTTP/2 200

# Test cash payment API
curl https://bochogs-kiosk.store/api/cash-payment/active-sessions
```

### 5. End-to-End Test

**Test Cash Payment:**
1. Create order on kiosk (from browser)
2. Select "Cash Payment"
3. Watch Raspberry Pi logs: `./check_kiosk_logs.sh cash`
4. Should see: "New payment session detected: ORD-xxx"
5. Insert cash into acceptor (or send `TEST:BILL:100` via Arduino)
6. Should see: "Bill inserted: ₱100 for order ORD-xxx"
7. Should see: "Cash update successful"

**Test Receipt Printing:**
1. Complete a cash payment
2. Watch printer logs: `./check_kiosk_logs.sh printer`
3. Should see: "Received print job: PRINT-xxx"
4. Should see: "Receipt printed successfully"

---

## Troubleshooting

### Issue: Service won't start

```bash
# Test manually to see errors
cd ~/kiosk
python3 kiosk_peripherals.py

# Check for import errors, connection errors, etc.
```

### Issue: Arduino not found

```bash
# Check if connected
lsusb | grep Arduino

# Check port name
ls -l /dev/ttyACM*
ls -l /dev/ttyUSB*

# Update config if different port
nano cash_reader_config.json
```

### Issue: Permission denied on serial port

```bash
# Check dialout group
groups pi

# If not in dialout:
sudo usermod -a -G dialout pi
sudo reboot
```

### Issue: Can't reach VPS

```bash
# Test connection
curl -I https://bochogs-kiosk.store

# Check DNS
nslookup bochogs-kiosk.store

# Check internet
ping 8.8.8.8
```

### Issue: Logs show errors

```bash
# Check recent errors
./check_kiosk_logs.sh errors

# View specific error details
./check_kiosk_logs.sh last 100
```

---

## Configuration Summary

Your `cash_reader_config.json`:

```json
{
  "vps_api_url": "https://bochogs-kiosk.store",    ← Your VPS
  "arduino_port": "/dev/ttyACM0",                  ← Arduino USB native
  "printer_serial_port": "/dev/ttyUSB0",           ← Printer USB-to-Serial
  "enable_cash_reader": true,                      ← Enabled
  "enable_printer": true                           ← Enabled
}
```

**Port Configuration:**
- Arduino on `/dev/ttyACM0` (correct for Arduino Uno USB)
- Printer on `/dev/ttyUSB0` (correct for USB-to-TTL adapter)
- No port conflicts ✅

---

## Quick Commands Reference

```bash
# Deploy
scp kiosk_peripherals.py cash_reader_config.json check_kiosk_logs.sh pi@raspberrypi:~/kiosk/

# Setup
ssh pi@raspberrypi
cd ~/kiosk
chmod +x install-unified-peripherals.sh
./install-unified-peripherals.sh

# Monitor
./check_kiosk_logs.sh

# Check status
./check_kiosk_logs.sh status

# Check errors
./check_kiosk_logs.sh errors
```

---

## Security Recommendation

⚠️ **Set API Key Before Production Use**

**Generate key:**
```bash
openssl rand -base64 32
```

**Update configs:**
```json
// Raspberry Pi: cash_reader_config.json
{
  "api_key": "generated-key-here"
}

// VPS: appsettings.json
{
  "CashPayment": {
    "ApiKey": "generated-key-here"
  }
}
```

**Restart both:**
```bash
# On Raspberry Pi
sudo systemctl restart kiosk-peripherals.service

# On VPS
sudo systemctl restart restaurant-kiosk
```

---

## Ready to Deploy! 🚀

Your configuration is complete and validated. Follow the deployment steps above to get your peripherals running!

**Next Steps:**
1. ✅ Copy files to Raspberry Pi
2. ✅ Run installation script
3. ✅ Verify service is running
4. ✅ Test cash payment flow
5. ✅ Test receipt printing

**Need Help?**
- See `CONFIGURATION_GUIDE.md` for detailed config options
- See `LOG_CHECKING_QUICK_START.md` for monitoring
- See `PERIPHERALS_UNIFIED_SETUP.md` for complete setup guide

