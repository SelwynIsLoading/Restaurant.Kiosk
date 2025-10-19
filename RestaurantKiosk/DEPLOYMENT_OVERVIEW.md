# Deployment Overview - Restaurant Kiosk

This document provides an overview of deploying your Restaurant Kiosk application to Raspberry Pi.

## 📚 Documentation Structure

| Document | Purpose | When to Use |
|----------|---------|-------------|
| **[DEPLOYMENT_DECISION_GUIDE.md](DEPLOYMENT_DECISION_GUIDE.md)** | 🎯 **START HERE** - Choose the right architecture | First time deploying - not sure which option |
| **[RASPBERRY_PI_DEPLOYMENT.md](RASPBERRY_PI_DEPLOYMENT.md)** | Complete step-by-step deployment guide | Single kiosk, all-in-one Pi solution |
| **[VPS_HYBRID_DEPLOYMENT.md](VPS_HYBRID_DEPLOYMENT.md)** | 🌟 **VPS + Local Hardware** - Best for multiple kiosks | Production with 2+ locations |
| **[PRODUCTION_INTERNET_SETUP.md](PRODUCTION_INTERNET_SETUP.md)** | ⚠️ **Required for Xendit** - Internet exposure & domain setup | Production deployment with payments |
| **[deployment/QUICK_START.md](deployment/QUICK_START.md)** | Fast deployment guide | When you need to deploy quickly |
| **[deployment/DEPLOYMENT_CHECKLIST.md](deployment/DEPLOYMENT_CHECKLIST.md)** | Track deployment progress | Ensure nothing is missed |
| **[deployment/README.md](deployment/README.md)** | Deployment scripts documentation | Understanding automation scripts |

## 🚀 Quick Deployment Options

### Option 1: Automated Deployment (Recommended)

**Best for:** Regular deployments, production updates

```powershell
# One command from Windows
cd RestaurantKiosk
.\deployment\Deploy-ToPi.ps1
```

**What it does:**
- ✅ Builds application for Raspberry Pi
- ✅ Creates deployment package
- ✅ Transfers to Pi
- ✅ Deploys automatically
- ✅ Starts services
- ⏱️ Time: ~3-5 minutes

### Option 2: Script-Assisted Deployment

**Best for:** First-time setup

```bash
# 1. Initial Pi setup (one time)
./setup-pi.sh          # ~10 minutes

# 2. Install services (one time)
./install-services.sh  # ~1 minute

# 3. Deploy application (repeatable)
./deploy.sh            # ~3 minutes
```

### Option 3: Manual Deployment

**Best for:** Custom configurations, troubleshooting

Follow the complete guide in [RASPBERRY_PI_DEPLOYMENT.md](RASPBERRY_PI_DEPLOYMENT.md)

## 🎯 Deployment Workflow

### First-Time Setup (One Time Only)

```
1. Prepare Raspberry Pi
   ├─ Install Raspberry Pi OS
   ├─ Enable SSH
   └─ Network configuration
   
2. Run Initial Setup Script
   ├─ Install .NET 9
   ├─ Install PostgreSQL
   ├─ Create database
   ├─ Install Python dependencies
   └─ Configure system
   
3. Install Systemd Services
   ├─ Create application service
   ├─ Create Arduino service (optional)
   └─ Enable auto-start
   
4. Deploy Application
   ├─ Build for linux-arm64
   ├─ Transfer to Pi
   ├─ Extract and configure
   └─ Start services
   
5. Configure Kiosk Mode (Optional)
   ├─ Install Chromium
   ├─ Create kiosk script
   └─ Enable auto-start
```

### Regular Updates (Repeatable)

```
1. Build Application
   └─ dotnet publish for linux-arm64
   
2. Deploy to Pi
   ├─ Stop services
   ├─ Backup current version
   ├─ Deploy new version
   └─ Restart services
```

## 📋 Pre-Deployment Checklist

### Hardware
- [ ] Raspberry Pi 4 (4GB+) or Pi 5
- [ ] 32GB+ MicroSD card or SSD (recommended)
- [ ] Power supply (official recommended)
- [ ] Touchscreen or monitor
- [ ] Cooling solution (heatsink/fan)
- [ ] Arduino (if using cash payment)

### Software
- [ ] Raspberry Pi OS (64-bit) installed
- [ ] SSH enabled and accessible
- [ ] .NET 9 SDK on development machine
- [ ] Application builds successfully
- [ ] OpenSSH client (Windows)

