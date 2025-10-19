# Cash Payment System Testing Guide

## Quick Start Testing (Without Hardware)

### 1. Start the Application
```bash
cd RestaurantKiosk
dotnet run
```

The application should be running on `https://localhost:5001` or `http://localhost:5000`

### 2. Create a Test Order

1. Navigate to `http://localhost:5000/kiosk`
2. Add items to cart
3. Go to checkout
4. Fill in customer information
5. Select "Cash" as payment method
6. Click "Place Order"

You should be redirected to the cash payment page.

### 3. Simulate Cash Insertion (PowerShell)

Open a new PowerShell window and run these commands to simulate cash being inserted:

```powershell
# Get the order number from the URL (e.g., ORD-20250114-ABC123)
$orderNumber = "ORD-20250114-ABC123"  # Replace with actual order number from URL

# Simulate inserting ₱100
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/test/simulate" `
    -Method Post `
    -ContentType "application/json" `
    -Body (@{orderNumber=$orderNumber; amount=100} | ConvertTo-Json)

# Wait a moment, then insert another ₱100
Start-Sleep -Seconds 2
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/test/simulate" `
    -Method Post `
    -ContentType "application/json" `
    -Body (@{orderNumber=$orderNumber; amount=100} | ConvertTo-Json)

# Continue until the total amount is reached
# The UI will automatically update in real-time
```

### 4. Observe the UI

Watch the cash payment page update in real-time as you send the simulate requests:
- Amount Inserted should increase
- Remaining Balance should decrease
- Progress bar should fill up
- When sufficient cash is received, payment completes automatically

## Testing with cURL (Linux/Mac/Git Bash)

```bash
# Get order number from URL
ORDER_NUMBER="ORD-20250114-ABC123"

# Simulate ₱100 bill
curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d "{\"orderNumber\":\"$ORDER_NUMBER\",\"amount\":100}"

# Simulate ₱50 bill
curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d "{\"orderNumber\":\"$ORDER_NUMBER\",\"amount\":50}"

# Simulate ₱20 bill
curl -X POST http://localhost:5000/api/cash-payment/test/simulate \
  -H "Content-Type: application/json" \
  -d "{\"orderNumber\":\"$ORDER_NUMBER\",\"amount\":20}"
```

## Testing Payment Cancellation

```powershell
# PowerShell
$orderNumber = "ORD-20250114-ABC123"
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/cancel/$orderNumber" -Method Post
```

```bash
# cURL
curl -X POST http://localhost:5000/api/cash-payment/cancel/$ORDER_NUMBER
```

## Testing with Python Script (Simulated Hardware)

Create a test script `test_cash_payment.py`:

```python
import requests
import time
import random

# Configuration
API_URL = "http://localhost:5000"
ORDER_NUMBER = "ORD-20250114-ABC123"  # Update with actual order number

# Available denominations
BILLS = [20, 50, 100, 200, 500, 1000]
COINS = [1, 5, 10]

def simulate_cash_insertion(order_number, amount):
    """Simulate inserting cash"""
    url = f"{API_URL}/api/cash-payment/test/simulate"
    payload = {
        "orderNumber": order_number,
        "amount": amount
    }
    
    response = requests.post(url, json=payload)
    if response.status_code == 200:
        data = response.json()
        print(f"✓ Inserted ₱{amount}")
        print(f"  Total: ₱{data['amountInserted']} / ₱{data['totalRequired']}")
        print(f"  Remaining: ₱{data['remainingAmount']}")
        
        if data['isComplete']:
            print("\n🎉 Payment completed!")
            return True
    else:
        print(f"✗ Error: {response.status_code}")
    
    return False

def main():
    print(f"Simulating cash payment for order: {ORDER_NUMBER}")
    print("=" * 60)
    
    # Simulate random cash insertions
    while True:
        # Randomly choose bill or coin
        if random.random() > 0.3:
            amount = random.choice(BILLS)
        else:
            amount = random.choice(COINS)
        
        completed = simulate_cash_insertion(ORDER_NUMBER, amount)
        
        if completed:
            break
        
        # Wait a bit before next insertion
        time.sleep(2)

if __name__ == "__main__":
    main()
