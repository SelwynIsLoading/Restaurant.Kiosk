# Migration Guide: Separate Scripts → Unified Peripherals Manager

This guide helps you migrate from the separate `arduino_cash_reader.py` and `receipt_printer_client.py` scripts to the unified `kiosk_peripherals.py` script.

## Why Migrate?

### Before (Separate Scripts)
```
├── arduino_cash_reader.py      (Process 1)
│   ├── cash_reader_config.json
│   └── Service: arduino-cash-reader.service
│
└── receipt_printer_client.py   (Process 2)
    ├── printer_config.json (if separate)
    └── Service: receipt-printer-client.service
```

**Issues:**
- Two processes to manage
- Two services to monitor
- Two configuration files to maintain
- Duplicate code for HTTP, logging, error handling
- More resource usage

### After (Unified Script)
```
└── kiosk_peripherals.py         (Single Process)
    ├── cash_reader_config.json  (unified config)
    └── Service: kiosk-peripherals.service
```

**Benefits:**
- Single process with two threads
- One service to manage
- One configuration file
- Shared resources (HTTP session, logging)
- Lower resource usage
- Can enable/disable modules independently

## Quick Migration

### Option 1: Automated Installation (Recommended)

```bash
# On your development machine
cd RestaurantKiosk

# Copy files to Raspberry Pi
scp kiosk_peripherals.py pi@raspberrypi:~/kiosk/
scp deployment/kiosk-peripherals.service pi@raspberrypi:~/kiosk/
scp deployment/install-unified-peripherals.sh pi@raspberrypi:~/kiosk/
scp kiosk_peripherals_config.example.json pi@raspberrypi:~/kiosk/

# SSH to Raspberry Pi
ssh pi@raspberrypi

# Run installation script
cd ~/kiosk
chmod +x install-unified-peripherals.sh
./install-unified-peripherals.sh
```

The script will:
1. ✓ Stop old services
2. ✓ Backup old scripts
3. ✓ Install dependencies
4. ✓ Set up new service
5. ✓ Start unified service

### Option 2: Manual Migration

#### Step 1: Prepare Configuration

Merge your existing configurations into one file:

```bash
cd ~/kiosk

# If you have separate configs, merge them
nano cash_reader_config.json
```

Example unified configuration:
```json
{
  "vps_api_url": "https://bochogs-kiosk.store",
  "api_key": "your-api-key-here",
  "environment": "production",
  
  "enable_cash_reader": true,
  "enable_printer": true,
  
  "arduino_port": "/dev/ttyUSB0",
  "arduino_baud_rate": 9600,
  "cash_poll_interval": 5,
  
  "printer_type": "serial",
  "printer_serial_port": "/dev/ttyUSB1",
  "printer_serial_baudrate": 9600,
  "printer_poll_interval": 2,
  
  "reconnect_delay_seconds": 5,
  "connection_timeout_seconds": 10,
  "retry_attempts": 3
}
```

#### Step 2: Stop Old Services

```bash
sudo systemctl stop receipt-printer-client.service
sudo systemctl stop arduino-cash-reader.service  # if running as service
sudo systemctl disable receipt-printer-client.service
sudo systemctl disable arduino-cash-reader.service
```

#### Step 3: Install New Script

```bash
# Install dependencies (if not already installed)
# Using apt (recommended - system-wide installation)
sudo apt update
sudo apt install -y python3-serial python3-requests
sudo pip3 install python-escpos --break-system-packages

# Or using pip (user-level installation)
# pip3 install pyserial requests python-escpos

# Copy new script
cp kiosk_peripherals.py ~/kiosk/
chmod +x ~/kiosk/kiosk_peripherals.py
```

#### Step 4: Set Up New Service

```bash
# Copy service file
sudo cp ~/kiosk/kiosk-peripherals.service /etc/systemd/system/
sudo chmod 644 /etc/systemd/system/kiosk-peripherals.service

# Reload systemd
sudo systemctl daemon-reload

# Enable service
sudo systemctl enable kiosk-peripherals.service
```

#### Step 5: Test Before Starting

```bash
# Test manually first
cd ~/kiosk
python3 kiosk_peripherals.py
```

Watch the output for any errors. Press Ctrl+C to stop when satisfied.

#### Step 6: Start Service

