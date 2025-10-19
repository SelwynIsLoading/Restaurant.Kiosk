# Production Internet Setup for Xendit Callbacks

This guide covers setting up your Raspberry Pi to be accessible from the internet with a domain name, required for Xendit payment callbacks.

## Problem Statement

**Xendit requires:**
- ✅ Public HTTPS URL (e.g., `https://yourdomain.com/api/callback`)
- ✅ Accessible from internet 24/7
- ✅ Valid SSL certificate
- ❌ `localhost` or private IPs won't work
- ❌ HTTP-only connections not recommended

**Your Raspberry Pi Challenge:**
- Dynamic IP address (changes periodically)
- Behind home/office router (NAT)
- Needs to be accessible from internet

## Solution Options

| Solution | Cost | Complexity | Reliability | Best For |
|----------|------|------------|-------------|----------|
| **Cloudflare Tunnel** | Free | Low | High | Recommended - Easy & Secure |
| **Ngrok** | Free-$8/mo | Very Low | High | Development & Testing |
| **Dynamic DNS + Port Forward** | $10-15/yr | Medium | Medium | Own domain, static setup |
| **VPS Deployment** | $5-20/mo | High | Very High | Production at scale |

---

## Option 1: Cloudflare Tunnel (Recommended) ⭐

**Pros:**
- ✅ Free forever
- ✅ Automatic HTTPS
- ✅ No port forwarding needed
- ✅ No router configuration
- ✅ DDoS protection
- ✅ Works behind any firewall

**Cons:**
- ❌ Requires Cloudflare account
- ❌ Domain must use Cloudflare nameservers

### Step-by-Step Setup

#### 1. Get a Domain Name

```
Register domain at:
- Namecheap (~$10/year)
- Google Domains (~$12/year)
- Cloudflare (~$10/year)

Example: restaurant-kiosk.com
```

#### 2. Add Domain to Cloudflare

1. Sign up at [cloudflare.com](https://cloudflare.com) (free account)
2. Add your domain
3. Update nameservers at your domain registrar to Cloudflare's
4. Wait for DNS propagation (5-60 minutes)

#### 3. Install Cloudflared on Raspberry Pi

```bash
# SSH to your Raspberry Pi
ssh pi@restaurant-kiosk.local

# Download cloudflared
wget https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64
sudo mv cloudflared-linux-arm64 /usr/local/bin/cloudflared
sudo chmod +x /usr/local/bin/cloudflared

# Verify installation
cloudflared --version
```

#### 4. Authenticate Cloudflared

```bash
# Login to Cloudflare (opens browser)
cloudflared tunnel login

# This creates a cert.pem file in ~/.cloudflared/
```

#### 5. Create Tunnel

```bash
# Create a tunnel
cloudflared tunnel create restaurant-kiosk

# Note the Tunnel ID (you'll need this)
# Example: 12345678-1234-1234-1234-123456789abc
```

#### 6. Configure Tunnel

```bash
# Create config directory
mkdir -p ~/.cloudflared

# Create configuration file
nano ~/.cloudflared/config.yml
```

**Content:**

```yaml
tunnel: restaurant-kiosk
credentials-file: /home/pi/.cloudflared/[YOUR-TUNNEL-ID].json

ingress:
  # Route your domain to local application
  - hostname: restaurant-kiosk.com
    service: http://localhost:5000
  
  # Route www subdomain
  - hostname: www.restaurant-kiosk.com
    service: http://localhost:5000
  
  # Catch-all rule (required)
  - service: http_status:404
```

#### 7. Route DNS

```bash
# Route DNS to your tunnel
cloudflared tunnel route dns restaurant-kiosk restaurant-kiosk.com
cloudflared tunnel route dns restaurant-kiosk www.restaurant-kiosk.com
```

#### 8. Create Systemd Service

```bash
sudo nano /etc/systemd/system/cloudflared.service
```

**Content:**

```ini
[Unit]
Description=Cloudflare Tunnel
After=network.target

[Service]
Type=simple
User=pi
ExecStart=/usr/local/bin/cloudflared tunnel --config /home/pi/.cloudflared/config.yml run restaurant-kiosk
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

**Enable and Start:**

```bash
sudo systemctl daemon-reload
sudo systemctl enable cloudflared
sudo systemctl start cloudflared
sudo systemctl status cloudflared
```

#### 9. Update Application Configuration

```bash
sudo nano /var/www/restaurant-kiosk/appsettings.Production.json
```

Update:

```json
{
  "BaseUrl": "https://restaurant-kiosk.com",
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=5432;Database=restaurant_kiosk;User Id=kiosk_user;Password=YourPassword;"
  },
  "Xendit": {
    "ApiKey": "xnd_production_YOUR_KEY",
    "WebhookToken": "YOUR_WEBHOOK_TOKEN",
    "IsSandbox": false,
    "BaseUrl": "https://api.xendit.co",
    "CallbackUrl": "https://restaurant-kiosk.com/api/payment/callback"
  }
}
```

#### 10. Restart Application

```bash
sudo systemctl restart restaurant-kiosk
```

#### 11. Test

```bash
# Test from anywhere
curl https://restaurant-kiosk.com

