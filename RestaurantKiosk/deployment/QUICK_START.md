# Quick Start Guide - Raspberry Pi Deployment

This is a condensed guide for quickly deploying the Restaurant Kiosk to Raspberry Pi.

> ⚠️ **IMPORTANT for Xendit Payments:** This kiosk requires internet access with a public domain for payment callbacks. After basic deployment, see step 5 for production internet setup.

## Prerequisites

### Hardware
- Raspberry Pi 4 (4GB+) or Pi 5
- 32GB+ MicroSD card or SSD
- Power supply
- Touchscreen or monitor

### Software
- Fresh Raspberry Pi OS (64-bit) installed
- Network connection to Pi
- SSH enabled

## 5-Minute Setup

### 1. Initial Pi Setup (One Time)

**Transfer and run setup script:**

```bash
# From Windows PowerShell (on your dev machine)
scp deployment\setup-pi.sh pi@restaurant-kiosk.local:~/

# Connect to Pi
ssh pi@restaurant-kiosk.local

# Run setup
chmod +x setup-pi.sh
./setup-pi.sh

# Logout and login again
logout
```

**What this does:**
- Installs .NET 9
- Installs PostgreSQL
- Creates database
- Installs dependencies

⏱️ **Time: ~10 minutes**

### 2. Install Services (One Time)

```bash
# Transfer service script
scp deployment\install-services.sh pi@restaurant-kiosk.local:~/

# On Pi
ssh pi@restaurant-kiosk.local
chmod +x install-services.sh
./install-services.sh
```

⏱️ **Time: ~1 minute**

### 3. Deploy Application

**Option A: Using PowerShell Script (Recommended)**

```powershell
# From your Windows dev machine
cd RestaurantKiosk
.\deployment\Deploy-ToPi.ps1

# Or specify custom host
.\deployment\Deploy-ToPi.ps1 -PiHost "192.168.1.100" -PiUser "pi"
```

⏱️ **Time: ~3-5 minutes**

**Option B: Manual Deployment**

```powershell
# Build
dotnet publish -c Release -r linux-arm64 --self-contained false -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath restaurant-kiosk.zip -Force

# Transfer
scp restaurant-kiosk.zip pi@restaurant-kiosk.local:~/

# Deploy
ssh pi@restaurant-kiosk.local
./deploy.sh
```

### 4. Verify Deployment

```bash
# Check if running
sudo systemctl status restaurant-kiosk

# View logs
sudo journalctl -u restaurant-kiosk -f

# Access from browser
# http://raspberry-pi-ip:5000
```

### 5. Setup Internet Access for Production (Required for Xendit)

⚠️ **This step is REQUIRED for Xendit payment callbacks to work**

```bash
# Transfer script
scp deployment\setup-cloudflare-tunnel.sh pi@restaurant-kiosk.local:~/

# On Raspberry Pi
ssh pi@restaurant-kiosk.local
chmod +x setup-cloudflare-tunnel.sh
./setup-cloudflare-tunnel.sh

# Follow prompts to:
# - Enter your domain name
# - Authenticate with Cloudflare
# - Configure tunnel
```

**What you need before this step:**
- [ ] Domain name registered (e.g., from Namecheap, Google Domains)
- [ ] Cloudflare free account created
- [ ] Domain added to Cloudflare
- [ ] Nameservers updated to Cloudflare

**See [PRODUCTION_INTERNET_SETUP.md](../PRODUCTION_INTERNET_SETUP.md) for detailed instructions**

⏱️ **Time: ~20 minutes**

**After setup:**
- Your app will be accessible at `https://your-domain.com`
- Xendit callbacks will work
- Automatic HTTPS enabled
- No router configuration needed

## Configuration Checklist

After deployment, configure:

### 1. Database Connection

```bash
sudo nano /var/www/restaurant-kiosk/appsettings.Production.json
```

Update:
- ✅ Connection string (done by setup script)
- ⚠️ Xendit API keys (production keys)
- ⚠️ Base URL (if using domain)

### 2. Run Migrations

```bash
cd /var/www/restaurant-kiosk
export ASPNETCORE_ENVIRONMENT=Production

# Apply migrations
# (Make sure connection string is correct)
```

