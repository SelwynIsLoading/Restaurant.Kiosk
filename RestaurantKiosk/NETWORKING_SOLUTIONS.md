# Networking Solutions for VPS to Raspberry Pi Communication

## The Problem

Your VPS is in the cloud with a public IP address, but your Raspberry Pi is on a home network with a private IP (192.168.x.x) behind a NAT router. The VPS **cannot directly reach** the Raspberry Pi.

```
┌─────────────────────┐           ┌──────────────────────┐
│  VPS (Cloud)        │           │  Home Network        │
│  Public IP:         │           │  ┌────────────────┐  │
│  203.0.113.10       │    ✗      │  │ Raspberry Pi   │  │
│                     │───────────│  │ 192.168.1.100  │  │
└─────────────────────┘  Blocked  │  └────────────────┘  │
                         by NAT   │                      │
                                  └──────────────────────┘
```

## Solution Comparison

| Solution | Complexity | Security | Reliability | Best For |
|----------|------------|----------|-------------|----------|
| **Polling (Recommended)** | Low | High | High | Your setup |
| Cloudflare Tunnel | Medium | High | Very High | Production |
| WebSocket/SignalR | Medium | Medium | High | Real-time needs |
| VPN (WireGuard) | Medium | Very High | Very High | Multiple devices |
| Port Forwarding | Low | Low ⚠️ | Medium | Not recommended |

---

## ✅ Solution 1: Polling Architecture (RECOMMENDED)

**Best for your setup!** Simple, secure, and works immediately.

### How It Works

```
┌─────────────────────┐           ┌──────────────────────┐
│  VPS (Cloud)        │           │  Home Network        │
│                     │           │  ┌────────────────┐  │
│  Queue:             │  ←─────────  │ Raspberry Pi   │  │
│  [Print Jobs]       │  "Give me  │  polls VPS      │  │
│                     │   next job" │  every 2 sec    │  │
└─────────────────────┘           │  └────────────────┘  │
                                  └──────────────────────┘
```

### Implementation

I've created the polling solution for you:

**Raspberry Pi Side:**
- `receipt_printer_client.py` - Polls VPS every 2 seconds for print jobs

**VPS Side:**
- `PrintQueueService.cs` - Manages print queue
- `ReceiptQueueController.cs` - API endpoints for queue

### Setup Instructions

#### 1. Update VPS Configuration

Edit `Program.cs`:
```csharp
// Add this service
builder.Services.AddSingleton<IPrintQueueService, PrintQueueService>();
```

Edit `ReceiptService.cs` - change from direct HTTP to queue:
```csharp
public async Task<bool> PrintReceiptAsync(ReceiptData receiptData)
{
    // Instead of HTTP call, queue the print job
    var jobId = await _printQueueService.QueuePrintJobAsync(receiptData);
    _logger.LogInformation("Queued print job {JobId} for order {OrderNumber}", 
        jobId, receiptData.OrderNumber);
    return true; // Job queued successfully
}
```

#### 2. On Raspberry Pi

```bash
# Install dependencies (if not already installed)
pip3 install python-escpos requests

# Edit the VPS URL in receipt_printer_client.py
nano receipt_printer_client.py
# Change: VPS_API_URL = "https://your-vps-domain.com"

# Run the client
python3 receipt_printer_client.py
```

#### 3. Run as Service

```bash
# Create service file
sudo nano /etc/systemd/system/receipt-printer-client.service
```

Add:
```ini
[Unit]
Description=Receipt Printer Client (Polling Mode)
After=network.target

[Service]
Type=simple
User=pi
WorkingDirectory=/home/pi/RestaurantKiosk
ExecStart=/usr/bin/python3 /home/pi/RestaurantKiosk/receipt_printer_client.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable receipt-printer-client
sudo systemctl start receipt-printer-client
```

### Advantages
- ✅ Simple to set up
- ✅ No port forwarding needed
- ✅ No firewall changes
- ✅ Works with any home router
- ✅ Secure (Pi initiates connection)
- ✅ Works immediately

### Disadvantages
- ❌ ~2 second delay (polling interval)
- ❌ Slight bandwidth usage (minimal)

---

## Solution 2: Cloudflare Tunnel (Production Grade)

You already have Cloudflare Tunnel setup scripts! This is the **best production solution**.

### How It Works

```
┌─────────────────────┐           ┌──────────────────────┐
│  VPS (Cloud)        │           │  Home Network        │
│  203.0.113.10       │           │  ┌────────────────┐  │
│         │           │           │  │ Raspberry Pi   │  │
│         └───────────┼───────────┼──│ Cloudflare     │  │
│    via Cloudflare   │  Tunnel   │  │ Tunnel (5001)  │  │
│    pi-printer.      │           │  └────────────────┘  │
│    yourdomain.com   │           │                      │
└─────────────────────┘           └──────────────────────┘
```

### Setup

```bash
# On Raspberry Pi - use your existing script
cd deployment
bash setup-cloudflare-tunnel.sh

# Expose the printer service
cloudflared tunnel route dns restaurant-kiosk pi-printer.yourdomain.com
```

Update `appsettings.json`:
```json
{
  "Receipt": {
    "PrinterApiUrl": "https://pi-printer.yourdomain.com"
  }
}
```

### Advantages
- ✅ Production-grade
- ✅ No polling delay
- ✅ Secure (encrypted tunnel)
- ✅ No port forwarding
- ✅ Free tier available
- ✅ Automatic HTTPS

---

