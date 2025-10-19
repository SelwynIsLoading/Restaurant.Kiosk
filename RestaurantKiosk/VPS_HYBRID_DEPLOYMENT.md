# VPS Hybrid Deployment - Cloud Backend with Local Hardware

This guide covers deploying the Restaurant Kiosk with the application hosted on a VPS while maintaining local hardware integration (touchscreen, Arduino, printer).

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                         CLOUD (VPS)                          │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Restaurant Kiosk Application (ASP.NET Core)           │ │
│  │  - Database (PostgreSQL)                               │ │
│  │  - Business Logic                                      │ │
│  │  - Payment Processing (Xendit)                         │ │
│  │  - Admin Panel                                         │ │
│  │  - SignalR Hub                                         │ │
│  └────────────────────────────────────────────────────────┘ │
│                            ↕                                 │
│                    (HTTPS/WebSocket)                         │
└─────────────────────────────────────────────────────────────┘
                             ↕
┌─────────────────────────────────────────────────────────────┐
│                   LOCAL (Raspberry Pi)                       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Hardware Interface Service                            │ │
│  │  - Chromium Kiosk Browser (displays web UI)           │ │
│  │  - Arduino Cash Acceptor Interface                    │ │
│  │  - Touchscreen Input                                   │ │
│  │  - Receipt Printer (optional)                          │ │
│  │  - SignalR Client (real-time sync)                    │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Benefits of This Architecture

### ✅ Advantages

1. **Centralized Management**
   - Single codebase deployment
   - One database for all locations
   - Centralized monitoring and updates

2. **Better Performance**
   - Dedicated VPS resources
   - Better database performance
   - Faster for complex queries

3. **Scalability**
   - Easy to add more kiosk locations
   - Each location needs only a Raspberry Pi
   - VPS can be upgraded as needed

4. **Reliability**
   - 99.9%+ uptime (VPS provider SLA)
   - Automatic backups
   - Professional infrastructure

5. **Cost Effective**
   - One VPS serves multiple kiosks
   - Cheaper than upgrading all Raspberry Pis
   - Predictable monthly costs

6. **Easier Xendit Integration**
   - VPS has static IP
   - Simple domain setup
   - No dynamic DNS needed

### ⚠️ Considerations

1. **Internet Dependency**
   - Kiosk won't work without internet
   - Need reliable internet connection
   - Consider offline mode for critical features

2. **Latency**
   - Slight delay for API calls
   - Hardware interactions still local (fast)
   - Usually not noticeable with good connection

3. **Complexity**
   - Two components to maintain
   - Need proper communication setup
   - More moving parts

---

## Deployment Architecture

### Option 1: Thin Client (Recommended)

**How it works:**
- VPS hosts the full web application
- Raspberry Pi runs browser in kiosk mode
- Hardware services run locally on Pi
- Communication via SignalR for real-time events

**Best for:** Most use cases

### Option 2: Hybrid with Local API Gateway

**How it works:**
- VPS hosts main application
- Raspberry Pi runs local API gateway
- Hardware interfaces through local API
- Local API communicates with VPS

**Best for:** Complex hardware integration, offline support needed

---

## Option 1: Thin Client Setup (Recommended)

### VPS Deployment

#### Step 1: Choose VPS Provider

Recommended providers for ASP.NET Core:

| Provider | Specs | Price | Location |
|----------|-------|-------|----------|
| **Hetzner** | 2GB RAM, 1 vCPU, 40GB SSD | €4.5/mo (~$5) | Europe, USA |
| **DigitalOcean** | 2GB RAM, 1 vCPU, 50GB SSD | $12/mo | Global |
| **Vultr** | 2GB RAM, 1 vCPU, 55GB SSD | $12/mo | Global |
| **Linode** | 2GB RAM, 1 vCPU, 50GB SSD | $12/mo | Global |
| **Contabo** | 4GB RAM, 2 vCPU, 100GB SSD | €5/mo (~$5.50) | Europe, USA |

**Recommended:** Hetzner or Contabo for best value

#### Step 2: Create VPS

