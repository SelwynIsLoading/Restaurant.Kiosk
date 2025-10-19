# Cash Payment System - VPS Deployment Guide

## Overview

This guide covers deploying the cash payment system when your **ASP.NET Core application is hosted on a cloud VPS** while the **Raspberry Pi with Arduino hardware is on your home network with a dynamic IP address**.

## ✅ Architecture: Why Dynamic Home IP is NOT a Problem

### Current Architecture Flow

```
┌─────────────────────────────────────────────────────────────┐
│                    CLOUD VPS (Static IP/Domain)              │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  ASP.NET Core Application                              │ │
│  │  - CashPaymentController                               │ │
│  │  - POST /api/cash-payment/update (receives updates)   │ │
│  │  - SignalR Hub (broadcasts to browsers)               │ │
│  └────────────────────────────────────────────────────────┘ │
│                            ↑                                  │
│                            │ HTTPS POST (Outgoing from Pi)    │
└────────────────────────────┼─────────────────────────────────┘
                             │
                    ┌────────┴───────┐
                    │   INTERNET     │
                    └────────┬───────┘
                             │
┌────────────────────────────┼─────────────────────────────────┐
│       HOME NETWORK (Dynamic IP - Changes Daily)              │
│                            ↓                                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Raspberry Pi                                          │ │
│  │  - arduino_cash_reader.py                             │ │
│  │  - Makes OUTGOING HTTP requests to VPS                │ │
│  │  - Reads from Arduino via USB                         │ │
│  └────────────────────────────────────────────────────────┘ │
│                            ↑                                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Arduino + Cash Acceptors                              │ │
│  │  - Bill acceptor                                       │ │
│  │  - Coin acceptor                                       │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Why This Works Perfectly

✅ **Raspberry Pi initiates connections** (OUTGOING from home network)  
✅ **VPS only receives requests** (INCOMING to cloud - no problem)  
✅ **Home NAT/Firewall allows outgoing connections** (standard behavior)  
✅ **Dynamic IP doesn't matter** (Pi connects TO VPS, not the reverse)  
✅ **No port forwarding needed** on home router  
✅ **No dynamic DNS needed** for home network  

### What Would NOT Work

❌ If VPS tried to connect TO the Raspberry Pi (would need static IP/port forwarding)  
❌ If we used webhooks from VPS to Pi (impossible with dynamic IP behind NAT)  

### What DOES Work (Our Implementation)

✅ Polling-based architecture (Pi asks VPS for work)  
✅ Push-based updates (Pi pushes data to VPS)  
✅ SignalR for browser updates (VPS to browsers, not VPS to Pi)  

---

## Deployment Steps

### Step 1: VPS Setup

#### 1.1 Deploy ASP.NET Core Application to VPS

See [VPS_HYBRID_DEPLOYMENT.md](VPS_HYBRID_DEPLOYMENT.md) for complete VPS deployment instructions.

**Quick summary:**
```bash
# On your development machine
cd RestaurantKiosk
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish-vps