## Solution 3: SignalR/WebSocket (Real-Time)

Use your existing SignalR infrastructure!

### How It Works

```
Raspberry Pi connects to VPS via WebSocket and listens for print commands
```

### Implementation

#### On Raspberry Pi
```python
from signalr import Connection

hub = Connection("https://your-vps.com/receipthub")
hub.start()

def on_print_receipt(receipt_data):
    print_receipt(receipt_data)

hub.on("PrintReceipt", on_print_receipt)
```

#### On VPS
```csharp
// In CashPaymentController.CompletePayment()
await _receiptHub.Clients.All.SendAsync("PrintReceipt", receiptData);
```

---

## Solution 4: VPN (WireGuard)

Create a virtual private network between VPS and Raspberry Pi.

### Setup

**On VPS:**
```bash
sudo apt install wireguard
wg genkey | tee privatekey | wg pubkey > publickey

sudo nano /etc/wireguard/wg0.conf
```

```ini
[Interface]
Address = 10.0.0.1/24
PrivateKey = <VPS_PRIVATE_KEY>
ListenPort = 51820

[Peer]
PublicKey = <PI_PUBLIC_KEY>
AllowedIPs = 10.0.0.2/32
```

**On Raspberry Pi:**
```bash
sudo apt install wireguard
# Similar setup with 10.0.0.2 address
```

Then VPS can access Pi at `10.0.0.2:5001`

---

## Solution 5: Port Forwarding ⚠️ (Not Recommended)

Forward port on home router to Raspberry Pi.

### Why NOT Recommended
- ❌ Security risk (exposes Pi to internet)
- ❌ Dynamic IP issues
- ❌ Router configuration needed
- ❌ Not practical for multiple locations

---

## Recommended Setup

### For Development
Use **Polling** - simple and works immediately

### For Production
Use **Cloudflare Tunnel** - secure, reliable, production-grade

### Alternative
Use **WebSocket/SignalR** - if you need real-time communication

---

## Migration Guide

### From Direct HTTP to Polling

1. **Install new files** (already provided):
   - `receipt_printer_client.py`
   - `PrintQueueService.cs`
   - `ReceiptQueueController.cs`

2. **Update `Program.cs`**:
```csharp
builder.Services.AddSingleton<IPrintQueueService, PrintQueueService>();
```

3. **Update `ReceiptService.cs`**:
```csharp
private readonly IPrintQueueService _printQueueService;

public async Task<bool> PrintReceiptAsync(ReceiptData receiptData)
{
    var jobId = await _printQueueService.QueuePrintJobAsync(receiptData);
    return true;
}
```

4. **On Raspberry Pi**:
```bash
# Stop old service
sudo systemctl stop receipt-printer

# Start new client
python3 receipt_printer_client.py
```

---

## Testing

### Test Polling Setup

1. **Start VPS application**
2. **Start Raspberry Pi client**
3. **Create an order and complete payment**
4. **Check logs:**

```bash
# VPS logs - should show job queued
grep "Queued print job" /var/log/restaurant-kiosk.log

# Pi logs - should show job received and printed
tail -f receipt_printer_client.log
```

---

## Monitoring

### Check Queue Status

```bash
# Number of pending jobs
curl http://your-vps.com/api/receipt/queue/status

# Check specific job
curl http://your-vps.com/api/receipt/queue/status/JOB-ID
```

### Raspberry Pi Health

```bash
# Check if client is running
ps aux | grep receipt_printer_client

# Check logs
tail -f receipt_printer_client.log
```

---

## Troubleshooting

### Pi Not Receiving Jobs

1. **Check Pi can reach VPS:**
```bash
curl https://your-vps.com/health
```

2. **Check polling is working:**
```bash
# Should see requests every 2 seconds
tail -f receipt_printer_client.log | grep "Checking for print jobs"
```

3. **Check VPS queue:**
```bash
curl http://your-vps.com/api/receipt/queue/next
# Should return 204 if no jobs
```

### Jobs Stuck in Queue

1. **Check job status:**
```bash
curl http://your-vps.com/api/receipt/queue/status/JOB-ID
```

2. **Clear old jobs** (add to PrintQueueService):
```csharp
public async Task ClearOldJobsAsync()
{
    var cutoff = DateTime.UtcNow.AddHours(-1);
    // Remove jobs older than 1 hour
}
```

---

## Performance Considerations

### Polling Interval

- **2 seconds**: Good balance (default)
- **1 second**: Faster but more requests
- **5 seconds**: Slower but less bandwidth

Adjust in `receipt_printer_client.py`:
```python
POLL_INTERVAL = 2  # Change this value
```

### Queue Size

For high volume, consider:
- Using Redis instead of in-memory queue
- Database persistence
- Queue monitoring and alerts

---

## Security

### Best Practices

1. **Use HTTPS** for VPS (you already have this)
2. **Add API key authentication** (optional):

```python
# In receipt_printer_client.py
headers = {"X-API-Key": "your-secret-key"}
response = self.session.get(url, headers=headers)
```

3. **Restrict endpoints** to polling client:
```csharp
[Authorize(Policy = "PrinterClient")]
public class ReceiptQueueController
```

---

## Summary

**Recommended Solution**: **Polling Architecture**

- Simple to implement (already provided!)
- No networking complexity
- Secure and reliable
- Works immediately

**For Production**: Consider **Cloudflare Tunnel**

- Uses your existing setup
- Production-grade reliability
- Better performance

Both solutions solve the NAT problem without requiring port forwarding or complex VPN setup.