```bash
# Choose:
- OS: Ubuntu 22.04 LTS (64-bit)
- Location: Closest to your kiosk location
- SSH Key: Add your public key
```

#### Step 3: Initial VPS Setup

```bash
# SSH to your VPS
ssh root@your-vps-ip

# Update system
apt update && apt upgrade -y

# Create non-root user
adduser kiosk
usermod -aG sudo kiosk

# Switch to new user
su - kiosk
```

#### Step 4: Install Prerequisites

```bash
# Install .NET 9 Runtime
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime aspnetcore

# Add to PATH
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc

# Install PostgreSQL
sudo apt install -y postgresql postgresql-contrib

# Install Nginx
sudo apt install -y nginx certbot python3-certbot-nginx

# Install tools
sudo apt install -y git curl wget htop
```

#### Step 5: Setup Database

```bash
# Create database
sudo -u postgres psql << EOF
CREATE DATABASE restaurant_kiosk;
CREATE USER kiosk_user WITH PASSWORD 'RestaurantKiosk!123';
GRANT ALL PRIVILEGES ON DATABASE restaurant_kiosk TO kiosk_user;
\c restaurant_kiosk
GRANT ALL ON SCHEMA public TO kiosk_user;
EOF
```

#### Step 6: Deploy Application

**From your development machine:**

```powershell
# Build for linux-x64 (VPS)
cd RestaurantKiosk
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish-vps

# Create deployment package
Compress-Archive -Path ./publish-vps/* -DestinationPath restaurant-kiosk-vps.zip

# Transfer to VPS
scp restaurant-kiosk-vps.zip kiosk@your-vps-ip:~/
```

**On VPS:**

```bash
# Create application directory
sudo mkdir -p /var/www/restaurant-kiosk
cd ~

# Extract
unzip restaurant-kiosk-vps.zip -d ~/app-temp
sudo mv ~/app-temp/* /var/www/restaurant-kiosk/
sudo chown -R kiosk:kiosk /var/www/restaurant-kiosk

# Make executable
chmod +x /var/www/restaurant-kiosk/RestaurantKiosk
```

#### Step 7: Configure Application

```bash
sudo nano /var/www/restaurant-kiosk/appsettings.Production.json
```

```json
{
  "BaseUrl": "https://your-domain.com",
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=5432;Database=restaurant_kiosk;User Id=kiosk_user;Password=YourSecurePassword123!;"
  },
  "Xendit": {
    "ApiKey": "xnd_production_YOUR_KEY",
    "WebhookToken": "YOUR_WEBHOOK_TOKEN",
    "IsSandbox": false,
    "BaseUrl": "https://api.xendit.co",
    "CallbackUrl": "https://your-domain.com/api/payment/callback"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://127.0.0.1:5000"
      }
    }
  },
  "AllowedHosts": "*"
}
```

#### Step 8: Create Systemd Service

```bash
sudo nano /etc/systemd/system/restaurant-kiosk.service
```

```ini
[Unit]
Description=Restaurant Kiosk Application
After=network.target postgresql.service

[Service]
Type=notify
User=kiosk
WorkingDirectory=/root/kiosk
ExecStart=/home/kiosk/.dotnet/dotnet /root/kiosk/RestaurantKiosk.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=restaurant-kiosk
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=/home/kiosk/.dotnet

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable restaurant-kiosk
sudo systemctl start restaurant-kiosk
```

#### Step 9: Configure Nginx

```bash
sudo nano /etc/nginx/sites-available/restaurant-kiosk
```

```nginx
upstream kiosk_backend {
    server 127.0.0.1:5000;
    keepalive 32;
}

map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen 80;
    server_name your-domain.com www.your-domain.com;
    
    # Redirect to HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name your-domain.com www.your-domain.com;

    # SSL certificates (will be configured by certbot)
    ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;
    
    # SSL configuration
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers on;
    ssl_ciphers ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256;
    
    client_max_body_size 10M;
    
    # Gzip compression
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;

    location / {
        proxy_pass http://kiosk_backend;
        proxy_http_version 1.1;
        
        # WebSocket support (for SignalR)
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Real-IP $remote_addr;
        
        # Timeouts for long-running requests
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
    
    # Cache static files
    location ~* \.(jpg|jpeg|png|gif|ico|css|js|woff|woff2|ttf)$ {
        proxy_pass http://kiosk_backend;
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

```bash
# Enable site
sudo ln -s /etc/nginx/sites-available/restaurant-kiosk /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