# Transfer to VPS
scp -r ./publish-vps/* user@your-vps-ip:/var/www/restaurant-kiosk/

# On VPS
sudo systemctl restart restaurant-kiosk
```

#### 1.2 Configure Cash Payment API Key (Optional but Recommended)

Edit `appsettings.Production.json` on your VPS:

```json
{
  "CashPayment": {
    "ApiKey": "your-secure-random-api-key-here-generate-something-long-and-random"
  }
}
```

Generate a secure API key:
```bash
# On Linux/Mac
openssl rand -base64 32

# On Windows PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

#### 1.3 Verify VPS is Accessible

```bash
# Test from your development machine
curl https://your-vps-domain.com/api/cash-payment/status/TEST-001

# Should return 404 (not found) but that's OK - it means the endpoint is accessible
```

### Step 2: Raspberry Pi Setup

#### 2.1 Install Python and Dependencies

```bash
# SSH to your Raspberry Pi
ssh pi@raspberry-pi-ip

# Update system
sudo apt update && sudo apt upgrade -y

# Install Python dependencies
sudo apt install -y python3 python3-pip

# Install required Python packages
pip3 install pyserial requests --break-system-packages
```

#### 2.2 Copy Files to Raspberry Pi

From your development machine:

```powershell
# PowerShell
scp RestaurantKiosk/arduino_cash_reader.py pi@raspberry-pi-ip:~/
scp RestaurantKiosk/cash_reader_config.json pi@raspberry-pi-ip:~/
scp RestaurantKiosk/requirements.txt pi@raspberry-pi-ip:~/
```

Or from Linux/Mac:

```bash
scp RestaurantKiosk/arduino_cash_reader.py pi@raspberry-pi-ip:~/
scp RestaurantKiosk/cash_reader_config.json pi@raspberry-pi-ip:~/
scp RestaurantKiosk/requirements.txt pi@raspberry-pi-ip:~/
```

#### 2.3 Configure Python Script for VPS

Edit the configuration file on Raspberry Pi:

```bash
nano ~/cash_reader_config.json
```

Update with your VPS details:

```json
{
  "vps_api_url": "https://your-vps-domain.com",
  "arduino_port": "/dev/ttyUSB0",
  "baud_rate": 9600,
  "reconnect_delay_seconds": 5,
  "connection_timeout_seconds": 10,
  "retry_attempts": 3,
  "api_key": "your-secure-random-api-key-here-same-as-vps",
  "environment": "production",
  "logging": {
    "log_file": "cash_reader.log",
    "log_level": "INFO"
  }
}
```

**Important Configuration Notes:**

| Setting | Description | Example |
|---------|-------------|---------|
| `vps_api_url` | Your VPS domain (HTTPS recommended) | `https://kiosk.yourdomain.com` |
| `arduino_port` | USB port where Arduino is connected | `/dev/ttyUSB0` or `/dev/ttyACM0` |
| `api_key` | Same key as configured on VPS | Must match VPS config |
| `environment` | Set to "production" for live deployment | `production` |

#### 2.4 Find Arduino Port

```bash
# Before connecting Arduino
ls /dev/tty*

# Connect Arduino via USB

# After connecting Arduino
ls /dev/tty*

# Look for new device, usually /dev/ttyUSB0 or /dev/ttyACM0
```

#### 2.5 Set Permissions

```bash
# Add user to dialout group for serial port access
sudo usermod -a -G dialout $USER

# Logout and login for changes to take effect
exit
ssh pi@raspberry-pi-ip
```

#### 2.6 Test Connection

```bash
# Test the Python script manually
python3 ~/arduino_cash_reader.py
```

You should see output like:
```
============================================================
Restaurant Kiosk - Arduino Cash Reader
============================================================
Environment: production
Arduino Port: /dev/ttyUSB0
Baud Rate: 9600
VPS API URL: https://your-vps-domain.com
Connection Timeout: 10s
Retry Attempts: 3
API Key Configured: Yes
============================================================
NOTE: Dynamic home IP is OK - only VPS needs static address
Python makes OUTGOING connections to VPS (works through NAT)
============================================================
```

**Press Ctrl+C to stop when done testing.**

### Step 3: Create Systemd Service (Auto-Start)

#### 3.1 Create Service File

```bash
sudo nano /etc/systemd/system/cash-reader.service
```

Paste this content:

```ini
[Unit]
Description=Arduino Cash Reader Service for Restaurant Kiosk
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi
ExecStart=/usr/bin/python3 /home/pi/arduino_cash_reader.py
Restart=always
RestartSec=10
StandardOutput=append:/home/pi/cash_reader.log
StandardError=append:/home/pi/cash_reader.log

# Network retry configuration
StartLimitIntervalSec=0

[Install]
WantedBy=multi-user.target
```

#### 3.2 Enable and Start Service

```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service to start on boot
sudo systemctl enable cash-reader

# Start service now
sudo systemctl start cash-reader

# Check status
sudo systemctl status cash-reader
```

#### 3.3 Monitor Logs

```bash
# Real-time logs
sudo journalctl -u cash-reader -f

# Last 50 lines
sudo journalctl -u cash-reader -n 50

# Or view the log file directly
tail -f ~/cash_reader.log
```

---

## Testing

### Test 1: Network Connectivity

From Raspberry Pi, verify you can reach the VPS:

```bash
# Test HTTPS connection
curl -I https://your-vps-domain.com

# Test cash payment endpoint
curl -X POST https://your-vps-domain.com/api/cash-payment/init \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","totalAmount":100.00}'
```

### Test 2: Arduino Connection

```bash
# Check if Arduino is detected
ls -l /dev/ttyUSB0

# Or
ls -l /dev/ttyACM0

# Test reading from Arduino (if you have test commands)
python3 -c "import serial; s=serial.Serial('/dev/ttyUSB0',9600); print(s.readline())"
```

### Test 3: End-to-End Test

1. **Start a cash payment session** from your kiosk web interface
2. **Insert cash** into the Arduino cash acceptor
3. **Watch the logs** on Raspberry Pi:
   ```bash
   tail -f ~/cash_reader.log
   ```
4. **Verify** the kiosk UI updates in real-time
5. **Check VPS logs** (optional):
   ```bash
   ssh user@your-vps-ip
   sudo journalctl -u restaurant-kiosk -f | grep "Cash"
   ```

### Test 4: Simulate Cash Insertion (Without Hardware)

From your development machine:

```bash
# Initialize a payment session
curl -X POST https://your-vps-domain.com/api/cash-payment/init \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","totalAmount":500.00}'

# Simulate cash insertions (like the Pi would send)
curl -X POST https://your-vps-domain.com/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","amount":100.00}'

# Check status
curl https://your-vps-domain.com/api/cash-payment/status/TEST-001
```

---

## Troubleshooting

### Issue 1: "Connection Error - Cannot Reach VPS"

**Symptoms:**
```
Connection error - cannot reach VPS API at https://your-domain.com: ...
```

**Solutions:**

1. **Check VPS is running:**
   ```bash
   ssh user@your-vps-ip
   sudo systemctl status restaurant-kiosk
   ```

2. **Test connectivity from Pi:**
   ```bash
   curl -I https://your-vps-domain.com
   ```

3. **Check firewall on VPS:**
   ```bash
   sudo ufw status
   # Should allow ports 80 and 443
   ```

4. **Verify DNS resolution:**
   ```bash
   nslookup your-vps-domain.com
   ```

5. **Check if using HTTPS with self-signed cert:**
   - Use a proper SSL certificate (Let's Encrypt is free)
   - Or temporarily use HTTP for testing (not recommended for production)

### Issue 2: "Invalid or Missing API Key"

**Symptoms:**
```
HTTP 401: Invalid or missing API key
```

**Solutions:**

1. **Verify API key matches:**
   - Check `cash_reader_config.json` on Pi
   - Check `appsettings.Production.json` on VPS
   - Keys must be EXACTLY the same (case-sensitive)

2. **Temporarily disable API key for testing:**
   - On VPS, set `"ApiKey": null` in appsettings
   - Restart VPS application
   - On Pi, remove or set `"api_key": null` in config
   - Restart cash reader service

### Issue 3: "Arduino Not Detected"

**Symptoms:**
```
Failed to connect to Arduino: [Errno 2] No such file or directory: '/dev/ttyUSB0'
```

**Solutions:**

1. **Find the correct port:**
   ```bash
   # List all serial devices
   ls /dev/tty* | grep -E "(USB|ACM)"
   
   # Check dmesg for Arduino connection
   dmesg | grep -i tty
   ```

2. **Update config with correct port:**
   ```bash
   nano ~/cash_reader_config.json
   # Change arduino_port to the correct device
   ```

3. **Check permissions:**
   ```bash
   # Check if user is in dialout group
   groups
   
   # Add if missing
   sudo usermod -a -G dialout $USER
   
   # Logout and login
   exit
   ssh pi@raspberry-pi-ip
   ```

4. **Check USB cable and connection:**
   - Try a different USB cable
   - Try a different USB port on Pi
   - Verify Arduino has power (LED on)

### Issue 4: Service Keeps Restarting

**Symptoms:**
```bash
sudo systemctl status cash-reader
# Shows: activating (auto-restart)
```

**Solutions:**

1. **Check logs for errors:**
   ```bash
   sudo journalctl -u cash-reader -n 100
   ```

2. **Test script manually:**
   ```bash
   sudo systemctl stop cash-reader
   python3 ~/arduino_cash_reader.py
   # Watch for errors
   ```

3. **Common causes:**
   - Wrong Python path
   - Missing dependencies
   - Arduino not connected
   - Invalid configuration file

### Issue 5: "Home IP Changed - Not Connecting"

**Good News:** This is NOT actually a problem!

**Why:** The Python script makes OUTGOING connections. Your home IP changing doesn't affect outgoing connections.

**If it's not working after IP change:**

1. **Check internet connectivity:**
   ```bash
   ping 8.8.8.8
   ping your-vps-domain.com
   ```

2. **Service should auto-reconnect:**
   - The script has retry logic
   - Systemd will restart the service
   - Check logs: `tail -f ~/cash_reader.log`

3. **Manual restart (if needed):**
   ```bash
   sudo systemctl restart cash-reader
   ```

### Issue 6: "SSL Certificate Verification Failed"

**Symptoms:**
```
SSLError: [SSL: CERTIFICATE_VERIFY_FAILED]
```

**Solutions:**

1. **Use proper SSL certificate on VPS:**
   ```bash
   # On VPS - Get free Let's Encrypt cert
   sudo certbot --nginx -d your-domain.com
   ```

2. **Update system certificates on Pi:**
   ```bash
   sudo apt update
   sudo apt install ca-certificates
   sudo update-ca-certificates
   ```

---

## Monitoring & Maintenance

### Check Service Status

```bash
# On Raspberry Pi
sudo systemctl status cash-reader

# View real-time logs
sudo journalctl -u cash-reader -f

# View log file
tail -f ~/cash_reader.log

# Check if Arduino is connected
ls -l /dev/ttyUSB* /dev/ttyACM* 2>/dev/null
```

### Restart Service

```bash
# Restart cash reader service
sudo systemctl restart cash-reader

# Check status after restart
sudo systemctl status cash-reader
```

### Update Configuration

```bash
# Edit config
nano ~/cash_reader_config.json

# Restart service to apply changes
sudo systemctl restart cash-reader

# Monitor logs to verify
sudo journalctl -u cash-reader -f
```

### Update Python Script

```bash
# Backup current version
cp ~/arduino_cash_reader.py ~/arduino_cash_reader.py.backup

# Upload new version from dev machine
# (from your dev machine)
scp RestaurantKiosk/arduino_cash_reader.py pi@raspberry-pi-ip:~/

# Restart service
sudo systemctl restart cash-reader
```

---

## Security Best Practices

### 1. Use HTTPS (Required for Production)

```bash
# On VPS - Install Let's Encrypt certificate
sudo certbot --nginx -d your-domain.com

# Auto-renewal is configured automatically
sudo certbot renew --dry-run
```

### 2. Use Strong API Key

```bash
# Generate a strong random key
openssl rand -base64 32

# Update on both VPS and Pi
```

### 3. Monitor Failed Requests

```bash
# On VPS - Check for suspicious activity
sudo journalctl -u restaurant-kiosk | grep "Invalid API key"

# Consider implementing rate limiting
```

### 4. Keep Systems Updated

```bash
# On Raspberry Pi
sudo apt update && sudo apt upgrade -y

# On VPS
sudo apt update && sudo apt upgrade -y
```

---

## FAQ

### Q: What happens if my home internet goes down?

**A:** The cash payment system won't work during the outage. The service will automatically reconnect when internet is restored.

**Recommendations:**
- Have a backup payment method (manual POS system)
- Consider a backup internet connection (4G/5G modem)
- Monitor uptime with external service

### Q: What happens if the VPS goes down?

**A:** Same as above - system won't work during outage. 

**Recommendations:**
- Choose reliable VPS provider (99.9%+ uptime SLA)
- Set up monitoring (UptimeRobot, Pingdom, etc.)
- Have backup payment method

### Q: Do I need to port forward on my router?

**A:** No! The Python script makes OUTGOING connections to the VPS. Port forwarding is only needed for INCOMING connections.

### Q: Do I need a static IP at home?

**A:** No! Dynamic IP is perfectly fine for this architecture.

### Q: Can I use multiple Raspberry Pis with one VPS?

**A:** Yes! Each Pi runs its own cash reader service, all connecting to the same VPS.

### Q: What if I want to move the VPS to a different provider?

**A:**
1. Deploy to new VPS
2. Update DNS to point to new VPS
3. Update `cash_reader_config.json` on Pi with new URL
4. Restart cash reader service

### Q: Can this work with a local server instead of VPS?

**A:** Yes, but then YOU would need a static IP or dynamic DNS at your location. The VPS approach is simpler for most cases.

---

## Architecture Summary

| Component | Location | IP Type | Notes |
|-----------|----------|---------|-------|
| ASP.NET Core App | Cloud VPS | Static (has domain) | Receives HTTP requests |
| Python Script | Home Pi | Dynamic (changes) | Makes HTTP requests |
| Arduino | Home Pi | N/A (USB) | Serial connection to Pi |
| Browser Clients | Anywhere | N/A | Connect to VPS via HTTPS |

**Key Point:** The Python script always initiates connections TO the VPS, never the other way around. This is why dynamic home IP is not a problem.

---

## Next Steps

1. ✅ Deploy ASP.NET Core app to VPS
2. ✅ Configure cash payment API key
3. ✅ Setup Raspberry Pi with Python script
4. ✅ Configure Python script with VPS URL
5. ✅ Create systemd service for auto-start
6. ✅ Test end-to-end
7. ✅ Monitor and maintain

---

## Support

For additional help:
- Check [CASH_PAYMENT_SETUP.md](CASH_PAYMENT_SETUP.md) for general setup
- Check [VPS_HYBRID_DEPLOYMENT.md](VPS_HYBRID_DEPLOYMENT.md) for VPS deployment
- Review logs on both Pi and VPS
- Test network connectivity from Pi to VPS

---

**Version:** 1.0  
**Last Updated:** January 2025  
**Architecture:** VPS + Home Raspberry Pi with Dynamic IP

