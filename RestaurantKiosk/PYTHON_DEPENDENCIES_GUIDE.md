# Python Dependencies Installation Guide

## Overview

This guide explains the different methods to install Python dependencies for the Restaurant Kiosk peripherals script on Raspberry Pi.

---

## Quick Reference

### ✅ Recommended Method (Using apt)

```bash
# System-wide installation via apt
sudo apt update
sudo apt install -y python3-serial python3-requests
sudo pip3 install python-escpos --break-system-packages
```

**Why this is recommended:**
- ✅ Works with systemd services
- ✅ Managed by system package manager
- ✅ Automatic updates via `apt upgrade`
- ✅ No PATH or permission issues
- ✅ No virtual environment needed

---

## Installation Methods Comparison

| Method | Use Case | Advantages | Disadvantages |
|--------|----------|------------|---------------|
| **apt (System-wide)** | Production deployment | System integration, auto-updates | Older package versions |
| **pip3 + sudo** | System-wide Python packages | Latest versions | `--break-system-packages` needed (Python 3.11+) |
| **pip3 (user)** | Development/testing | No sudo needed | Doesn't work with systemd |
| **venv + pip** | Isolated development | Clean isolation | Extra setup complexity |

---

## Method 1: apt (System Package Manager) ✅ RECOMMENDED

### Installation

```bash
# Update package lists
sudo apt update

# Install available Python packages via apt
sudo apt install -y python3-serial python3-requests

# Install python-escpos via pip (not in apt repositories)
sudo pip3 install python-escpos --break-system-packages
```

### Package Mapping

| pip Package | apt Package | Available in apt? |
|-------------|-------------|-------------------|
| `pyserial` | `python3-serial` | ✅ Yes |
| `requests` | `python3-requests` | ✅ Yes |
| `python-escpos` | N/A | ❌ No - use pip |

### Advantages

✅ **System-wide installation**
- Packages available to all users
- Works with systemd services (root/pi user)
- No need to worry about user-specific paths

✅ **Package management**
- Managed by apt (Debian package manager)
- Security updates via `apt upgrade`
- Consistent with other system packages

✅ **Stability**
- Tested packages for your OS version
- Fewer compatibility issues
- Well-integrated with system Python

✅ **Easy uninstall**
```bash
sudo apt remove python3-serial python3-requests
```

### Disadvantages

⚠️ **Older versions**
- apt packages are usually older than pip
- Example: `python3-requests` might be 2.28 vs pip's 2.31
- Usually not a problem for our use case

⚠️ **Not all packages available**
- `python-escpos` not in Raspberry Pi OS repos
- Still need pip for some packages

---

## Method 2: pip3 with sudo (System-wide)

### Installation

```bash
# Install to system Python (requires sudo)
sudo pip3 install pyserial requests python-escpos --break-system-packages
```

### Python 3.11+ Note

Python 3.11 introduced "externally managed environments" to prevent breaking system packages. You'll see this error:

```
error: externally-managed-environment
This environment is externally managed
```

**Solutions:**

**Option A: Use `--break-system-packages` flag** (Recommended)
```bash
sudo pip3 install python-escpos --break-system-packages
```

**Option B: Create pip config file**
```bash
# Allow system-wide pip installs
sudo mkdir -p /etc/pip
echo "[global]" | sudo tee /etc/pip/pip.conf
echo "break-system-packages = true" | sudo tee -a /etc/pip/pip.conf
```

### Advantages

✅ **Latest versions**
- Get the newest package versions
- Latest features and bug fixes

✅ **All packages available**
- Access to full PyPI repository
- No waiting for apt packaging

### Disadvantages

⚠️ **System package conflicts**
- Can conflict with apt-installed packages
- `--break-system-packages` flag needed

⚠️ **Manual updates**
- Need to manually update packages
- `sudo pip3 install --upgrade <package>`

---

## Method 3: pip3 User Installation

### Installation

```bash
# Install to user directory (~/.local/lib/python3.x/site-packages)
pip3 install pyserial requests python-escpos
```

### Advantages

✅ **No sudo needed**
- Safe for development
- Can't break system packages

✅ **User-specific**
- Each user can have different versions
- Good for testing

### Disadvantages

❌ **Doesn't work with systemd services**
- Service runs as different user
- Can't find user-installed packages

❌ **PATH issues**
- Need to add `~/.local/bin` to PATH
- Scripts might not find packages

**Not recommended for production deployment!**

---

## Method 4: Virtual Environment (venv)

### Installation

```bash
# Create virtual environment
python3 -m venv ~/kiosk-venv

# Activate it
source ~/kiosk-venv/bin/activate

# Install packages
pip install pyserial requests python-escpos

# Run script
python kiosk_peripherals.py
```

### Systemd Service Configuration

If using venv, update service file:

```ini
[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/kiosk
ExecStart=/home/pi/kiosk-venv/bin/python /home/pi/kiosk/kiosk_peripherals.py
```

### Advantages

✅ **Complete isolation**
- No conflicts with system packages
- Clean dependency management

✅ **Reproducible**
- `requirements.txt` for exact versions
- Easy to replicate on other systems