### ⚠️ For Production (Xendit Payments)
- [ ] **Domain name registered** (required for Xendit callbacks)
- [ ] **Cloudflare account** (free - recommended for tunnel)
- [ ] OR VPS with static IP
- [ ] See **[PRODUCTION_INTERNET_SETUP.md](PRODUCTION_INTERNET_SETUP.md)** for details

## 🔧 Available Tools

### PowerShell Scripts (Windows)

| Script | Purpose |
|--------|---------|
| `Deploy-ToPi.ps1` | Automated build and deployment |

**Usage:**
```powershell
# Basic deployment
.\deployment\Deploy-ToPi.ps1

# Custom host
.\deployment\Deploy-ToPi.ps1 -PiHost "192.168.1.100"

# Build only (no deploy)
.\deployment\Deploy-ToPi.ps1 -SkipDeploy

# Deploy existing package
.\deployment\Deploy-ToPi.ps1 -SkipBuild
```

### Bash Scripts (Raspberry Pi)

| Script | Purpose |
|--------|---------|
| `setup-pi.sh` | Initial Raspberry Pi setup |
| `install-services.sh` | Install systemd services |
| `deploy.sh` | Deploy application |
| `setup-cloudflare-tunnel.sh` | ⚠️ **Setup internet access for Xendit** |

## 📊 Deployment Comparison

| Method | Setup Time | Deploy Time | Skill Level | Flexibility |
|--------|------------|-------------|-------------|-------------|
| Automated (PowerShell) | 10 min | 3-5 min | Beginner | Low |
| Script-Assisted | 15 min | 3 min | Intermediate | Medium |
| Manual | 30+ min | 10+ min | Advanced | High |

## 🎓 Step-by-Step for Beginners

**1. Prepare Your Raspberry Pi (15 minutes)**

- Download Raspberry Pi Imager
- Flash Raspberry Pi OS (64-bit) to SD card
- Set hostname: `restaurant-kiosk`
- Enable SSH
- Boot up and find IP address

**2. Transfer Setup Script (2 minutes)**

```powershell
# From Windows PowerShell
scp RestaurantKiosk\deployment\setup-pi.sh pi@restaurant-kiosk.local:~/
```

**3. Run Initial Setup (10 minutes)**

```bash
# SSH to Pi
ssh pi@restaurant-kiosk.local

# Run setup
chmod +x setup-pi.sh
./setup-pi.sh

# Follow prompts for database password
# Logout and login when done
```

**4. Install Services (2 minutes)**

```powershell
# Transfer script
scp RestaurantKiosk\deployment\install-services.sh pi@restaurant-kiosk.local:~/
```

```bash
# On Pi
chmod +x install-services.sh
./install-services.sh
```

**5. Deploy Application (5 minutes)**

```powershell
# From your development machine
cd RestaurantKiosk
.\deployment\Deploy-ToPi.ps1
```

**6. Verify (2 minutes)**

Open browser: `http://raspberry-pi-ip:5000`

**7. Setup Internet Access for Xendit (Required for Production - 20 minutes)**

```bash
# Transfer script
scp RestaurantKiosk\deployment\setup-cloudflare-tunnel.sh pi@restaurant-kiosk.local:~/

# On Pi
chmod +x setup-cloudflare-tunnel.sh
./setup-cloudflare-tunnel.sh
```

See **[PRODUCTION_INTERNET_SETUP.md](PRODUCTION_INTERNET_SETUP.md)** for details.

**Total Time: ~35 minutes (Development) | ~55 minutes (Production with Xendit)** ⏱️

## 🔍 Verification Steps

After deployment, verify:

```bash
# Check service status
sudo systemctl status restaurant-kiosk

# Check logs
sudo journalctl -u restaurant-kiosk -f

# Check database
psql -U kiosk_user -h localhost -d restaurant_kiosk -c "\dt"

# Check temperature
vcgencmd measure_temp

# Check disk space
df -h
```

## 🆘 Common Issues & Solutions

| Issue | Quick Fix |
|-------|-----------|
| Can't SSH to Pi | Use IP instead of hostname: `ssh pi@192.168.1.100` |
| .NET not found | Run `setup-pi.sh` again |
| Service won't start | Check logs: `sudo journalctl -u restaurant-kiosk -n 50` |
| Database connection failed | Verify connection string in `appsettings.Production.json` |
| Port 5000 in use | Check: `sudo netstat -tulpn \| grep :5000` |
| Out of memory | Increase swap or use 8GB Pi |