#### Step 10: Get SSL Certificate

```bash
# Get certificate
sudo certbot --nginx -d your-domain.com -d www.your-domain.com

# Test auto-renewal
sudo certbot renew --dry-run
```

#### Step 11: Configure Firewall

```bash
# Install UFW
sudo apt install -y ufw

# Allow SSH
sudo ufw allow 22/tcp

# Allow HTTP/HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Allow PostgreSQL only from localhost (security)
sudo ufw deny 5432/tcp

# Enable firewall
sudo ufw enable
```

### Raspberry Pi Setup (Hardware Interface)

#### Step 1: Install Raspberry Pi OS

Same as before - see [RASPBERRY_PI_DEPLOYMENT.md](RASPBERRY_PI_DEPLOYMENT.md)

#### Step 2: Setup Hardware Services

```bash
# SSH to Raspberry Pi
ssh pi@raspberry-pi-ip

# Update system
sudo apt update && sudo apt upgrade -y

# Install required packages
sudo apt install -y python3 python3-pip chromium-browser unclutter xdotool
```

#### Step 3: Setup Arduino Cash Reader (If Using)

The Arduino service connects to VPS via SignalR:

```bash
# Create modified Arduino reader script
nano ~/arduino_cash_vps_reader.py
```

```python
#!/usr/bin/env python3
import serial
import time
import requests
import json
from signalrcore.hub_connection_builder import HubConnectionBuilder

# Configuration
VPS_URL = "https://your-domain.com"
CASH_HUB_URL = f"{VPS_URL}/cashpaymenthub"
ARDUINO_PORT = "/dev/ttyACM0"
BAUD_RATE = 9600

class CashReaderService:
    def __init__(self):
        self.serial_conn = None
        self.hub_connection = None
        self.current_session_id = None
        
    def connect_arduino(self):
        """Connect to Arduino"""
        try:
            self.serial_conn = serial.Serial(ARDUINO_PORT, BAUD_RATE, timeout=1)
            time.sleep(2)  # Wait for Arduino to initialize
            print(f"Connected to Arduino on {ARDUINO_PORT}")
            return True
        except Exception as e:
            print(f"Failed to connect to Arduino: {e}")
            return False
    
    def connect_signalr(self):
        """Connect to VPS SignalR hub"""
        try:
            self.hub_connection = HubConnectionBuilder()\
                .with_url(CASH_HUB_URL)\
                .with_automatic_reconnect({
                    "type": "interval",
                    "keep_alive_interval": 10,
                    "intervals": [1, 2, 5, 10, 30, 60]
                })\
                .build()
            
            # Register handlers
            self.hub_connection.on("StartSession", self.on_start_session)
            self.hub_connection.on("CancelSession", self.on_cancel_session)
            
            self.hub_connection.start()
            print("Connected to SignalR hub on VPS")
            return True
        except Exception as e:
            print(f"Failed to connect to SignalR: {e}")
            return False
    
    def on_start_session(self, session_id, amount_required):
        """Handle session start from VPS"""
        print(f"Session started: {session_id}, amount: {amount_required}")
        self.current_session_id = session_id
        
        # Send command to Arduino
        if self.serial_conn:
            command = f"START:{amount_required}\n"
            self.serial_conn.write(command.encode())
    
    def on_cancel_session(self, session_id):
        """Handle session cancellation from VPS"""
        print(f"Session cancelled: {session_id}")
        self.current_session_id = None
        
        # Send cancel to Arduino
        if self.serial_conn:
            self.serial_conn.write(b"CANCEL\n")
    
    def read_arduino_events(self):
        """Read events from Arduino and forward to VPS"""
        if not self.serial_conn:
            return
        
        try:
            if self.serial_conn.in_waiting > 0:
                line = self.serial_conn.readline().decode('utf-8').strip()
                
                if line.startswith("BILL_INSERTED:"):
                    amount = int(line.split(":")[1])
                    print(f"Bill inserted: {amount}")
                    
                    # Send to VPS
                    if self.hub_connection and self.current_session_id:
                        self.hub_connection.send("BillInserted", [self.current_session_id, amount])
                
                elif line.startswith("SESSION_COMPLETE"):
                    print("Session complete")
                    
                    if self.hub_connection and self.current_session_id:
                        self.hub_connection.send("SessionComplete", [self.current_session_id])
                    
                    self.current_session_id = None
                
                elif line.startswith("ERROR:"):
                    error_msg = line.split(":", 1)[1]
                    print(f"Arduino error: {error_msg}")
                    
                    if self.hub_connection and self.current_session_id:
                        self.hub_connection.send("Error", [self.current_session_id, error_msg])
        
        except Exception as e:
            print(f"Error reading Arduino: {e}")
    
    def run(self):
        """Main loop"""
        print("Starting Cash Reader Service...")
        
        # Connect to Arduino
        while not self.connect_arduino():
            print("Retrying Arduino connection in 5 seconds...")
            time.sleep(5)
        
        # Connect to SignalR
        while not self.connect_signalr():
            print("Retrying SignalR connection in 5 seconds...")
            time.sleep(5)
        
        print("Cash Reader Service running...")
        
        # Main loop
        try:
            while True:
                self.read_arduino_events()
                time.sleep(0.1)  # Small delay to prevent CPU hogging
        
        except KeyboardInterrupt:
            print("\nShutting down...")
        finally:
            if self.serial_conn:
                self.serial_conn.close()
            if self.hub_connection:
                self.hub_connection.stop()

if __name__ == "__main__":
    service = CashReaderService()
    service.run()
```