```bash
sudo systemctl start kiosk-peripherals.service

# Check status
sudo systemctl status kiosk-peripherals.service

# View logs
sudo journalctl -u kiosk-peripherals.service -f
```

## Configuration Changes

### Serial Port Mapping

If both Arduino and printer use USB/Serial connections, they need different ports:

```bash
# List USB devices
ls -l /dev/ttyUSB*

# Typical output:
# /dev/ttyUSB0 -> Arduino
# /dev/ttyUSB1 -> Printer
```

Update config:
```json
{
  "arduino_port": "/dev/ttyUSB0",
  "printer_serial_port": "/dev/ttyUSB1"
}
```

### Module Control

You can enable/disable modules independently:

```json
{
  "enable_cash_reader": true,   // Set to false to disable
  "enable_printer": true         // Set to false to disable
}
```

This is useful for:
- Testing individual modules
- Partial deployments (e.g., only printer, no cash)
- Troubleshooting

## Verification

### 1. Check Service Status

```bash
sudo systemctl status kiosk-peripherals.service
```

Expected output:
```
● kiosk-peripherals.service - Restaurant Kiosk Peripherals Manager
   Loaded: loaded (/etc/systemd/system/kiosk-peripherals.service; enabled)
   Active: active (running) since ...
```

### 2. Check Logs

```bash
# Live log tail
sudo journalctl -u kiosk-peripherals.service -f

# Or check log file
tail -f ~/kiosk/kiosk_peripherals.log
```

Look for:
```
[CASH] Starting cash reader loop...
[PRINTER] Starting receipt printer loop...
[CASH] Successfully connected to Arduino
[PRINTER] Successfully connected to printer
```

### 3. Test Functionality

**Test Cash Reader:**
1. Create a cash payment order from the kiosk
2. Insert cash into the acceptor
3. Watch logs for `[CASH] Bill inserted` or `[CASH] Coin inserted`

**Test Printer:**
1. Complete an order
2. Watch logs for `[PRINTER] Received print job`
3. Receipt should print automatically

## Troubleshooting

### Service Won't Start

**Check configuration:**
```bash
cd ~/kiosk
python3 kiosk_peripherals.py
```

This runs the script manually and shows detailed errors.

### Can't Find Serial Port

**List USB devices:**
```bash
ls -l /dev/ttyUSB*
dmesg | grep tty
```

**Update permissions:**
```bash
sudo usermod -a -G dialout pi
sudo reboot
```

### One Module Not Working

**Test individual modules:**

Disable the working module in config:
```json
{
  "enable_cash_reader": true,
  "enable_printer": false  // Disable printer to test cash reader only
}
```

Restart and test:
```bash
sudo systemctl restart kiosk-peripherals.service
```

### High CPU Usage

**Increase polling intervals:**
```json
{
  "cash_poll_interval": 10,      // Increase from 5
  "printer_poll_interval": 5     // Increase from 2
}
```

## Rollback (If Needed)

If you need to rollback to the separate scripts:

```bash
# Stop unified service
sudo systemctl stop kiosk-peripherals.service
sudo systemctl disable kiosk-peripherals.service

# Restore backup scripts
cd ~/kiosk
mv arduino_cash_reader.py.backup.* arduino_cash_reader.py
mv receipt_printer_client.py.backup.* receipt_printer_client.py

# Start old services
sudo systemctl enable receipt-printer-client.service
sudo systemctl start receipt-printer-client.service
# (and cash reader service if applicable)
```

## Cleanup (After Successful Migration)

Once you've confirmed the unified script works:

```bash
# Remove old service files
sudo rm /etc/systemd/system/receipt-printer-client.service
sudo rm /etc/systemd/system/arduino-cash-reader.service
sudo systemctl daemon-reload

# Remove old scripts (optional - keep as backup)
rm ~/kiosk/arduino_cash_reader.py.backup.*
rm ~/kiosk/receipt_printer_client.py.backup.*
```

## Support

For detailed information, see:
- `PERIPHERALS_UNIFIED_SETUP.md` - Complete setup documentation
- `kiosk_peripherals_config.example.json` - Example configuration

For issues:
1. Check logs: `sudo journalctl -u kiosk-peripherals.service -f`
2. Test manually: `python3 ~/kiosk/kiosk_peripherals.py`
3. Verify configuration: `cat ~/kiosk/cash_reader_config.json`
4. Check serial ports: `ls -l /dev/ttyUSB*`