# Should return your application
```

#### 12. Configure Xendit Webhook

1. Go to [Xendit Dashboard](https://dashboard.xendit.co)
2. Settings → Webhooks
3. Add webhook URL: `https://restaurant-kiosk.com/api/payment/callback`
4. Test the webhook

**✅ Done! Your Raspberry Pi is now accessible from the internet with HTTPS!**

---

## Option 2: Ngrok (Quick & Easy for Testing)

**Best for:** Development, testing, temporary deployments

### Setup

```bash
# SSH to Raspberry Pi
ssh pi@restaurant-kiosk.local

# Install ngrok
wget https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-linux-arm64.tgz
tar -xvzf ngrok-v3-stable-linux-arm64.tgz
sudo mv ngrok /usr/local/bin/

# Sign up at https://ngrok.com and get auth token
ngrok config add-authtoken YOUR_AUTH_TOKEN

# Start tunnel
ngrok http 5000
```

**You'll get a URL like:** `https://abc123.ngrok.io`

### For Permanent Use (Paid Plan - $8/month)

```bash
# Create ngrok config
nano ~/.config/ngrok/ngrok.yml
```

```yaml
version: "2"
authtoken: YOUR_AUTH_TOKEN
tunnels:
  kiosk:
    addr: 5000
    proto: http
    hostname: restaurant-kiosk.ngrok.app  # Custom domain (paid)
```

### Create Service

```bash
sudo nano /etc/systemd/system/ngrok.service
```

```ini
[Unit]
Description=Ngrok Tunnel
After=network.target

[Service]
Type=simple
User=pi
ExecStart=/usr/local/bin/ngrok start --all --config /home/pi/.config/ngrok/ngrok.yml
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable ngrok
sudo systemctl start ngrok
```

**Free Plan Limitations:**
- ❌ URL changes each restart
- ❌ Random subdomain
- ❌ No custom domain
- ✅ Good for testing only

**Paid Plan ($8/mo):**
- ✅ Static domain
- ✅ Custom domain
- ✅ Reserved domains

---

## Option 3: Dynamic DNS + Port Forwarding

**Best for:** Own your infrastructure, one-time setup cost

### Requirements

- Domain name ($10-15/year)
- Router with port forwarding capability
- Dynamic DNS service

### Step 1: Choose Dynamic DNS Provider

**Free Options:**
- No-IP (free tier: 1 hostname)
- DuckDNS (free)
- FreeDNS (free)

**Paid Options:**
- Namecheap Dynamic DNS (included with domain)
- Google Domains Dynamic DNS (included)
- DynDNS Pro ($55/year)

### Step 2: Setup with Duck DNS (Free)

```bash
# SSH to Raspberry Pi
ssh pi@restaurant-kiosk.local

# Create update script
mkdir -p ~/duckdns
nano ~/duckdns/duck.sh
```

```bash
#!/bin/bash
echo url="https://www.duckdns.org/update?domains=YOUR_SUBDOMAIN&token=YOUR_TOKEN&ip=" | curl -k -o ~/duckdns/duck.log -K -
```

```bash
chmod +x ~/duckdns/duck.sh

# Test
~/duckdns/duck.sh
cat ~/duckdns/duck.log  # Should say "OK"

# Add to crontab (update every 5 minutes)
crontab -e

# Add this line:
*/5 * * * * ~/duckdns/duck.sh >/dev/null 2>&1
```

### Step 3: Port Forwarding

**On your router:**

1. Login to router admin panel (usually 192.168.1.1 or 192.168.0.1)
2. Find "Port Forwarding" or "Virtual Server" section
3. Add forwarding rules:

```
Service Name: HTTP
External Port: 80
Internal IP: [Raspberry Pi IP, e.g., 192.168.1.100]
Internal Port: 80
Protocol: TCP

Service Name: HTTPS
External Port: 443
Internal IP: [Raspberry Pi IP]
Internal Port: 443
Protocol: TCP
```

4. Save and reboot router if needed

### Step 4: Configure Nginx as Reverse Proxy

```bash
sudo nano /etc/nginx/sites-available/restaurant-kiosk
```