```bash
# Install dependencies
pip3 install pyserial signalrcore requests --break-system-packages

# Make executable
chmod +x ~/arduino_cash_vps_reader.py

# Test
python3 ~/arduino_cash_vps_reader.py
```

#### Step 4: Create Arduino Service

```bash
sudo nano /etc/systemd/system/arduino-cash-reader.service
```

```ini
[Unit]
Description=Arduino Cash Reader Service (VPS Connected)
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi
ExecStart=/usr/bin/python3 /home/pi/arduino_cash_vps_reader.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable arduino-cash-reader
sudo systemctl start arduino-cash-reader
```

#### Step 5: Configure Kiosk Browser

```bash
nano ~/kiosk-start.sh
```

```bash
#!/bin/bash

# Wait for network
sleep 10

# Hide cursor
unclutter -idle 0.5 -root &

# Disable screen blanking
xset s off
xset -dpms
xset s noblank

# Start Chromium in kiosk mode pointing to VPS
chromium-browser --noerrdialogs \
  --disable-infobars \
  --kiosk \
  --incognito \
  --disable-session-crashed-bubble \
  --disable-restore-session-state \
  --check-for-update-interval=31536000 \
  --app=https://your-domain.com/kioskstart
```

```bash
chmod +x ~/kiosk-start.sh
```

#### Step 6: Auto-start Kiosk

```bash
mkdir -p ~/.config/lxsession/LXDE-pi
nano ~/.config/lxsession/LXDE-pi/autostart
```

```
@lxpanel --profile LXDE-pi
@pcmanfm --desktop --profile LXDE-pi
@xscreensaver -no-splash
@point-rpi
@/home/pi/kiosk-start.sh
```

---

## Option 2: Local API Gateway Setup

For more complex hardware integration or offline support.

### Architecture

```
VPS (Cloud)
  └─ Main Application
       ↕ (REST API)
Raspberry Pi (Local)
  ├─ Local API Gateway
  ├─ Hardware Services
  │   ├─ Arduino Interface
  │   ├─ Printer Interface
  │   └─ Card Reader Interface
  └─ Browser (Kiosk Mode)
```

### Local API Gateway

Create a lightweight ASP.NET Core API on Raspberry Pi:

```bash
# On development machine
dotnet new webapi -n KioskHardwareGateway
cd KioskHardwareGateway
```

**Controllers/HardwareController.cs:**

```csharp
[ApiController]
[Route("api/[controller]")]
public class HardwareController : ControllerBase
{
    private readonly IArduinoService _arduinoService;
    private readonly IVpsApiClient _vpsClient;
    
    [HttpPost("cash/start")]
    public async Task<IActionResult> StartCashSession([FromBody] CashSessionRequest request)
    {
        // Start Arduino session
        await _arduinoService.StartSession(request.Amount);
        
        // Notify VPS
        await _vpsClient.NotifySessionStarted(request.SessionId);
        
        return Ok();
    }
    
    [HttpPost("cash/bill-inserted")]
    public async Task<IActionResult> BillInserted([FromBody] BillInsertedEvent evt)
    {
        // Forward to VPS
        await _vpsClient.NotifyBillInserted(evt);
        return Ok();
    }
}
```

---

## Multi-Location Deployment

### For Multiple Kiosks

**One VPS serves all locations:**

```
                  VPS (Cloud)
                      ↕
         ┌────────────┼────────────┐
         ↕            ↕            ↕
    Kiosk 1       Kiosk 2      Kiosk 3
   (Location A)  (Location B)  (Location C)
```

**Each kiosk:**
- Raspberry Pi with touchscreen
- Arduino (if using cash payment)
- Connects to same VPS
- Separate database records for location

**Benefits:**
- Centralized inventory
- Unified reporting
- Easy management
- Cost effective

---

## Cost Comparison

### Single Kiosk

| Component | Cost | Period |
|-----------|------|--------|
| **Full Pi Deployment** | | |
| Domain | $10-15 | /year |
| Cloudflare Tunnel | Free | Forever |
| Raspberry Pi | $75-100 | One-time |
| **Subtotal** | **~$85-115** | **First year** |
| | **$10-15** | **Ongoing/year** |
| | | |
| **VPS Hybrid** | | |
| Domain | $10-15 | /year |
| VPS (Hetzner) | $5 | /month |
| Raspberry Pi | $75-100 | One-time |
| **Subtotal** | **~$145-175** | **First year** |
| | **$70-75** | **Ongoing/year** |

### Three Kiosks

| Component | Cost | Period |
|-----------|------|--------|
| **Full Pi x3** | | |
| Domains (3x) | $30-45 | /year |
| Raspberry Pi (3x) | $225-300 | One-time |
| **Total** | **~$255-345** | **First year** |
| | **$30-45** | **Ongoing/year** |
| | | |
| **VPS Hybrid** | | |
| Domain (1x) | $10-15 | /year |
| VPS (Upgraded) | $12 | /month |
| Raspberry Pi (3x) | $225-300 | One-time |
| **Total** | **~$379-459** | **First year** |
| | **$154-159** | **Ongoing/year** |

**VPS becomes more cost-effective at 2+ kiosks long-term**

---

## Monitoring & Maintenance

### VPS Monitoring

```bash
# Install monitoring tools
sudo apt install -y prometheus-node-exporter

# Monitor application
sudo journalctl -u restaurant-kiosk -f

# Monitor resources
htop
df -h
free -h
```

### Raspberry Pi Monitoring

```bash
# Check hardware service
sudo systemctl status arduino-cash-reader

# Monitor temperature
vcgencmd measure_temp

# Check browser
ps aux | grep chromium
```

### Centralized Logging (Optional)

Use services like:
- **Papertrail** (free tier available)
- **Loggly** (free tier available)
- **Grafana Cloud** (free tier available)

---

## Offline Support (Optional)

For critical operations when internet is down:

### Service Worker Cache

Add to your Blazor app:

```javascript
// wwwroot/service-worker.js
self.addEventListener('fetch', (event) => {
    event.respondWith(
        caches.match(event.request)
            .then((response) => {
                return response || fetch(event.request);
            })
    );
});
```

### Local Queue

Queue orders locally when offline:

