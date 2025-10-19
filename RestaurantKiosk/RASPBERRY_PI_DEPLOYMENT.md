# Raspberry Pi Deployment Guide

This guide walks you through deploying the Restaurant Kiosk application to a Raspberry Pi 4 or 5.

> **🚀 Quick Start:** For rapid deployment, see [deployment/QUICK_START.md](deployment/QUICK_START.md)  
> **📋 Checklist:** Use [deployment/DEPLOYMENT_CHECKLIST.md](deployment/DEPLOYMENT_CHECKLIST.md) to track progress  
> **🤖 Automated:** Use PowerShell script [deployment/Deploy-ToPi.ps1](deployment/Deploy-ToPi.ps1) for one-command deployment

## Table of Contents
1. [Hardware Requirements](#hardware-requirements)
2. [Raspberry Pi Setup](#raspberry-pi-setup)
3. [Install Prerequisites](#install-prerequisites)
4. [Database Setup](#database-setup)
5. [Build and Deploy Application](#build-and-deploy-application)
6. [Configure Application](#configure-application)
7. [Arduino Integration](#arduino-integration)
8. [Auto-Start Configuration](#auto-start-configuration)
9. [Kiosk Mode Setup](#kiosk-mode-setup)
10. [Troubleshooting](#troubleshooting)

---

## Hardware Requirements

### Recommended
- **Raspberry Pi 4 (4GB RAM) or Raspberry Pi 5 (8GB RAM)** - Minimum 4GB RAM
- **32GB+ MicroSD Card** (Class 10 or better) or SSD via USB
- **Official Raspberry Pi Power Supply** (5V 3A for Pi 4, 5V 5A for Pi 5)
- **Touchscreen Display** (7" or 10" official Raspberry Pi touchscreen recommended)
- **Arduino** (for cash payment integration)
- **Cooling** (Heatsinks or fan case recommended)
- **Ethernet cable** or WiFi connection

### Optional
- **USB Keyboard & Mouse** (for initial setup)
- **HDMI Cable** (if not using touchscreen)

---

## Raspberry Pi Setup

### 1. Install Raspberry Pi OS

Use **Raspberry Pi Imager** to install the OS:

```bash
# Download from: https://www.raspberrypi.com/software/

# Choose OS: Raspberry Pi OS (64-bit) - Debian Bookworm
# Recommended: "Raspberry Pi OS with Desktop" for kiosk mode
```

**Configuration in Imager:**
- Set hostname: `restaurant-kiosk`
- Enable SSH
- Set username and password
- Configure WiFi (if needed)
- Set locale settings

### 2. First Boot Configuration

```bash
# SSH into your Pi
ssh pi@restaurant-kiosk.local

# Update system
sudo apt update && sudo apt upgrade -y

# Install basic tools
sudo apt install -y git curl wget vim nano htop
```

### 3. Performance Optimization

```bash
# Edit boot config
sudo nano /boot/firmware/config.txt

# Add these lines for better performance:
# over_voltage=2
# arm_freq=2000
# gpu_mem=256

# Reboot
sudo reboot
```

---

## Install Prerequisites

### 1. Install .NET 9 Runtime

```bash
# Download .NET 9 installation script
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh

# Install .NET 9 Runtime (ARM64)
./dotnet-install.sh --channel 9.0 --runtime aspnetcore

# Add to PATH
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc

# Verify installation
dotnet --list-runtimes
```

### 2. Install PostgreSQL

```bash
# Install PostgreSQL
sudo apt install -y postgresql postgresql-contrib

# Start and enable PostgreSQL
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Create database and user
sudo -u postgres psql << EOF
CREATE DATABASE restaurant_kiosk;
CREATE USER kiosk_user WITH PASSWORD 'YourSecurePassword123!';
GRANT ALL PRIVILEGES ON DATABASE restaurant_kiosk TO kiosk_user;
\c restaurant_kiosk
GRANT ALL ON SCHEMA public TO kiosk_user;
EOF
```

### 3. Install Python (for Arduino integration)

```bash
# Python should be pre-installed, verify
python3 --version

# Install pip and required packages
sudo apt install -y python3-pip
pip3 install pyserial --break-system-packages

# Or use virtual environment (recommended)
sudo apt install -y python3-venv
python3 -m venv ~/arduino-env
source ~/arduino-env/bin/activate
pip install pyserial
```

### 4. Install Nginx (Reverse Proxy)

```bash
# Install Nginx
sudo apt install -y nginx

# Start and enable
sudo systemctl start nginx
sudo systemctl enable nginx
```

---

## Build and Deploy Application

### Option 1: Build on Development Machine (Recommended)

On your **development Windows machine**:

```powershell
# Navigate to project directory
cd RestaurantKiosk

# Publish for Linux ARM64
dotnet publish -c Release -r linux-arm64 --self-contained false -o ./publish

# Create deployment package
Compress-Archive -Path ./publish/* -DestinationPath restaurant-kiosk.zip
```

**Transfer to Raspberry Pi:**

```powershell
# Using SCP (if you have OpenSSH)
scp restaurant-kiosk.zip pi@restaurant-kiosk.local:~/

# Or use WinSCP or FileZilla
```

On **Raspberry Pi**:

```bash
# Create application directory
sudo mkdir -p /var/www/restaurant-kiosk
cd ~

# Extract application
unzip restaurant-kiosk.zip -d ~/app-temp
sudo mv ~/app-temp/* /var/www/restaurant-kiosk/
sudo chown -R pi:pi /var/www/restaurant-kiosk

# Make executable
chmod +x /var/www/restaurant-kiosk/RestaurantKiosk
```

### Option 2: Build on Raspberry Pi

```bash
# Clone repository
cd ~
git clone <your-repository-url>
cd RestaurantKiosk/RestaurantKiosk

# Restore and publish
dotnet publish -c Release -o /var/www/restaurant-kiosk
chmod +x /var/www/restaurant-kiosk/RestaurantKiosk
```

---

## Configure Application

### 1. Create Production Configuration

```bash
# Create appsettings.Production.json
sudo nano /var/www/restaurant-kiosk/appsettings.Production.json
```

**Content:**

```json
{
  "BaseUrl": "http://localhost:5000",
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=5432;Database=restaurant_kiosk;User Id=kiosk_user;Password=YourSecurePassword123!;"
  },
  "Xendit": {
    "ApiKey": "your-production-api-key",
    "WebhookToken": "your-webhook-token",
    "IsSandbox": false,
    "BaseUrl": "https://api.xendit.co"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    }
  }
}
```

### 2. Run Database Migrations

```bash
cd /var/www/restaurant-kiosk

# Set environment
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=5432;Database=restaurant_kiosk;User Id=kiosk_user;Password=YourSecurePassword123!;"

# If you have the EF Core tools installed on the Pi
dotnet ef database update

# Or, apply migrations programmatically by modifying Program.cs
# See migration section below
```

**Alternative: Auto-migrate on startup**

Modify `Program.cs` to add before `app.Run()`:

```csharp
// Auto-apply migrations on startup (use carefully in production)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}
```

---

## Arduino Integration

### 1. Upload Arduino Sketch

```bash
# Install Arduino CLI on Pi (optional)
curl -fsSL https://raw.githubusercontent.com/arduino/arduino-cli/master/install.sh | sh

# Or upload from your development machine using Arduino IDE
```

### 2. Find Arduino Port

```bash
# List USB devices
ls /dev/tty*

# Usually: /dev/ttyACM0 or /dev/ttyUSB0

# Add user to dialout group for serial access
sudo usermod -a -G dialout pi
# Logout and login again for changes to take effect
```

### 3. Test Arduino Connection

```bash
# Test with Python script
cd /var/www/restaurant-kiosk
python3 arduino_cash_reader.py
```

---

## Auto-Start Configuration

### 1. Create Systemd Service

```bash
sudo nano /etc/systemd/system/restaurant-kiosk.service
```

**Content:**

```ini
[Unit]
Description=Restaurant Kiosk Application
After=network.target postgresql.service

[Service]
Type=notify
User=pi
WorkingDirectory=/var/www/restaurant-kiosk
ExecStart=/home/pi/.dotnet/dotnet /var/www/restaurant-kiosk/RestaurantKiosk.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=restaurant-kiosk
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=/home/pi/.dotnet

[Install]
WantedBy=multi-user.target
```

**Enable and Start:**

```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service
sudo systemctl enable restaurant-kiosk.service

# Start service
sudo systemctl start restaurant-kiosk.service

# Check status
sudo systemctl status restaurant-kiosk.service

# View logs
sudo journalctl -u restaurant-kiosk.service -f
```

### 2. Create Arduino Cash Reader Service

```bash
sudo nano /etc/systemd/system/arduino-cash-reader.service
```

**Content:**

```ini
[Unit]
Description=Arduino Cash Reader Service
After=restaurant-kiosk.service

[Service]
Type=simple
User=pi
WorkingDirectory=/var/www/restaurant-kiosk
ExecStart=/usr/bin/python3 /var/www/restaurant-kiosk/arduino_cash_reader.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

**Enable and Start:**

```bash
sudo systemctl daemon-reload
sudo systemctl enable arduino-cash-reader.service
sudo systemctl start arduino-cash-reader.service
sudo systemctl status arduino-cash-reader.service
```

---

## Kiosk Mode Setup

### 1. Install Chromium Browser

```bash
sudo apt install -y chromium-browser unclutter xdotool
```

### 2. Create Kiosk Start Script

```bash
nano ~/kiosk-start.sh
```

**Content:**

```bash
#!/bin/bash

# Wait for application to start
sleep 10

# Hide cursor
unclutter -idle 0.5 -root &

# Disable screen blanking
xset s off
xset -dpms
xset s noblank

# Start Chromium in kiosk mode
chromium-browser --noerrdialogs \
  --disable-infobars \
  --kiosk \
  --incognito \
  --disable-session-crashed-bubble \
  --disable-restore-session-state \
  --check-for-update-interval=31536000 \
  --app=http://localhost:5000/kioskstart
```

**Make executable:**

```bash
chmod +x ~/kiosk-start.sh
```

### 3. Auto-Start Kiosk on Boot

```bash
# Create autostart directory if it doesn't exist
mkdir -p ~/.config/lxsession/LXDE-pi

# Edit autostart file
nano ~/.config/lxsession/LXDE-pi/autostart
```

**Add these lines:**

```
@lxpanel --profile LXDE-pi
@pcmanfm --desktop --profile LXDE-pi
@xscreensaver -no-splash
@point-rpi
@/home/pi/kiosk-start.sh
```

### 4. Disable Sleep and Screensaver

```bash
# Edit lightdm config
sudo nano /etc/lightdm/lightdm.conf

# Add under [Seat:*]
xserver-command=X -s 0 -dpms
```

### 5. Auto-Login (Optional)

```bash
sudo raspi-config

# Navigate to:
# System Options -> Boot / Auto Login -> Desktop Autologin
```

---

## Configure Nginx (Optional - for HTTPS)

### 1. Configure Reverse Proxy

```bash
sudo nano /etc/nginx/sites-available/restaurant-kiosk
```

**Content:**

```nginx
upstream kiosk_backend {
    server 127.0.0.1:5000;
}

map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen 80;
    server_name restaurant-kiosk.local;

    client_max_body_size 10M;

    location / {
        proxy_pass http://kiosk_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Enable site:**

```bash
sudo ln -s /etc/nginx/sites-available/restaurant-kiosk /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

---

## Monitoring and Maintenance

### 1. View Application Logs

```bash
# Real-time logs
sudo journalctl -u restaurant-kiosk.service -f

# Last 100 lines
sudo journalctl -u restaurant-kiosk.service -n 100

# Logs from today
sudo journalctl -u restaurant-kiosk.service --since today
```

### 2. Restart Services

```bash
# Restart application
sudo systemctl restart restaurant-kiosk.service

# Restart Arduino reader
sudo systemctl restart arduino-cash-reader.service

# Restart both
sudo systemctl restart restaurant-kiosk.service arduino-cash-reader.service
```

### 3. Update Application

```bash
# Stop services
sudo systemctl stop restaurant-kiosk.service arduino-cash-reader.service

# Backup current version
sudo cp -r /var/www/restaurant-kiosk /var/www/restaurant-kiosk.backup

# Upload new version (from development machine)
# Then extract to /var/www/restaurant-kiosk

# Start services
sudo systemctl start restaurant-kiosk.service arduino-cash-reader.service
```

### 4. Database Backup

```bash
# Create backup script
nano ~/backup-db.sh
```

**Content:**

```bash
#!/bin/bash
BACKUP_DIR="/home/pi/backups"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $BACKUP_DIR
pg_dump -U kiosk_user -h localhost restaurant_kiosk > $BACKUP_DIR/backup_$DATE.sql
```

**Schedule daily backups:**

```bash
chmod +x ~/backup-db.sh
crontab -e

# Add this line (backup at 2 AM daily)
0 2 * * * /home/pi/backup-db.sh
```

---

## Troubleshooting

### Application Won't Start

```bash
# Check service status
sudo systemctl status restaurant-kiosk.service

# Check logs
sudo journalctl -u restaurant-kiosk.service -n 50

# Check if port is in use
sudo netstat -tulpn | grep :5000

# Test manually
cd /var/www/restaurant-kiosk
ASPNETCORE_ENVIRONMENT=Production /home/pi/.dotnet/dotnet RestaurantKiosk.dll
```

### Database Connection Issues

```bash
# Test PostgreSQL connection
psql -U kiosk_user -h localhost -d restaurant_kiosk

# Check PostgreSQL status
sudo systemctl status postgresql

# Check PostgreSQL logs
sudo journalctl -u postgresql -n 50
```

### Touchscreen Not Working

```bash
# Check if touchscreen is detected
sudo apt install -y xinput
DISPLAY=:0 xinput list

# Calibrate touchscreen
sudo apt install -y xinput-calibrator
DISPLAY=:0 xinput_calibrator
```

### Arduino Connection Issues

```bash
# Check if Arduino is connected
ls -l /dev/ttyACM*

# Check permissions
groups pi  # Should include 'dialout'

# Test serial connection
sudo apt install -y screen
screen /dev/ttyACM0 9600
```

### Performance Issues

```bash
# Check CPU temperature
vcgencmd measure_temp

# Monitor resources
htop

# Check memory
free -h

# Increase swap if needed (for 4GB Pi)
sudo dphys-swapfile swapoff
sudo nano /etc/dphys-swapfile
# Set: CONF_SWAPSIZE=2048
sudo dphys-swapfile setup
sudo dphys-swapfile swapon
```

### Browser Not Starting in Kiosk Mode

```bash
# Check if X server is running
echo $DISPLAY  # Should be :0

# Manually test kiosk script
DISPLAY=:0 ~/kiosk-start.sh

# Check autostart
cat ~/.config/lxsession/LXDE-pi/autostart
```

---

## Security Best Practices

### 1. Change Default Passwords

```bash
# Change Pi user password
passwd

# Change PostgreSQL password
sudo -u postgres psql
ALTER USER kiosk_user WITH PASSWORD 'NewSecurePassword123!';
```

### 2. Configure Firewall

```bash
# Install UFW
sudo apt install -y ufw

# Allow SSH (if needed)
sudo ufw allow 22/tcp

# Allow HTTP
sudo ufw allow 80/tcp

# Allow HTTPS (if configured)
sudo ufw allow 443/tcp

# Enable firewall
sudo ufw enable
```

### 3. Disable Unnecessary Services

```bash
# Disable Bluetooth (if not needed)
sudo systemctl disable bluetooth

# Disable WiFi (if using Ethernet)
sudo rfkill block wifi
```

### 4. Keep System Updated

```bash
# Create update script
nano ~/update-system.sh
```

**Content:**

```bash
#!/bin/bash
sudo apt update
sudo apt upgrade -y
sudo apt autoremove -y
sudo apt autoclean
```

**Schedule monthly updates:**

```bash
chmod +x ~/update-system.sh
sudo crontab -e

# Add (first day of month at 3 AM)
0 3 1 * * /home/pi/update-system.sh
```

---

## Quick Reference Commands

```bash
# Service Management
sudo systemctl status restaurant-kiosk
sudo systemctl start restaurant-kiosk
sudo systemctl stop restaurant-kiosk
sudo systemctl restart restaurant-kiosk

# Logs
sudo journalctl -u restaurant-kiosk -f
sudo journalctl -u arduino-cash-reader -f

# Database
psql -U kiosk_user -h localhost -d restaurant_kiosk
pg_dump -U kiosk_user restaurant_kiosk > backup.sql

# System Info
vcgencmd measure_temp
free -h
df -h
htop

# Reboot
sudo reboot
```

---

## Performance Tips

1. **Use SSD instead of SD Card** - Dramatically improves database performance
2. **Enable ZRAM** - Better memory compression
3. **Optimize PostgreSQL** - Tune postgresql.conf for Raspberry Pi
4. **Disable GUI** - If you're using remote kiosk display
5. **Use lightweight window manager** - Consider Openbox instead of LXDE
6. **Reduce logging** - In production, set log level to Warning

---

## Next Steps

1. Test all features thoroughly on the Raspberry Pi
2. Set up remote monitoring (e.g., with Grafana)
3. Configure automatic updates
4. Set up offsite database backups
5. Document your specific kiosk hardware setup
6. Create disaster recovery procedures

For additional help, refer to:
- [LOCAL_DEVELOPMENT_SETUP.md](./LOCAL_DEVELOPMENT_SETUP.md)
- [CASH_PAYMENT_SETUP.md](./CASH_PAYMENT_SETUP.md)
- [ORDER_MANAGEMENT_SETUP.md](./ORDER_MANAGEMENT_SETUP.md)