```nginx
server {
    listen 80;
    server_name your-subdomain.duckdns.org;
    
    # Redirect to HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name your-subdomain.duckdns.org;

    # SSL will be configured in next step
    ssl_certificate /etc/letsencrypt/live/your-subdomain.duckdns.org/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-subdomain.duckdns.org/privkey.pem;

    client_max_body_size 10M;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Step 5: Get SSL Certificate (Let's Encrypt)

```bash
# Install Certbot
sudo apt install -y certbot python3-certbot-nginx

# Get certificate
sudo certbot --nginx -d your-subdomain.duckdns.org

# Follow prompts
# Select "2" for redirect HTTP to HTTPS

# Test auto-renewal
sudo certbot renew --dry-run
```

### Step 6: Enable Nginx

```bash
sudo ln -s /etc/nginx/sites-available/restaurant-kiosk /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

### Step 7: Configure Application

```bash
sudo nano /var/www/restaurant-kiosk/appsettings.Production.json
```

```json
{
  "BaseUrl": "https://your-subdomain.duckdns.org",
  "Xendit": {
    "CallbackUrl": "https://your-subdomain.duckdns.org/api/payment/callback"
  }
}
```

---

## Option 4: VPS Deployment (Most Reliable)

**Best for:** Production at scale, multiple kiosks

### Recommended Providers

| Provider | Price | Specs | Best For |
|----------|-------|-------|----------|
| DigitalOcean | $6/mo | 1GB RAM, 1 vCPU | Small deployments |
| Linode | $5/mo | 1GB RAM, 1 vCPU | Budget option |
| Vultr | $6/mo | 1GB RAM, 1 vCPU | Global locations |
| Hetzner | €4.5/mo | 2GB RAM, 1 vCPU | Best value |
| AWS Lightsail | $5/mo | 1GB RAM, 1 vCPU | AWS ecosystem |

### Quick VPS Setup

```bash
# Create Ubuntu 22.04 LTS server
# SSH to server
ssh root@your-server-ip

# Run setup script (similar to Raspberry Pi)
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0 --runtime aspnetcore

# Install PostgreSQL
sudo apt update
sudo apt install -y postgresql nginx certbot python3-certbot-nginx

# Deploy application
# (Same as Raspberry Pi deployment)

# Get SSL certificate
sudo certbot --nginx -d your-domain.com

# Configure Nginx (same as above)
```

**Pros:**
- ✅ Static IP
- ✅ 99.9% uptime
- ✅ Scalable
- ✅ Professional

**Cons:**
- ❌ Monthly cost
- ❌ No physical access to kiosk
- ❌ Requires remote management

---

## Comparison Matrix