```csharp
public class OfflineQueueService
{
    private readonly ILocalStorageService _localStorage;
    
    public async Task QueueOrder(Order order)
    {
        var queue = await _localStorage.GetItemAsync<List<Order>>("offline_queue") ?? new List<Order>();
        queue.Add(order);
        await _localStorage.SetItemAsync("offline_queue", queue);
    }
    
    public async Task SyncQueue()
    {
        var queue = await _localStorage.GetItemAsync<List<Order>>("offline_queue");
        if (queue == null || !queue.Any()) return;
        
        foreach (var order in queue)
        {
            await _apiClient.CreateOrder(order);
        }
        
        await _localStorage.RemoveItemAsync("offline_queue");
    }
}
```

---

## Security Considerations

### VPS Security

```bash
# Disable root login
sudo nano /etc/ssh/sshd_config
# Set: PermitRootLogin no

# Install fail2ban
sudo apt install -y fail2ban
sudo systemctl enable fail2ban

# Setup automatic security updates
sudo apt install -y unattended-upgrades
sudo dpkg-reconfigure -plow unattended-upgrades
```

### API Authentication

Secure communication between Pi and VPS:

```csharp
// Add API key authentication
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKey;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API Key missing");
            return;
        }
        
        if (!_apiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid API Key");
            return;
        }
        
        await _next(context);
    }
}
```

---

## Deployment Scripts

### VPS Deployment Script

**deploy-to-vps.ps1:**

```powershell
param(
    [string]$VpsHost = "your-vps-ip",
    [string]$VpsUser = "kiosk"
)

Write-Host "Building for VPS..." -ForegroundColor Cyan
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish-vps

Write-Host "Creating package..." -ForegroundColor Cyan
Compress-Archive -Path ./publish-vps/* -DestinationPath restaurant-kiosk-vps.zip -Force

Write-Host "Transferring to VPS..." -ForegroundColor Cyan
scp restaurant-kiosk-vps.zip ${VpsUser}@${VpsHost}:~/

Write-Host "Deploying on VPS..." -ForegroundColor Cyan
ssh ${VpsUser}@${VpsHost} @"
    sudo systemctl stop restaurant-kiosk
    sudo rm -rf /var/www/restaurant-kiosk/*
    unzip -o restaurant-kiosk.zip -d temp
    sudo mv temp/* /var/www/restaurant-kiosk/
    rm -rf temp
    chmod +x /var/www/restaurant-kiosk/RestaurantKiosk
    sudo systemctl start restaurant-kiosk
"@

Write-Host "Deployment complete!" -ForegroundColor Green
```

---

## Troubleshooting

### VPS Issues

```bash
# Application won't start
sudo journalctl -u restaurant-kiosk -n 100

# Database connection issues
psql -U kiosk_user -h localhost -d restaurant_kiosk

# Nginx issues
sudo nginx -t
sudo tail -f /var/log/nginx/error.log
```

### Raspberry Pi Issues

```bash
# Browser not loading
ps aux | grep chromium
sudo journalctl -u lightdm -n 50

# Hardware service not connecting
sudo journalctl -u arduino-cash-reader -f
ping your-domain.com
```

### Network Issues

```bash
# Test VPS connectivity from Pi
curl -I https://your-domain.com

# Test WebSocket
wscat -c wss://your-domain.com/cashpaymenthub
```

---

## Recommended Setup

**For 1 Kiosk:**
- Use Cloudflare Tunnel (cheapest, simplest)

**For 2-3 Kiosks:**
- VPS Hybrid (better management)

**For 4+ Kiosks:**
- VPS Hybrid (significantly cheaper, professional)

---

## Next Steps

1. Choose architecture based on your needs
2. Deploy VPS following this guide
3. Configure domain and SSL
4. Setup Raspberry Pi as thin client
5. Test hardware integration
6. Monitor and optimize

For questions, see:
- [RASPBERRY_PI_DEPLOYMENT.md](RASPBERRY_PI_DEPLOYMENT.md)
- [PRODUCTION_INTERNET_SETUP.md](PRODUCTION_INTERNET_SETUP.md)
- [DEPLOYMENT_OVERVIEW.md](DEPLOYMENT_OVERVIEW.md)

