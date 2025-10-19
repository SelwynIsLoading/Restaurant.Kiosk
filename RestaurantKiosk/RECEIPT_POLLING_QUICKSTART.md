# Receipt Printing with Polling Mode - Quick Start

## Why Polling Mode?

Your VPS is in the cloud, but your Raspberry Pi is on your home network behind a NAT router. The VPS **cannot directly reach** the Raspberry Pi's local IP address.

**Polling mode solves this**: The Raspberry Pi connects to your VPS and checks for print jobs every 2 seconds.

```
┌─────────────────────┐           ┌──────────────────────┐
│  VPS (Cloud)        │           │  Home Network        │
│  your-domain.com    │           │  ┌────────────────┐  │
│                     │  ←───────────│ Raspberry Pi   │  │
│  Queue:             │  "Any jobs?"│  Polls every    │  │
│  [Print Jobs]       │           │  │  2 seconds     │  │
└─────────────────────┘           │  └────────────────┘  │
                                  └──────────────────────┘
```

## 5-Minute Setup

### Step 1: Configure VPS (Already Done!)

The polling system is **already configured** in your code:

✅ `PrintQueueService.cs` - Manages print queue
✅ `ReceiptQueueController.cs` - API for Raspberry Pi to poll
✅ `ReceiptService.cs` - Updated to use queue
✅ `Program.cs` - Services registered
✅ `appsettings.json` - `UsePollingMode: true`

Just deploy your updated code!

### Step 2: Setup Raspberry Pi

```bash
# 1. Copy files to Raspberry Pi
cd /home/pi
git clone https://your-repo-url.git RestaurantKiosk
cd RestaurantKiosk

# 2. Install dependencies
pip3 install python-escpos requests

# 3. Configure printer
lsusb  # Find your printer's vendor and product IDs

# 4. Edit configuration
nano receipt_printer_client.py
```

Update these lines:
```python
VPS_API_URL = "https://your-vps-domain.com"  # Your VPS URL
USB_VENDOR_ID = 0x04b8   # Your printer's vendor ID
USB_PRODUCT_ID = 0x0e15  # Your printer's product ID
```

### Step 3: Setup USB Permissions

```bash
# Find your printer IDs from lsusb, then:
echo 'SUBSYSTEM=="usb", ATTRS{idVendor}=="04b8", ATTRS{idProduct}=="0e15", MODE="0666"' | sudo tee /etc/udev/rules.d/99-escpos.rules

# Reload rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### Step 4: Test It

```bash
# Run the client
python3 receipt_printer_client.py
```

You should see:
```
============================================================
Receipt Printer Client - Polling Mode
VPS URL: https://your-vps-domain.com
Poll Interval: 2s
============================================================
INFO - Connecting to usb printer...
INFO - Successfully connected to printer
```

### Step 5: Test with an Order

1. Open your kiosk application
2. Create an order
3. Complete payment
4. Watch the Raspberry Pi logs - you should see:
   ```
   INFO - Received print job: PRINT-20250115-001
   INFO - Printing receipt for order: ORD-20250115-001
   INFO - Receipt printed successfully
   ```

## Run as Background Service

Once it's working, set it up to start automatically:

```bash
# Copy service file
sudo cp deployment/receipt-printer-client.service /etc/systemd/system/

# Enable and start
sudo systemctl enable receipt-printer-client
sudo systemctl start receipt-printer-client

# Check status
sudo systemctl status receipt-printer-client

# View logs
sudo journalctl -u receipt-printer-client -f
```

## Configuration Options

### Adjust Polling Interval

Edit `receipt_printer_client.py`:
```python
POLL_INTERVAL = 2  # Change to 1 for faster, 5 for slower
```

### Switch Between Modes

In `appsettings.json`:
```json
{
  "Receipt": {
    "UsePollingMode": true,  // Set to false for direct HTTP mode
    "PrinterApiUrl": "http://localhost:5001"
  }
}
```

## How It Works

### 1. Order Completed on VPS
```
User completes payment → VPS adds receipt to queue
```

### 2. Raspberry Pi Polls
```
Every 2 seconds → Pi asks: "Any print jobs?"
```

### 3. Print Job Retrieved
```
VPS: "Yes! Here's the receipt data"
Pi: Downloads and prints
```

### 4. Job Completed
```
Pi tells VPS: "Job complete!"
VPS removes job from queue
```

## Monitoring

### Check VPS Queue

```bash
# Via API
curl https://your-vps.com/api/receipt/queue/next
# Returns 204 if no jobs, or job data if pending
```

### Check Raspberry Pi

```bash
# Is the client running?
ps aux | grep receipt_printer_client