Or configure auto-migration in `Program.cs`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}
```

### 3. Configure Domain and Xendit (Required for Production)

After completing step 5 (Cloudflare Tunnel setup):

```bash
sudo nano /var/www/restaurant-kiosk/appsettings.Production.json
```

Update these values:

```json
{
  "BaseUrl": "https://your-domain.com",
  "Xendit": {
    "ApiKey": "xnd_production_YOUR_ACTUAL_KEY",
    "WebhookToken": "YOUR_ACTUAL_WEBHOOK_TOKEN",
    "IsSandbox": false,
    "CallbackUrl": "https://your-domain.com/api/payment/callback"
  }
}
```

**Then configure Xendit Dashboard:**

1. Login to [Xendit Dashboard](https://dashboard.xendit.co)
2. Navigate to Settings → Webhooks
3. Click "Add Webhook URL"
4. Enter: `https://your-domain.com/api/payment/callback`
5. Select event types (at minimum: payment.paid, payment.expired)
6. Save

**Restart application:**

```bash
sudo systemctl restart restaurant-kiosk
```

**Test the webhook:**

```bash
# From external network or Xendit dashboard
curl -X POST https://your-domain.com/api/payment/callback \
  -H "Content-Type: application/json" \
  -H "x-callback-token: YOUR_WEBHOOK_TOKEN" \
  -d '{"id": "test-123", "status": "COMPLETED"}'

# Check logs to verify receipt
sudo journalctl -u restaurant-kiosk -f | grep -i callback
```

### 4. Arduino Setup (If Using Cash Payment)

```bash
# Find Arduino port
ls /dev/ttyACM*

# Update script if needed
nano /var/www/restaurant-kiosk/arduino_cash_reader.py

# Test
python3 /var/www/restaurant-kiosk/arduino_cash_reader.py
```

### 5. Kiosk Mode (Optional)

```bash
# Create kiosk script
nano ~/kiosk-start.sh
```

Add:
```bash
#!/bin/bash
sleep 10
unclutter -idle 0.5 -root &
xset s off
xset -dpms
xset s noblank
chromium-browser --noerrdialogs --disable-infobars --kiosk --incognito --app=http://localhost:5000/kioskstart
```

Enable:
```bash
chmod +x ~/kiosk-start.sh
mkdir -p ~/.config/lxsession/LXDE-pi
nano ~/.config/lxsession/LXDE-pi/autostart
```

Add line:
```
@/home/pi/kiosk-start.sh
```

## Quick Commands

### Service Management
```bash
sudo systemctl start restaurant-kiosk     # Start
sudo systemctl stop restaurant-kiosk      # Stop
sudo systemctl restart restaurant-kiosk   # Restart
sudo systemctl status restaurant-kiosk    # Status
```

### Logs
```bash
sudo journalctl -u restaurant-kiosk -f           # Live logs
sudo journalctl -u restaurant-kiosk -n 100       # Last 100 lines
sudo journalctl -u restaurant-kiosk --since today # Today's logs
```

### Database
```bash
# Connect
psql -U kiosk_user -h localhost -d restaurant_kiosk

# Backup
pg_dump -U kiosk_user restaurant_kiosk > backup.sql

# Restore
psql -U kiosk_user -h localhost -d restaurant_kiosk < backup.sql
```

### System
```bash
vcgencmd measure_temp  # Check temperature
htop                   # Resource monitor
df -h                  # Disk space
free -h                # Memory
sudo reboot            # Reboot
```

## Updates

To update the application:

```powershell
# From Windows dev machine
.\deployment\Deploy-ToPi.ps1
```

That's it! The script handles:
- ✅ Building
- ✅ Packaging
- ✅ Transferring
- ✅ Stopping services
- ✅ Backing up
- ✅ Deploying
- ✅ Starting services

## Troubleshooting

### App Won't Start

```bash
# Check logs
sudo journalctl -u restaurant-kiosk -n 50

# Test manually
cd /var/www/restaurant-kiosk
ASPNETCORE_ENVIRONMENT=Production ~/.dotnet/dotnet RestaurantKiosk.dll
```

### Database Issues

```bash
# Test connection
psql -U kiosk_user -h localhost -d restaurant_kiosk

# Check PostgreSQL
sudo systemctl status postgresql
```

### Port Already in Use