## 📈 Performance Recommendations

### Minimum (Works)
- Raspberry Pi 4 (4GB RAM)
- 32GB SD Card (Class 10)
- Basic heatsink

### Recommended (Better)
- Raspberry Pi 4 (8GB RAM) or Pi 5
- 64GB+ SSD via USB
- Active cooling (fan)
- Ethernet connection

### Optimal (Best)
- Raspberry Pi 5 (8GB RAM)
- 128GB+ NVMe SSD
- Active cooling with heatsink
- Gigabit Ethernet
- Overclocked settings

## 🔐 Security Best Practices

Before production deployment:

1. **Change All Default Passwords**
   ```bash
   passwd                    # Pi user
   # Update database password
   # Update application admin password
   ```

2. **Use SSH Keys**
   ```powershell
   ssh-keygen -t rsa
   ssh-copy-id pi@restaurant-kiosk.local
   ```

3. **Configure Firewall**
   ```bash
   sudo apt install ufw
   sudo ufw allow 22/tcp
   sudo ufw allow 80/tcp
   sudo ufw enable
   ```

4. **Update Configuration**
   - Set production Xendit API keys
   - Configure HTTPS (if using domain)
   - Secure connection strings

## 🔄 Update Strategy

### Regular Updates (Weekly)
```powershell
.\deployment\Deploy-ToPi.ps1
```

### System Updates (Monthly)
```bash
ssh pi@restaurant-kiosk.local
sudo apt update && sudo apt upgrade -y
sudo reboot
```

### Rollback (If Needed)
```bash
# List backups
ls -la /var/www/ | grep backup

# Restore
sudo systemctl stop restaurant-kiosk
sudo rm -rf /var/www/restaurant-kiosk
sudo mv /var/www/restaurant-kiosk.backup.YYYYMMDD_HHMMSS /var/www/restaurant-kiosk
sudo systemctl start restaurant-kiosk
```

## 📞 Support Resources

### Documentation
- [Complete Deployment Guide](RASPBERRY_PI_DEPLOYMENT.md)
- [Quick Start](deployment/QUICK_START.md)
- [Deployment Checklist](deployment/DEPLOYMENT_CHECKLIST.md)
- [Scripts Documentation](deployment/README.md)

### Application Setup
- [Local Development](LOCAL_DEVELOPMENT_SETUP.md)
- [Cash Payment Setup](CASH_PAYMENT_SETUP.md)
- [Order Management](ORDER_MANAGEMENT_SETUP.md)
- [Xendit Integration](XENDIT_SETUP.md)

### External Resources
- [Raspberry Pi Documentation](https://www.raspberrypi.com/documentation/)
- [.NET on Linux ARM](https://learn.microsoft.com/en-us/dotnet/core/install/linux-debian)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [ASP.NET Core Deployment](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/)

## 🎯 Next Steps

After successful deployment:

1. **Configure Application**
   - Set up admin account
   - Add categories
   - Add products
   - Configure prices

2. **Test Features**
   - Payment processing
   - Order creation
   - Arduino integration (if applicable)
   - Kiosk mode

3. **Set Up Monitoring**
   - Database backups
   - Log rotation
   - Performance monitoring
   - Alert system

4. **Train Users**
   - Staff training
   - Admin interface
   - Troubleshooting basics

5. **Go Live**
   - Soft launch
   - Monitor closely
   - Collect feedback
   - Iterate

## 📝 Notes

- All scripts include error handling and logging
- Backups are created automatically during deployment
- Services restart automatically if they crash
- Database connection pooling is configured
- Application uses production-optimized settings

---

## Quick Reference

**Deploy Application:**
```powershell
.\deployment\Deploy-ToPi.ps1
```

**Check Status:**
```bash
sudo systemctl status restaurant-kiosk
```

**View Logs:**
```bash
sudo journalctl -u restaurant-kiosk -f
```

**Restart Service:**
```bash
sudo systemctl restart restaurant-kiosk
```

**Access Application:**
```
http://raspberry-pi-ip:5000
```

---

**Ready to Deploy?** Start with [deployment/QUICK_START.md](deployment/QUICK_START.md) 🚀