# View logs
tail -f receipt_printer_client.log

# Service logs
sudo journalctl -u receipt-printer-client -n 50
```

## Troubleshooting

### "Cannot connect to VPS"

```bash
# Test VPS connectivity
curl https://your-vps-domain.com/health

# Check DNS
ping your-vps-domain.com
```

### "Printer not found"

```bash
# Check USB connection
lsusb

# Check permissions
ls -l /dev/bus/usb/*/*

# Try running with sudo (temporary test)
sudo python3 receipt_printer_client.py
```

### Jobs Not Printing

```bash
# Check if jobs are being queued (on VPS)
# Check application logs

# Check if Pi is polling (on Raspberry Pi)
tail -f receipt_printer_client.log | grep "Checking for print jobs"

# Check printer status
# Look for errors in logs
```

### Service Won't Start

```bash
# Check service status
sudo systemctl status receipt-printer-client

# View errors
sudo journalctl -u receipt-printer-client -n 50 --no-pager

# Common issues:
# - Wrong file path in service file
# - Missing Python packages
# - USB permission issues
```

## Advanced: Manual Testing

### Test Queue on VPS

```bash
# Queue a test print (replace with real order number)
curl -X POST https://your-vps.com/api/receipt/print/ORD-TEST-001
```

### Test Client Manually

```python
# On Raspberry Pi - Python console
from receipt_printer_client import ReceiptPrinterClient

client = ReceiptPrinterClient("https://your-vps.com")
job = client.check_for_print_jobs()
print(job)
```

## Performance

- **Latency**: ~2 seconds (polling interval)
- **Bandwidth**: Minimal (~100 bytes per poll)
- **Reliability**: Very high (automatic retry)
- **Scalability**: Works for single kiosk location

## Security

The polling approach is secure because:
- ✅ Raspberry Pi initiates all connections
- ✅ VPS never needs to know Pi's IP address
- ✅ Works through firewalls and NAT
- ✅ HTTPS encryption (if VPS uses HTTPS)

## Comparison with Other Solutions

| Method | Setup Time | Reliability | Latency |
|--------|------------|-------------|---------|
| **Polling (This)** | 5 minutes | ⭐⭐⭐⭐⭐ | 2 seconds |
| Direct HTTP | Complex | ⭐⭐ (NAT issues) | Instant |
| Cloudflare Tunnel | 15 minutes | ⭐⭐⭐⭐⭐ | Instant |
| VPN | 30 minutes | ⭐⭐⭐⭐ | Instant |

## When to Use Polling Mode

✅ **Use polling mode when:**
- Raspberry Pi is on home network
- VPS is in the cloud
- You need a quick, simple solution
- 2-second delay is acceptable

❌ **Consider alternatives when:**
- You need instant printing (use Cloudflare Tunnel)
- You have multiple locations (use Cloudflare Tunnel or VPN)
- You need bidirectional real-time communication (use WebSocket)

## Next Steps

Once polling mode is working:

1. **Monitor for a few days** to ensure stability
2. **Consider upgrading to Cloudflare Tunnel** for production (see NETWORKING_SOLUTIONS.md)
3. **Set up log rotation** to prevent disk fill
4. **Add monitoring/alerts** for print failures

## Support

If you encounter issues:

1. Check both VPS and Raspberry Pi logs
2. Verify network connectivity
3. Test printer hardware separately
4. Review NETWORKING_SOLUTIONS.md for alternatives
5. Check RECEIPT_PRINTER_SETUP.md for detailed troubleshooting

---

**That's it!** You now have a working receipt printing system that works around the NAT/firewall issue. 🎉