```bash
# Check what's using port 5000
sudo netstat -tulpn | grep :5000

# Kill process if needed
sudo kill -9 <PID>
```

### Permission Denied

```bash
# Fix ownership
sudo chown -R pi:pi /var/www/restaurant-kiosk

# Fix executable
chmod +x /var/www/restaurant-kiosk/RestaurantKiosk
```

### Can't Connect via SSH

```bash
# Find Pi IP
# From Pi directly:
hostname -I

# From Windows (if on same network):
ping restaurant-kiosk.local

# Connect with IP
ssh pi@192.168.1.100
```

## Default Credentials

After fresh setup:

**Raspberry Pi:**
- User: `pi`
- Password: (set during OS installation)

**Database:**
- Database: `restaurant_kiosk`
- User: `kiosk_user`
- Password: (set during setup script)
- Port: `5432`

**Application:**
- URL: `http://raspberry-pi-ip:5000`
- Admin: (configure in application)

## Security Notes

⚠️ Before production:

1. **Change default passwords**
   ```bash
   passwd  # Pi user
   sudo -u postgres psql
   ALTER USER kiosk_user WITH PASSWORD 'NewPassword';
   ```

2. **Configure firewall**
   ```bash
   sudo apt install ufw
   sudo ufw allow 22    # SSH
   sudo ufw allow 80    # HTTP
   sudo ufw enable
   ```

3. **Use SSH keys**
   ```powershell
   ssh-keygen -t rsa
   ssh-copy-id pi@restaurant-kiosk.local
   ```

4. **Update Xendit keys** in `appsettings.Production.json`

5. **Set up HTTPS** with Nginx + Let's Encrypt

## Performance Tips

For better performance:

1. **Use SSD instead of SD card**
2. **Increase swap** (if using 4GB Pi)
   ```bash
   sudo dphys-swapfile swapoff
   sudo nano /etc/dphys-swapfile
   # Set: CONF_SWAPSIZE=2048
   sudo dphys-swapfile setup
   sudo dphys-swapfile swapon
   ```

3. **Add cooling** (heatsink or fan)
4. **Overclock** (optional, in `/boot/firmware/config.txt`)

## Next Steps

- [ ] **Setup internet access with domain (REQUIRED for payments)** - See step 5 above
- [ ] Configure production Xendit keys
- [ ] Configure Xendit webhook URL
- [ ] Test payment callbacks
- [ ] Set up database backups
- [ ] Configure kiosk mode
- [ ] Set up remote monitoring
- [ ] Test all features on Pi
- [ ] Create admin accounts
- [ ] Add products and categories
- [ ] Test payment flows end-to-end
- [ ] Configure touchscreen
- [ ] Test Arduino integration (if applicable)

## Support

For detailed information:
- [RASPBERRY_PI_DEPLOYMENT.md](../RASPBERRY_PI_DEPLOYMENT.md) - Full deployment guide
- [README.md](./README.md) - Deployment scripts documentation
- [CASH_PAYMENT_SETUP.md](../CASH_PAYMENT_SETUP.md) - Arduino setup

## Common Issues

| Issue | Solution |
|-------|----------|
| Service won't start | Check logs: `sudo journalctl -u restaurant-kiosk -n 50` |
| Can't connect to database | Verify connection string in `appsettings.Production.json` |
| .NET not found | Run `setup-pi.sh` again |
| Port 5000 in use | Change port in appsettings or kill process |
| Touchscreen not working | Install xinput-calibrator and calibrate |
| Arduino not detected | Check `/dev/ttyACM0` and user in `dialout` group |
| High CPU usage | Check for infinite loops in logs |
| Out of memory | Increase swap or upgrade to 8GB Pi |

---

## Production Deployment Summary

**For Development/Testing (Local Only):**
- Time: ~15-20 minutes
- No domain needed
- Access via `http://raspberry-pi-ip:5000`
- Xendit payments won't work (no callbacks)

**For Production (With Payments):**
- Time: ~35-40 minutes (includes internet setup)
- Domain required (~$10/year)
- Cloudflare account (free)
- Access via `https://your-domain.com`
- ✅ Xendit payments fully functional

---

**Estimated Total Time:** 
- Development: 15-20 minutes ⚡
- Production: 35-40 minutes 🚀

