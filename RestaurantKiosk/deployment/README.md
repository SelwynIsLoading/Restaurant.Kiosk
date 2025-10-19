# Deployment Scripts

This directory contains automated scripts for deploying the Restaurant Kiosk application to Raspberry Pi.

## Quick Start

### First Time Setup on Raspberry Pi

1. **Transfer setup script to Raspberry Pi:**
   ```bash
   scp setup-pi.sh pi@restaurant-kiosk.local:~/
   ```

2. **Run initial setup:**
   ```bash
   ssh pi@restaurant-kiosk.local
   chmod +x setup-pi.sh
   ./setup-pi.sh
   ```
   
   This will:
   - Install .NET 9 Runtime
   - Install PostgreSQL
   - Create database
   - Install Python dependencies
   - Configure system

3. **Logout and login again** (for group permissions to take effect)

### Deploy Application

1. **On your development machine, publish the application:**
   ```powershell
   # Windows PowerShell
   cd RestaurantKiosk
   dotnet publish -c Release -r linux-arm64 --self-contained false -o ./publish
   Compress-Archive -Path ./publish/* -DestinationPath restaurant-kiosk.zip
   ```

2. **Transfer files to Raspberry Pi:**
   ```powershell
   # Transfer deployment package
   scp restaurant-kiosk.zip pi@restaurant-kiosk.local:~/
   
   # Transfer deployment script
   scp deployment/deploy.sh pi@restaurant-kiosk.local:~/
   ```

3. **Run deployment on Raspberry Pi:**
   ```bash
   ssh pi@restaurant-kiosk.local
   chmod +x deploy.sh
   ./deploy.sh
   ```

### Install Services

1. **Transfer service installation script:**
   ```bash
   scp deployment/install-services.sh pi@restaurant-kiosk.local:~/
   ```

2. **Install systemd services:**
   ```bash
   ssh pi@restaurant-kiosk.local
   chmod +x install-services.sh
   ./install-services.sh
   ```

## Scripts Overview

### setup-pi.sh
Initial Raspberry Pi setup script. Run this once on a fresh Pi.

**What it does:**
- Updates system packages
- Installs .NET 9 Runtime
- Installs and configures PostgreSQL
- Creates database and user
- Installs Python packages for Arduino
- Optionally installs Nginx and Chromium
- Creates necessary directories
- Applies performance optimizations

**Usage:**
```bash
chmod +x setup-pi.sh
./setup-pi.sh
```

### deploy.sh
Application deployment script. Run this every time you want to update the application.

**What it does:**
- Stops running services
- Creates backup of current installation
- Extracts new application version
- Sets correct permissions
- Optionally runs database migrations
- Starts services
- Verifies deployment

**Prerequisites:**
- `restaurant-kiosk.zip` must be in the same directory
- Services must be installed (run `install-services.sh` first)

**Usage:**
```bash
chmod +x deploy.sh
./deploy.sh
```

### install-services.sh
Creates and installs systemd service files.

**What it does:**
- Creates systemd service file for main application
- Optionally creates service for Arduino cash reader
- Enables services to start on boot
- Reloads systemd daemon

**Usage:**
```bash
chmod +x install-services.sh
./install-services.sh
```

## Step-by-Step Deployment Workflow

### Initial Deployment

```bash
# 1. On Raspberry Pi - Initial setup
ssh pi@restaurant-kiosk.local
./setup-pi.sh
logout

# 2. Log back in
ssh pi@restaurant-kiosk.local

# 3. Install services
./install-services.sh

# 4. Deploy application
./deploy.sh

# 5. Verify deployment
sudo systemctl status restaurant-kiosk
```

### Updating Application

```bash
# 1. On development machine - Build and transfer
cd RestaurantKiosk
dotnet publish -c Release -r linux-arm64 --self-contained false -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath restaurant-kiosk.zip -Force
scp restaurant-kiosk.zip pi@restaurant-kiosk.local:~/

# 2. On Raspberry Pi - Deploy update
ssh pi@restaurant-kiosk.local
./deploy.sh
```

## Configuration Files

After running `setup-pi.sh`, you'll find:

- `~/kiosk-config/connection-string.txt` - Database connection string
- `/var/www/restaurant-kiosk/` - Application directory
- `~/backups/` - Database backup directory
- `~/arduino-env/` - Python virtual environment for Arduino script

## Troubleshooting

### Script Permissions
```bash
chmod +x setup-pi.sh deploy.sh install-services.sh
```

### View Deployment Logs
```bash
sudo journalctl -u restaurant-kiosk -f
```

### Rollback Deployment
```bash
# List backups
ls -la /var/www/ | grep restaurant-kiosk.backup

# Restore backup
sudo systemctl stop restaurant-kiosk
sudo rm -rf /var/www/restaurant-kiosk
sudo mv /var/www/restaurant-kiosk.backup.YYYYMMDD_HHMMSS /var/www/restaurant-kiosk
sudo systemctl start restaurant-kiosk
```

### Database Connection Issues
```bash
# Check connection string
cat ~/kiosk-config/connection-string.txt

# Test database connection
psql -U kiosk_user -h localhost -d restaurant_kiosk
```

### Service Won't Start
```bash
# Check service status
sudo systemctl status restaurant-kiosk

# Check detailed logs
sudo journalctl -u restaurant-kiosk -n 100 --no-pager

# Test manual start
cd /var/www/restaurant-kiosk
ASPNETCORE_ENVIRONMENT=Production ~/.dotnet/dotnet RestaurantKiosk.dll
```

## Advanced Options

### Custom Installation Directory

Edit the scripts and change:
```bash
APP_DIR="/your/custom/path"
```

### Different Database Configuration

Edit `appsettings.Production.json` in the application directory:
```bash
nano /var/www/restaurant-kiosk/appsettings.Production.json
```

### Automated Deployments

For automated deployments, you can:

1. **Use SSH keys** for password-less authentication:
   ```bash
   ssh-keygen -t rsa
   ssh-copy-id pi@restaurant-kiosk.local
   ```

2. **Create a deployment script on your dev machine**:
   ```powershell
   # deploy-to-pi.ps1
   dotnet publish -c Release -r linux-arm64 --self-contained false -o ./publish
   Compress-Archive -Path ./publish/* -DestinationPath restaurant-kiosk.zip -Force
   scp restaurant-kiosk.zip pi@restaurant-kiosk.local:~/
   ssh pi@restaurant-kiosk.local "./deploy.sh"
   ```

3. **Run with one command**:
   ```powershell
   .\deploy-to-pi.ps1
   ```

## Security Considerations

1. **Change default passwords** after initial setup
2. **Use SSH keys** instead of passwords
3. **Configure firewall** (see main deployment guide)
4. **Keep connection strings secure** - stored in `~/kiosk-config/` with restricted permissions
5. **Regular backups** - scripts don't delete old backups automatically

## Support

For detailed information, see:
- [RASPBERRY_PI_DEPLOYMENT.md](../RASPBERRY_PI_DEPLOYMENT.md) - Complete deployment guide
- [LOCAL_DEVELOPMENT_SETUP.md](../LOCAL_DEVELOPMENT_SETUP.md) - Development setup
- [CASH_PAYMENT_SETUP.md](../CASH_PAYMENT_SETUP.md) - Arduino integration

## Script Maintenance

These scripts are designed for Raspberry Pi OS (Debian Bookworm) with .NET 9. If you're using different versions, you may need to adjust:

- .NET version in `setup-pi.sh`
- PostgreSQL configuration
- Service file paths
- Package names

