# Restaurant Kiosk Deployment Checklist

Use this checklist to track your deployment progress to Raspberry Pi.

## Pre-Deployment

### Hardware Setup
- [ ] Raspberry Pi 4 (4GB+) or Pi 5 acquired
- [ ] 32GB+ MicroSD card or SSD
- [ ] Official power supply (5V 3A for Pi 4, 5V 5A for Pi 5)
- [ ] Touchscreen or monitor connected
- [ ] Keyboard and mouse for initial setup
- [ ] Network connection (Ethernet or WiFi)
- [ ] Cooling solution (heatsink/fan) installed
- [ ] Arduino (if using cash payment)
- [ ] Arduino uploaded with cash acceptor sketch

### Raspberry Pi OS Installation
- [ ] Downloaded Raspberry Pi Imager
- [ ] Installed Raspberry Pi OS (64-bit) - Debian Bookworm
- [ ] Configured hostname: `restaurant-kiosk`
- [ ] Enabled SSH
- [ ] Set username and password
- [ ] Configured WiFi (if applicable)
- [ ] Set locale and timezone
- [ ] First boot successful
- [ ] Can SSH into Pi: `ssh pi@restaurant-kiosk.local`

### Development Machine Setup
- [ ] .NET 9 SDK installed on dev machine
- [ ] Application builds successfully on dev machine
- [ ] OpenSSH client installed (Windows)
- [ ] Can connect to Pi via SSH
- [ ] SCP/WinSCP/FileZilla available for file transfer

## Initial Pi Setup (One-Time)

### System Configuration
- [ ] Transferred `setup-pi.sh` to Pi
- [ ] Made script executable: `chmod +x setup-pi.sh`
- [ ] Ran `./setup-pi.sh` successfully
- [ ] System packages updated
- [ ] Basic tools installed (git, curl, wget, etc.)

### .NET Runtime
- [ ] .NET 9 runtime installed
- [ ] `dotnet --list-runtimes` shows ASP.NET Core 9.0
- [ ] DOTNET_ROOT added to PATH
- [ ] Can run .NET commands

### Database Setup
- [ ] PostgreSQL installed
- [ ] PostgreSQL service running
- [ ] Database created: `restaurant_kiosk`
- [ ] Database user created: `kiosk_user`
- [ ] User has proper permissions
- [ ] Connection string saved to `~/kiosk-config/connection-string.txt`
- [ ] Can connect: `psql -U kiosk_user -h localhost -d restaurant_kiosk`

### Python & Arduino
- [ ] Python 3 installed
- [ ] pip installed
- [ ] Virtual environment created: `~/arduino-env`
- [ ] pyserial package installed
- [ ] User added to `dialout` group
- [ ] Logged out and back in for group changes
- [ ] Arduino detected: `ls /dev/ttyACM*` or `/dev/ttyUSB*`
- [ ] Can communicate with Arduino

### Optional Components
- [ ] Nginx installed (if using reverse proxy)
- [ ] Chromium installed (for kiosk mode)
- [ ] unclutter and xdotool installed
- [ ] Performance optimizations applied

## Application Deployment

### Service Installation
- [ ] Transferred `install-services.sh` to Pi
- [ ] Made script executable
- [ ] Ran `./install-services.sh`
- [ ] Main service created: `/etc/systemd/system/restaurant-kiosk.service`
- [ ] Arduino service created: `/etc/systemd/system/arduino-cash-reader.service`
- [ ] Services enabled for auto-start
- [ ] Systemd daemon reloaded

### Build & Package
- [ ] Application builds successfully: `dotnet publish -c Release -r linux-arm64`
- [ ] Published to `./publish` directory
- [ ] Created deployment package: `restaurant-kiosk.zip`
- [ ] Package size reasonable (< 100MB typically)

### Deploy to Pi
- [ ] Transferred deployment package to Pi
- [ ] Transferred `deploy.sh` to Pi
- [ ] Made deploy script executable
- [ ] Ran `./deploy.sh`
- [ ] Deployment completed without errors
- [ ] Application extracted to `/var/www/restaurant-kiosk/`
- [ ] Permissions set correctly
- [ ] Service started successfully