| Feature | Cloudflare Tunnel | Ngrok (Paid) | DDNS + Port Forward | VPS |
|---------|------------------|--------------|---------------------|-----|
| **Cost** | Free | $8/mo | $10-15/yr | $5-20/mo |
| **Setup Complexity** | Low | Very Low | Medium | High |
| **Requires Router Access** | No | No | Yes | No |
| **Static Domain** | Yes | Yes | Yes | Yes |
| **HTTPS** | Auto | Auto | Manual (Let's Encrypt) | Manual |
| **DDoS Protection** | Yes | Yes | No | Depends |
| **Reliability** | 99.9%+ | 99.9%+ | Depends on ISP | 99.9%+ |
| **Speed** | Fast | Fast | Fastest (direct) | Fast |
| **Security** | High | High | Medium | High |

---

## Recommended Setup for Production

### For Single Kiosk Location

**Best: Cloudflare Tunnel**

```
Domain → Cloudflare DNS → Cloudflare Tunnel → Raspberry Pi
```

**Why:**
- Free forever
- No router configuration
- Automatic HTTPS
- DDoS protection
- Easy to setup

### For Multiple Kiosk Locations

**Best: VPS + Cloudflare**

```
Each Kiosk (Raspberry Pi) → VPN → Central VPS → Cloudflare → Domain
```

**Or:**

Each kiosk gets its own subdomain:
- kiosk1.restaurant.com → Kiosk 1 Pi
- kiosk2.restaurant.com → Kiosk 2 Pi
- kiosk3.restaurant.com → Kiosk 3 Pi

---

## Testing Your Setup

### 1. Test from External Network

```bash
# From your phone (using mobile data, not WiFi)
curl https://your-domain.com

# Or visit in browser
```

### 2. Test SSL Certificate

Visit: [SSL Labs](https://www.ssllabs.com/ssltest/)
- Enter your domain
- Should get A+ rating

### 3. Test Xendit Webhook

```bash
# From Xendit dashboard
# Settings → Webhooks → Test Webhook

# Check application logs
sudo journalctl -u restaurant-kiosk -f
```

### 4. Test Callback Endpoint

```bash
# Test from external network
curl -X POST https://your-domain.com/api/payment/callback \
  -H "Content-Type: application/json" \
  -H "X-Callback-Token: YOUR_WEBHOOK_TOKEN" \
  -d '{
    "id": "test-123",
    "status": "COMPLETED"
  }'
```

---

## Security Considerations

### 1. Firewall Configuration

```bash
# On Raspberry Pi with Cloudflare Tunnel
# Only allow localhost connections
sudo ufw default deny incoming
sudo ufw allow ssh
sudo ufw enable

# Application only listens on localhost
# Cloudflare Tunnel connects outbound (no open ports)
```

### 2. Rate Limiting

```nginx
# In Nginx config
limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;

location /api/payment/callback {
    limit_req zone=api burst=20;
    # ... rest of config
}
```

### 3. Webhook Authentication

```csharp
// In CallbackController.cs
[HttpPost("callback")]
public async Task<IActionResult> XenditCallback()
{
    // Verify webhook token
    var token = Request.Headers["X-Callback-Token"].FirstOrDefault();
    if (token != _configuration["Xendit:WebhookToken"])
    {
        return Unauthorized();
    }
    
    // Process webhook
    // ...
}
```

### 4. IP Whitelisting (Optional)

Xendit IP ranges to whitelist:
```
169.50.59.188/32
169.50.4.145/32
```

---

## Monitoring

### 1. Uptime Monitoring

Use free services:
- [UptimeRobot](https://uptimerobot.com) - Free, checks every 5 min
- [Pingdom](https://pingdom.com) - Free tier available
- [Freshping](https://freshping.io) - Free

Configure to check:
```
https://your-domain.com/health
```

### 2. Log Monitoring

```bash
# Watch Cloudflare Tunnel logs
sudo journalctl -u cloudflared -f

# Watch application logs
sudo journalctl -u restaurant-kiosk -f

# Watch Nginx logs
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log
```

---

## Troubleshooting

### Cloudflare Tunnel Not Connecting

```bash
# Check tunnel status
cloudflared tunnel info restaurant-kiosk

# Check service
sudo systemctl status cloudflared

# Check logs
sudo journalctl -u cloudflared -n 50

# Restart tunnel
sudo systemctl restart cloudflared
```

### SSL Certificate Issues

```bash
# Renew certificate
sudo certbot renew

# Test renewal
sudo certbot renew --dry-run

# Check certificate
sudo certbot certificates
```

### Domain Not Resolving

```bash
# Check DNS
dig your-domain.com
nslookup your-domain.com

# Check from external
# https://www.whatsmydns.net/
```

### Xendit Callback Not Working

```bash
# Test endpoint manually
curl -v https://your-domain.com/api/payment/callback

# Check application logs
sudo journalctl -u restaurant-kiosk -f | grep -i callback

# Verify webhook token
# Check appsettings.Production.json
```

---

## Cost Breakdown

### Recommended Setup (Cloudflare Tunnel)

| Item | Cost | Period |
|------|------|--------|
| Domain (Namecheap) | $10-15 | /year |
| Cloudflare Account | Free | Forever |
| Cloudflare Tunnel | Free | Forever |
| **Total** | **$10-15** | **/year** |

### Alternative Setup (Ngrok)

| Item | Cost | Period |
|------|------|--------|
| Ngrok Pro | $8 | /month |
| **Total** | **$96** | **/year** |

### VPS Setup

| Item | Cost | Period |
|------|------|--------|
| Domain | $10-15 | /year |
| VPS (Hetzner) | €4.5 (~$5) | /month |
| **Total** | **$70-75** | **/year** |

---

## Quick Start Guide

**Fastest Path to Production:**

1. **Register Domain** (1 hour)
   - Buy at Namecheap/Google Domains

2. **Setup Cloudflare** (30 minutes)
   - Add domain to Cloudflare
   - Update nameservers

3. **Install Cloudflared** (15 minutes)
   ```bash
   wget https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64
   sudo mv cloudflared-linux-arm64 /usr/local/bin/cloudflared
   sudo chmod +x /usr/local/bin/cloudflared
   cloudflared tunnel login
   cloudflared tunnel create restaurant-kiosk
   ```

4. **Configure** (10 minutes)
   - Create config.yml
   - Route DNS
   - Create systemd service

5. **Test** (5 minutes)
   - Visit domain
   - Test Xendit webhook

**Total Time: ~2 hours**

---

## Next Steps

After setup:

1. ✅ Update `appsettings.Production.json` with your domain
2. ✅ Configure Xendit webhook URL
3. ✅ Test payment flow end-to-end
4. ✅ Set up monitoring
5. ✅ Configure backups
6. ✅ Document your setup

**You're ready for production! 🚀**

