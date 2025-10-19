# Cash Payment System - Troubleshooting 404 Error

## Problem: Python Script Gets 404 Not Found

The Python script is trying to poll `/api/cash-payment/active-sessions` but getting a 404 error.

## ✅ Quick Fix Checklist

### 1. **Restart the ASP.NET Application**

The new endpoint was just added, so you need to restart the server:

```bash
# Stop any running instance (Ctrl+C)
# Then restart:
cd "C:\Users\cauba\Documents\selwyn dev\2025\Restaurant\RestaurantKiosk\RestaurantKiosk"
dotnet run
```

**Or if using VS/Rider:** Stop debugging and start again.

### 2. **Verify the Endpoint Works**

Test the endpoint directly before running the Python script:

```bash
# Open browser or use curl
http://localhost:5000/api/cash-payment/active-sessions

# Should return:
{
  "success": true,
  "count": 0,
  "sessions": []
}
```

### 3. **Check Python Configuration**

Open `cash_reader_config.json`:

```json
{
  "vps_api_url": "http://localhost:5000",  ← Make sure this matches your running app
  "arduino_port": "/dev/ttyUSB0",
  "baud_rate": 9600
}
```

**Common Issues:**
- ❌ `"vps_api_url": "https://localhost:5000"` (HTTPS won't work in development without cert)
- ❌ `"vps_api_url": "http://localhost:5001"` (Wrong port)
- ✅ `"vps_api_url": "http://localhost:5000"` (Correct for development)

### 4. **Test the Python Script**

```bash
# Make sure app is running first!
cd "C:\Users\cauba\Documents\selwyn dev\2025\Restaurant\RestaurantKiosk\RestaurantKiosk"
python arduino_cash_reader.py
```

**Expected Output:**
```
============================================================
Restaurant Kiosk - Arduino Cash Reader (Polling Mode)
============================================================
VPS API URL: http://localhost:5000
============================================================
Polling VPS for active payment sessions...
No active payment sessions. Waiting for orders...
```

**If you see 404:**
```
Failed to poll active sessions: 404
Connection error polling active sessions
```

## 🔍 Detailed Troubleshooting

### Step 1: Verify ASP.NET App is Running

```powershell
# Check if process is running
Get-Process -Name "RestaurantKiosk" -ErrorAction SilentlyContinue

# Or check the port
netstat -ano | findstr :5000
```

### Step 2: Test Endpoint Manually

**Option A: Browser**
```
http://localhost:5000/api/cash-payment/active-sessions
```

**Option B: PowerShell**
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/active-sessions" -Method Get | ConvertTo-Json
```

**Option C: curl (if installed)**
```bash
curl http://localhost:5000/api/cash-payment/active-sessions
```

### Step 3: Check Application Logs

Look for:
```
Now listening on: http://localhost:5000
Application started. Press Ctrl+C to shut down.
```

### Step 4: Verify Route Registration

Check `Program.cs` has:
```csharp
app.MapControllers();  // This line must exist!
```

### Step 5: Test Other Endpoints

Try the existing status endpoint to confirm API is working:
```bash
# First create a test session
curl -X POST http://localhost:5000/api/cash-payment/init \
  -H "Content-Type: application/json" \
  -d '{"orderNumber": "TEST-001", "totalAmount": 500}'

# Then check if active-sessions shows it
curl http://localhost:5000/api/cash-payment/active-sessions
```

## 📝 Common Scenarios

### Scenario 1: "Connection refused"
**Cause:** ASP.NET app is not running  
**Fix:** Start the app with `dotnet run`

### Scenario 2: "404 Not Found" 
**Cause:** App is running but endpoint not registered  
**Fix:** 
1. Rebuild: `dotnet build`
2. Restart app: `dotnet run`

### Scenario 3: "Cannot find path '/dev/ttyUSB0'"
**Cause:** Arduino not connected or wrong port on Windows  
**Fix:** Update `cash_reader_config.json`:
```json
{
  "arduino_port": "COM3"  // Use Device Manager to find correct COM port
}
```

### Scenario 4: Python script connects but sees no sessions
**Cause:** No payment sessions created yet (this is normal!)  
**Fix:** Create a test session:
1. Open browser: `http://localhost:5000/cash-payment?orderNumber=TEST-001&totalAmount=500`
2. Python script should detect it within 5 seconds

## 🧪 Complete Test Flow

### 1. Start Backend
```bash
cd "C:\Users\cauba\Documents\selwyn dev\2025\Restaurant\RestaurantKiosk\RestaurantKiosk"
dotnet run
```

**Wait for:**
```
Now listening on: http://localhost:5000
```

### 2. Test Endpoint
```bash
# New terminal/PowerShell
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/active-sessions"
```

**Expected:**
```json
{
  "success": true,
  "count": 0,
  "sessions": []
}
```

### 3. Create Test Session
Open browser:
```
http://localhost:5000/cash-payment?orderNumber=TEST-001&totalAmount=500
```

### 4. Verify Session Appears
```bash
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/active-sessions"
```

**Expected:**
```json
{
  "success": true,
  "count": 1,
  "sessions": [
    {
      "orderNumber": "TEST-001",
      "totalRequired": 500,
      "amountInserted": 0,
      "remainingAmount": 500
    }
  ]
}
```

### 5. Start Python Script
```bash
python arduino_cash_reader.py
```

**Expected Output:**
```
============================================================
NEW ORDER WAITING FOR CASH PAYMENT
Order Number: TEST-001
Total Required: ₱500
Please insert cash into the acceptor
============================================================
```

## 🔧 Python Script Configuration Reference

### Development (Local Testing)
```json
{
  "vps_api_url": "http://localhost:5000",
  "arduino_port": "COM3",
  "baud_rate": 9600,
  "api_key": null,
  "environment": "development"
}
```

### Production (VPS Deployment)
```json
{
  "vps_api_url": "https://your-domain.com",
  "arduino_port": "/dev/ttyUSB0",
  "baud_rate": 9600,
  "api_key": "your-secure-api-key-here",
  "environment": "production"
}
```

## 📊 Debug Logs

### Enable Verbose Logging in Python

Edit `arduino_cash_reader.py` line 69:
```python
logging.basicConfig(
    level=logging.DEBUG,  # Change from INFO to DEBUG
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('cash_reader.log'),
        logging.StreamHandler()
    ]
)
```

### Check Logs
```bash
tail -f cash_reader.log
```

Look for:
```
INFO:__main__:Polling VPS for active payment sessions...
DEBUG:urllib3.connectionpool:Starting new HTTP connection (1): localhost:5000
DEBUG:urllib3.connectionpool:http://localhost:5000 "GET /api/cash-payment/active-sessions HTTP/1.1" 200 45
```

If you see `404` instead of `200`, the endpoint isn't being found.

## ✅ Final Checklist

- [ ] ASP.NET app is running (`dotnet run`)
- [ ] Endpoint accessible in browser: `http://localhost:5000/api/cash-payment/active-sessions`
- [ ] `cash_reader_config.json` has correct `vps_api_url`
- [ ] Test session created (browser opened payment page)
- [ ] Python script can connect (no connection errors)
- [ ] Python script sees the test session

If all checkboxes are ✅ and you still have issues, check:
1. Firewall blocking localhost connections
2. Antivirus blocking Python network access
3. Port 5000 already in use by another app

## 🆘 Still Not Working?

**Collect this information:**

1. **ASP.NET console output:**
   ```
   Copy the output from `dotnet run`
   ```

2. **Python console output:**
   ```
   Copy the output from running arduino_cash_reader.py
   ```

3. **Test endpoint result:**
   ```bash
   curl http://localhost:5000/api/cash-payment/active-sessions
   # or
   Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/active-sessions"
   ```

4. **Python config:**
   ```
   Show contents of cash_reader_config.json
   ```

This will help diagnose the exact issue!

