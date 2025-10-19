# Cash Payment System - Quick Start Guide

## Choose Your Deployment Scenario

### Scenario 1: Local Development (Same Machine)
**Use Case:** Testing on your development machine  
**Time:** 5 minutes  
**Requirements:** Arduino connected to dev machine

```bash
# 1. Ensure arduino_cash_reader.py uses default config (localhost)
cd RestaurantKiosk
python arduino_cash_reader.py
```

✅ **Done!** Script connects to localhost:5000

---

### Scenario 2: Raspberry Pi Standalone (All-in-One)
**Use Case:** App and Arduino both on same Raspberry Pi  
**Time:** 10 minutes  
**Requirements:** Raspberry Pi with .NET runtime

#### Setup Steps:

```bash
# 1. Copy configuration
cp cash_reader_config.example.json cash_reader_config.json

# 2. No changes needed - uses localhost by default

# 3. Install dependencies
pip3 install pyserial requests --break-system-packages

# 4. Run
python3 arduino_cash_reader.py
```

✅ **Done!** Everything runs on the Pi.

---

### Scenario 3: VPS Deployment (App in Cloud, Arduino at Home)
**Use Case:** App on cloud VPS, Raspberry Pi with Arduino at home  
**Time:** 20 minutes  
**Requirements:** VPS with domain/IP, Raspberry Pi at home

#### Quick Setup:

```bash
# 1. On Raspberry Pi - Copy configuration
cd ~
cp cash_reader_config.example.json cash_reader_config.json

# 2. Edit configuration
nano cash_reader_config.json
```

**Update these values:**
```json
{
  "vps_api_url": "https://your-vps-domain.com",
  "arduino_port": "/dev/ttyUSB0",
  "api_key": "your-api-key-from-vps-config",
  "environment": "production"
}
```

```bash
# 3. Install dependencies
pip3 install -r requirements.txt --break-system-packages

# 4. Test connection
python3 arduino_cash_reader.py

# 5. Create service (auto-start on boot)
sudo nano /etc/systemd/system/cash-reader.service
```

Paste this:
```ini
[Unit]
Description=Arduino Cash Reader Service
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi
ExecStart=/usr/bin/python3 /home/pi/arduino_cash_reader.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

```bash
# 6. Enable and start
sudo systemctl daemon-reload
sudo systemctl enable cash-reader
sudo systemctl start cash-reader

# 7. Check status
sudo systemctl status cash-reader
```

✅ **Done!** Service runs automatically.

---

## Configuration Cheat Sheet

| Scenario | `vps_api_url` | `arduino_port` | `api_key` |
|----------|---------------|----------------|-----------|
| Development | `http://localhost:5000` | `COM3` (Win) or `/dev/ttyUSB0` (Linux) | `null` |
| Pi Standalone | `http://localhost:5000` | `/dev/ttyUSB0` | `null` |
| VPS Production | `https://your-domain.com` | `/dev/ttyUSB0` | Required |

---

## Testing

### Test 1: Without Arduino (Simulate)

```bash
# Start Python script (or service)
python3 arduino_cash_reader.py

# In another terminal, simulate cash insertion
curl -X POST http://localhost:5000/api/cash-payment/init \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","totalAmount":500}'

curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","amount":100}'
```

### Test 2: With Arduino Hardware

1. Navigate to `/kiosk` in browser
2. Add items to cart
3. Select "Cash" payment
4. Insert bills/coins into acceptors
5. Watch UI update in real-time

---

## Troubleshooting

### "Cannot connect to Arduino"

```bash
# Find Arduino port
ls /dev/tty* | grep -E "(USB|ACM)"

# Update config with correct port
nano cash_reader_config.json

# Give permission
sudo usermod -a -G dialout $USER
# Then logout and login
```

### "Connection error - cannot reach API"

For localhost:
```bash
# Check if app is running
curl http://localhost:5000

# Start app
dotnet run
```

For VPS:
```bash
# Check connectivity
ping your-domain.com
curl -I https://your-domain.com

# Check DNS
nslookup your-domain.com
```

### "Invalid API key"

```bash
# Make sure keys match:
# 1. On VPS: appsettings.Production.json -> CashPayment:ApiKey
# 2. On Pi: cash_reader_config.json -> api_key

# Disable temporarily for testing:
# Set both to null
```

---

## Documentation

| Document | Purpose |
|----------|---------|
| **CASH_PAYMENT_README.md** | Overview and features |
| **CASH_PAYMENT_SETUP.md** | Detailed setup guide |
| **CASH_PAYMENT_VPS_DEPLOYMENT.md** | VPS + home Pi deployment (⭐ if using cloud VPS) |
| **CASH_PAYMENT_TESTING.md** | Testing procedures |
| **ARDUINO_PROTOCOL.md** | Serial communication protocol |
| **CASH_PAYMENT_QUICKSTART.md** | This file |

---

## Need Help?

1. Check logs:
   ```bash
   # View Python script logs
   tail -f cash_reader.log
   
   # View service logs (if using systemd)
   sudo journalctl -u cash-reader -f
   
   # View app logs
   sudo journalctl -u restaurant-kiosk -f
   ```

2. Test connectivity:
   ```bash
   # Test VPS API
   curl https://your-domain.com/api/cash-payment/status/TEST
   ```

3. Read detailed guides linked above

---

## Summary: Which Guide Do I Need?

- 🏠 **Testing locally?** → Use this quickstart (Scenario 1)
- 🥧 **Everything on one Pi?** → Use this quickstart (Scenario 2)  
- ☁️ **App in cloud, Arduino at home?** → Use this quickstart (Scenario 3) + [CASH_PAYMENT_VPS_DEPLOYMENT.md](CASH_PAYMENT_VPS_DEPLOYMENT.md)
- 📚 **Need details?** → See [CASH_PAYMENT_SETUP.md](CASH_PAYMENT_SETUP.md)

---

**Pro Tip:** Dynamic home IP is NOT a problem for VPS deployment! The Python script makes outgoing connections to the VPS, which works perfectly through NAT. ✅