### Configuration
- [ ] Created/updated `appsettings.Production.json`
- [ ] Updated database connection string
- [ ] Updated Xendit API keys (production)
- [ ] Updated BaseUrl (if using domain)
- [ ] Configured allowed hosts
- [ ] Set Kestrel endpoint (http://0.0.0.0:5000)
- [ ] Environment variable set: `ASPNETCORE_ENVIRONMENT=Production`

### Database Migrations
- [ ] Migrations applied successfully
- [ ] Database schema up to date
- [ ] Identity tables created
- [ ] Products table exists
- [ ] Categories table exists
- [ ] Orders table exists
- [ ] OrderItems table exists

## Verification & Testing

### Application Running
- [ ] Service status: `sudo systemctl status restaurant-kiosk` shows active
- [ ] No errors in logs: `sudo journalctl -u restaurant-kiosk -n 50`
- [ ] Application listening on port 5000
- [ ] Can access via browser: `http://raspberry-pi-ip:5000`
- [ ] Home page loads correctly
- [ ] Static files (CSS, JS, images) loading

### Functionality Testing
- [ ] Admin console accessible
- [ ] Can login/register
- [ ] Categories page loads
- [ ] Products page loads
- [ ] Can add/edit/delete categories
- [ ] Can add/edit/delete products
- [ ] Kiosk mode accessible: `/kioskstart`
- [ ] Can add items to cart
- [ ] Checkout page works
- [ ] Payment integration working (Xendit)
- [ ] Orders are created successfully
- [ ] Order items saved correctly

### Arduino Integration (if applicable)
- [ ] Arduino service running: `sudo systemctl status arduino-cash-reader`
- [ ] Arduino connected and detected
- [ ] Cash payment hub accessible
- [ ] Can accept cash payments
- [ ] Bills detected correctly
- [ ] Payment confirmation works
- [ ] Cash payment flow complete

### Performance
- [ ] Application responds quickly (< 2 seconds)
- [ ] CPU usage reasonable (< 50% idle)
- [ ] Memory usage acceptable (< 2GB)
- [ ] Temperature under 70°C: `vcgencmd measure_temp`
- [ ] No memory leaks over time
- [ ] Database queries optimized

## Kiosk Mode Setup (Optional)

### Browser Configuration
- [ ] Chromium installed
- [ ] Kiosk start script created: `~/kiosk-start.sh`
- [ ] Script made executable
- [ ] Script tested manually
- [ ] Cursor hidden (unclutter)
- [ ] Screen blanking disabled
- [ ] Chromium starts in kiosk mode
- [ ] Application loads automatically

### Auto-Start
- [ ] Autostart directory created: `~/.config/lxsession/LXDE-pi/`
- [ ] Autostart file configured
- [ ] Kiosk script added to autostart
- [ ] Auto-login enabled (if desired)
- [ ] Tested reboot - kiosk starts automatically
- [ ] Touchscreen calibrated (if needed)
- [ ] Touch input working correctly

## Production Readiness

### Security
- [ ] Changed default Pi password
- [ ] Changed database password
- [ ] Updated Xendit to production keys (not sandbox)
- [ ] Configured firewall (ufw)
- [ ] SSH access secured (keys instead of password)
- [ ] Unnecessary services disabled
- [ ] HTTPS configured (if using Nginx)
- [ ] Connection strings not exposed
- [ ] Application secrets secured

### Monitoring & Maintenance
- [ ] Database backup script created
- [ ] Backup cron job scheduled
- [ ] Log rotation configured
- [ ] Disk space monitoring set up
- [ ] Remote monitoring configured (optional)
- [ ] Update procedure documented
- [ ] Rollback procedure tested
- [ ] Contact information for support documented

### Documentation
- [ ] Deployment documented
- [ ] Configuration settings documented
- [ ] Admin credentials stored securely
- [ ] Database connection details documented
- [ ] Arduino setup documented
- [ ] Troubleshooting guide available
- [ ] Maintenance procedures documented

## Post-Deployment

### Data Setup
- [ ] Admin user account created
- [ ] Categories added
- [ ] Products added
- [ ] Product images uploaded
- [ ] Prices configured
- [ ] Tax rates set
- [ ] Service charges configured

### Testing with Real Users
- [ ] Staff training completed
- [ ] Test orders processed
- [ ] Payment flow tested end-to-end
- [ ] Cash payment tested (if applicable)
- [ ] Receipt/confirmation tested
- [ ] Error handling tested
- [ ] Network interruption handling tested
- [ ] Power failure recovery tested

### Go-Live
- [ ] Backup before go-live
- [ ] Final smoke test
- [ ] Monitoring in place
- [ ] Support contact available
- [ ] Rollback plan ready
- [ ] Soft launch completed
- [ ] Full deployment
- [ ] Post-deployment verification
- [ ] User feedback collected

## Maintenance Schedule

### Daily
- [ ] Check service status
- [ ] Review error logs
- [ ] Monitor disk space

### Weekly
- [ ] Database backup verification
- [ ] Performance monitoring
- [ ] Check for application updates

### Monthly
- [ ] System updates: `sudo apt update && sudo apt upgrade`
- [ ] Security review
- [ ] Performance optimization review
- [ ] Clear old logs
- [ ] Database optimization

## Troubleshooting Contacts

| Issue Type | Contact/Resource |
|------------|-----------------|
| Application Issues | [Your contact info] |
| Database Issues | [Your contact info] |
| Hardware Issues | [Vendor contact] |
| Payment Issues | Xendit Support |
| Network Issues | [IT contact] |

## Important Information

### Access Details
```
Raspberry Pi SSH: pi@restaurant-kiosk.local
Application URL: http://[IP]:5000
Database: restaurant_kiosk on localhost:5432
Logs: sudo journalctl -u restaurant-kiosk -f
```

### File Locations
```
Application: /var/www/restaurant-kiosk/
Configuration: /var/www/restaurant-kiosk/appsettings.Production.json
Database Connection: ~/kiosk-config/connection-string.txt
Backups: ~/backups/
Logs: sudo journalctl -u restaurant-kiosk
```

### Quick Commands
```bash
# Service Management
sudo systemctl status restaurant-kiosk
sudo systemctl restart restaurant-kiosk

# Logs
sudo journalctl -u restaurant-kiosk -f

# Database
psql -U kiosk_user -h localhost -d restaurant_kiosk

# System Info
vcgencmd measure_temp
htop
df -h
```

## Notes

Use this space for deployment-specific notes:

```
Deployment Date: _______________
Deployed By: _______________
Pi Serial Number: _______________
Location: _______________
Network Details: _______________
Special Configuration: _______________

Issues Encountered:


Solutions Applied:


Performance Notes:


```

---

## Sign-Off

- [ ] All critical items completed
- [ ] All tests passed
- [ ] Documentation complete
- [ ] Training completed
- [ ] Ready for production

**Deployed By:** _______________  
**Date:** _______________  
**Signature:** _______________