```

Run it:
```bash
pip install requests
python test_cash_payment.py
```

## Testing with Arduino Hardware

### Prerequisites
1. Arduino board (Uno, Mega, etc.)
2. Bill acceptor (e.g., JY-15A, ICT A7+)
3. Coin acceptor (e.g., CH-923, 616)
4. Connecting wires
5. Raspberry Pi (for production) or Windows PC (for testing)

### Setup Steps

1. **Upload Arduino Sketch**
   ```bash
   # Open Arduino IDE
   # Load arduino_cash_acceptor.ino
   # Select your board and port
   # Click Upload
   ```

2. **Install Python Dependencies**
   ```bash
   pip install -r requirements.txt
   ```

3. **Update Configuration**
   
   Edit `arduino_cash_reader.py`:
   ```python
   # For Windows testing:
   ARDUINO_PORT = "COM3"  # Check Device Manager for actual port
   KIOSK_API_URL = "http://localhost:5000"
   
   # For Raspberry Pi production:
   ARDUINO_PORT = "/dev/ttyUSB0"  # or /dev/ttyACM0
   KIOSK_API_URL = "http://localhost:5000"  # or Pi's IP
   ```

4. **Run the Python Script**
   ```bash
   python arduino_cash_reader.py
   ```

5. **Test the Flow**
   - Start a cash payment in the UI
   - The Python script should output: `ORDER:ORD-20250114-ABC123`
   - Insert bills/coins into acceptors
   - Watch the UI update in real-time
   - Payment completes automatically when sufficient cash received

### Hardware Testing Checklist

- [ ] Arduino connects successfully
- [ ] Bill acceptor pulses detected
- [ ] Coin acceptor pulses detected
- [ ] Correct denominations reported
- [ ] Real-time UI updates working
- [ ] Payment completes automatically
- [ ] Change calculated correctly
- [ ] Order sent to kitchen
- [ ] Product quantities decreased
- [ ] Cancel payment works
- [ ] Money returned on cancel

## API Testing

### Check Payment Session Status

```powershell
# PowerShell
$orderNumber = "ORD-20250114-ABC123"
Invoke-RestMethod -Uri "http://localhost:5000/api/cash-payment/status/$orderNumber"
```

```bash
# cURL
curl http://localhost:5000/api/cash-payment/status/$ORDER_NUMBER
```

Expected response:
```json
{
  "success": true,
  "orderNumber": "ORD-20250114-ABC123",
  "amountInserted": 150.00,
  "totalRequired": 450.00,
  "remainingAmount": 300.00,
  "change": 0.00,
  "status": "Active",
  "startedAt": "2025-01-14T10:30:00Z",
  "completedAt": null
}
```

## SignalR Testing (Browser Console)

Open browser console on the cash payment page and run:

```javascript
// Check if connection is active
console.log("SignalR state:", window._hubConnection?.state);

// You should see real-time messages in console when cash is inserted
```

## Common Issues and Solutions

### Issue: "Payment session not found"
**Solution**: Make sure you initialized the session by navigating to `/cash-payment` with valid parameters

### Issue: UI not updating
**Solutions**:
- Check browser console for SignalR connection errors
- Verify SignalR hub is registered in Program.cs
- Check that the order number matches
- Try refreshing the page

### Issue: Python script can't connect to Arduino
**Solutions**:
- Check USB cable connection
- Verify correct COM port in script
- Install Arduino drivers
- Check permissions (Linux: `sudo usermod -a -G dialout $USER`)
- Try different USB port

### Issue: Wrong amounts detected
**Solution**: 
- Check pulse mapping in Arduino code
- Consult your acceptor's datasheet
- Calibrate pulse counts for your specific hardware

### Issue: API returns 404
**Solution**: 
- Verify the application is running
- Check the correct port (5000 for HTTP, 5001 for HTTPS)
- Update `KIOSK_API_URL` in Python script

## Performance Testing

Test with rapid cash insertion:

```python
import requests
import concurrent.futures

ORDER_NUMBER = "ORD-20250114-ABC123"
API_URL = "http://localhost:5000"

def insert_cash(amount):
    requests.post(
        f"{API_URL}/api/cash-payment/test/simulate",
        json={"orderNumber": ORDER_NUMBER, "amount": amount}
    )

# Simulate 10 rapid insertions
with concurrent.futures.ThreadPoolExecutor(max_workers=10) as executor:
    amounts = [20, 50, 100, 20, 50, 100, 20, 50, 100, 20]
    executor.map(insert_cash, amounts)
```

## Production Readiness Checklist

- [ ] All tests passing
- [ ] Hardware properly connected
- [ ] Python script runs as system service
- [ ] API authentication enabled
- [ ] HTTPS configured
- [ ] Error logging enabled
- [ ] Monitoring setup
- [ ] Backup power supply for Pi
- [ ] Cash dispenser for change (if needed)
- [ ] Staff training completed

## Next Steps

After successful testing:
1. Deploy to Raspberry Pi
2. Setup system services
3. Enable security features
4. Configure monitoring
5. Train staff
6. Go live!

See `CASH_PAYMENT_SETUP.md` for detailed production deployment instructions.