### Disadvantages

⚠️ **Extra complexity**
- Need to activate venv before running
- Systemd service needs venv path
- More setup steps

⚠️ **Not necessary for single-purpose Pi**
- Overkill if Pi only runs kiosk script
- System-wide installation simpler

---

## Recommended Installation for Production

### For Raspberry Pi (Systemd Service)

```bash
#!/bin/bash

# Step 1: Update system
sudo apt update && sudo apt upgrade -y

# Step 2: Install Python packages via apt (where available)
sudo apt install -y python3-serial python3-requests

# Step 3: Install remaining packages via pip
sudo pip3 install python-escpos --break-system-packages

# Step 4: Verify installation
python3 -c "import serial, requests, escpos; print('✓ All packages installed')"

# Step 5: Copy script and config
sudo cp kiosk_peripherals.py /home/pi/kiosk/
sudo cp cash_reader_config.json /home/pi/kiosk/
sudo chmod +x /home/pi/kiosk/kiosk_peripherals.py

# Step 6: Install systemd service
sudo cp deployment/kiosk-peripherals.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable kiosk-peripherals.service
sudo systemctl start kiosk-peripherals.service
```

---

## Verification

### Check Installed Packages

```bash
# Check apt packages
dpkg -l | grep python3-serial
dpkg -l | grep python3-requests

# Check pip packages
pip3 list | grep pyserial
pip3 list | grep requests
pip3 list | grep python-escpos

# Or check all at once
python3 << EOF
try:
    import serial
    print("✓ pyserial:", serial.__version__)
except ImportError:
    print("✗ pyserial not found")

try:
    import requests
    print("✓ requests:", requests.__version__)
except ImportError:
    print("✗ requests not found")

try:
    import escpos
    print("✓ python-escpos: installed")
except ImportError:
    print("✗ python-escpos not found")
EOF
```

### Test Import

```bash
# Quick test
python3 -c "import serial, requests, escpos; print('All modules OK')"
```

---

## Package Versions

### Minimum Required Versions

| Package | Minimum Version | Reason |
|---------|-----------------|--------|
| `pyserial` | 3.0+ | Serial port support |
| `requests` | 2.20+ | HTTPS/TLS support |
| `python-escpos` | 3.0+ | Thermal printer commands |

### Current Versions (as of 2025)

| Package | Raspberry Pi OS apt | PyPI (pip) |
|---------|-------------------|------------|
| `python3-serial` | 3.5 | 3.5+ |
| `python3-requests` | 2.28 | 2.31+ |
| `python-escpos` | N/A | 3.0+ |

✅ **apt versions are sufficient for our needs**

---

## Troubleshooting

### Issue: "externally-managed-environment" error

**Solution:**
```bash
# Use --break-system-packages flag
sudo pip3 install python-escpos --break-system-packages
```

### Issue: Package not found when running as service

**Cause:** Installed with user pip (`pip3` without sudo)

**Solution:**
```bash
# Reinstall system-wide
sudo pip3 install pyserial requests python-escpos --break-system-packages

# Or use apt
sudo apt install -y python3-serial python3-requests
```

### Issue: Permission denied when using serial port

**Cause:** User not in `dialout` group

**Solution:**
```bash
sudo usermod -a -G dialout pi
sudo reboot
```

### Issue: Import error for escpos

**Check installation:**
```bash
pip3 show python-escpos
# or
sudo pip3 show python-escpos
```

**Reinstall if needed:**
```bash
sudo pip3 install --upgrade --force-reinstall python-escpos --break-system-packages
```

---

## Uninstallation

### Remove apt packages

```bash
sudo apt remove python3-serial python3-requests
sudo apt autoremove
```

### Remove pip packages

```bash
# If installed with sudo
sudo pip3 uninstall python-escpos pyserial requests

# If installed without sudo
pip3 uninstall python-escpos pyserial requests
```

---

## Best Practices

### ✅ DO:
- Use apt for packages available in repositories
- Use `--break-system-packages` for pip on Python 3.11+
- Verify packages after installation
- Document your installation method
- Keep packages updated via `apt upgrade`

### ❌ DON'T:
- Mix apt and user pip for same package
- Install system packages without sudo in production
- Use venv for single-purpose Raspberry Pi
- Forget to add user to `dialout` group

---

## Summary

### For Production (Raspberry Pi with systemd):

```bash
# Best approach - mix of apt and pip
sudo apt update
sudo apt install -y python3-serial python3-requests
sudo pip3 install python-escpos --break-system-packages
```

**Why:**
- ✅ System-wide (works with systemd)
- ✅ Uses apt where available (better integration)
- ✅ Falls back to pip when needed
- ✅ Automatic security updates for apt packages
- ✅ No virtual environment complexity

### For Development:

```bash
# User-level or venv
pip3 install pyserial requests python-escpos
# or
python3 -m venv venv && source venv/bin/activate && pip install -r requirements.txt
```

---

**Last Updated:** October 17, 2025  
**Recommended Method:** apt + pip (system-wide)  
**Status:** ✅ Production Ready

