# Arduino Cash Acceptor Protocol

## Serial Communication Protocol

The Arduino communicates with the Raspberry Pi via serial USB connection at **9600 baud**.

## Message Format

All messages are sent as plain text strings terminated by a newline character (`\n`).

### Commands from Raspberry Pi to Arduino

#### 1. Start Order Session
```
ORDER:<order_number>
```
**Example**: `ORDER:ORD-20250114-ABC123`

**Description**: Initializes a new payment session. The Arduino will start accepting and reporting cash for this order.

**Arduino Response**: Echoes the command back
```
ORDER:ORD-20250114-ABC123
```

---

#### 2. Cancel Payment
```
CANCEL
```

**Description**: Cancels the current payment session and resets the acceptors.

**Arduino Response**: Echoes the command back
```
CANCEL
```

---

#### 3. Health Check
```
PING
```

**Description**: Checks if Arduino is responsive.

**Arduino Response**:
```
PONG
```

---

### Commands from Arduino to Raspberry Pi

#### 1. Bill Inserted
```
BILL:<amount>
```

**Example**: `BILL:100`

**Description**: Reports that a bill has been inserted. The amount is in Philippine Pesos (₱).

**Common Values**: 20, 50, 100, 200, 500, 1000

---

#### 2. Coin Inserted
```
COIN:<amount>
```

**Example**: `COIN:5.00`

**Description**: Reports that a coin has been inserted. The amount is in Philippine Pesos (₱) with 2 decimal places.

**Common Values**: 1.00, 5.00, 10.00

---

#### 3. Order Session Started
```
ORDER:<order_number>
```

**Example**: `ORDER:ORD-20250114-ABC123`

**Description**: Confirms that the order session has been started.

---

#### 4. Payment Cancelled
```
CANCEL
```

**Description**: Confirms that the payment session has been cancelled.

---

#### 5. Ready Status
```
Arduino Cash Acceptor Ready
Waiting for order...
```

**Description**: Sent on startup to indicate the Arduino is ready to receive commands.

---

## Communication Flow

### Normal Payment Flow

```
                Raspberry Pi                      Arduino
                     |                                |
                     |-------- ORDER:ORD-001 -------->|
                     |<------- ORDER:ORD-001 ---------|
                     |                                |
                     |              [Customer inserts ₱100 bill]
                     |<--------- BILL:100 ------------|
                     |                                |
                     |              [Customer inserts ₱50 bill]
                     |<--------- BILL:50 -------------|
                     |                                |
                     |              [Customer inserts ₱5 coin]
                     |<--------- COIN:5.00 -----------|
                     |                                |
                     |         [Payment complete - handled by API]
                     |                                |
```

### Cancelled Payment Flow

```
                Raspberry Pi                      Arduino
                     |                                |
                     |-------- ORDER:ORD-001 -------->|
                     |<------- ORDER:ORD-001 ---------|
                     |                                |
                     |              [Customer inserts ₱100 bill]
                     |<--------- BILL:100 ------------|
                     |                                |
                     |         [Customer cancels]     |
                     |-------- CANCEL --------------->|
                     |<------- CANCEL ----------------|
                     |                                |
```

## Hardware Pulse Detection

Most bill and coin acceptors output pulses to indicate denominations:

### Pulse-Based Detection
```
Physical Event          Arduino Detection        Serial Output
    ↓                          ↓                       ↓
Bill inserted          Interrupt triggered      BILL:<amount>
(3 pulses)        →    Count pulses (3)    →    (e.g., BILL:100)
                       Wait for timeout
                       Map to denomination
```

### Typical Pulse Mappings

These vary by acceptor model - **check your device datasheet!**

#### Example Bill Acceptor Mapping
| Pulses | Denomination |
|--------|--------------|
| 1      | ₱20          |
| 2      | ₱50          |
| 3      | ₱100         |
| 4      | ₱200         |
| 5      | ₱500         |
| 6      | ₱1000        |

#### Example Coin Acceptor Mapping
| Pulses | Denomination |
|--------|--------------|
| 1      | ₱1           |
| 5      | ₱5           |
| 10     | ₱10          |

## Timing Specifications

- **Baud Rate**: 9600
- **Data Bits**: 8
- **Parity**: None
- **Stop Bits**: 1
- **Pulse Timeout**: 200ms (time to wait after last pulse before processing)
- **Debounce Delay**: 50ms (minimum time between pulses)

## Arduino Pin Configuration

```cpp
const int BILL_ACCEPTOR_PIN = 2;  // Digital pin 2 (INT0)
const int COIN_ACCEPTOR_PIN = 3;  // Digital pin 3 (INT1)
const int LED_PIN = 13;           // Built-in LED
```

### Wiring Diagram

```
Bill Acceptor               Arduino Uno
┌──────────────┐           ┌────────────┐
│   Pulse Out  ├───────────┤ Pin 2      │
│   GND        ├───────────┤ GND        │
│   VCC (12V)  ├───────────┤ VIN/5V*    │
└──────────────┘           └────────────┘

Coin Acceptor               Arduino Uno
┌──────────────┐           ┌────────────┐
│   Pulse Out  ├───────────┤ Pin 3      │
│   GND        ├───────────┤ GND        │
│   VCC (12V)  ├───────────┤ VIN/5V*    │
└──────────────┘           └────────────┘

* Use external 12V power supply for acceptors
  Arduino can only supply 5V/limited current
```

## Python Implementation Example

```python
import serial
import time

# Connect to Arduino
ser = serial.Serial('/dev/ttyUSB0', 9600, timeout=1)
time.sleep(2)  # Wait for Arduino reset

# Start payment session
order_number = "ORD-20250114-ABC123"
ser.write(f"ORDER:{order_number}\n".encode())

# Read responses
while True:
    if ser.in_waiting > 0:
        line = ser.readline().decode('utf-8').strip()
        
        if line.startswith("BILL:"):
            amount = float(line.split(":")[1])
            print(f"Bill inserted: ₱{amount}")
            # Send to API...
            
        elif line.startswith("COIN:"):
            amount = float(line.split(":")[1])
            print(f"Coin inserted: ₱{amount}")
            # Send to API...
```

## Error Handling

### Communication Errors

**Timeout**: If no response within 1 second, retry command
```python
try:
    ser.write(b"PING\n")
    response = ser.readline().decode('utf-8', errors='ignore')
    if response.strip() != "PONG":
        # Reconnect
except serial.SerialException:
    # Reconnect
```

**Garbled Data**: Use error='ignore' when decoding
```python
line = ser.readline().decode('utf-8', errors='ignore')
```

**Port Disconnected**: Monitor exceptions and reconnect
```python
try:
    # Read from serial
except serial.SerialException:
    ser.close()
    time.sleep(5)
    ser = serial.Serial(port, baud_rate)
```

## Testing Without Hardware

### Serial Port Emulator

Use virtual serial ports for testing:

**Windows**: [com0com](https://sourceforge.net/projects/com0com/)
**Linux**: socat

```bash
# Linux - Create virtual serial port pair
socat -d -d pty,raw,echo=0 pty,raw,echo=0
# Use the displayed ports (e.g., /dev/pts/2 and /dev/pts/3)
```

### Manual Serial Testing

Use Arduino IDE Serial Monitor or screen:

```bash
# Linux/Mac
screen /dev/ttyUSB0 9600

# Windows - use Arduino IDE Serial Monitor

# Type commands:
ORDER:TEST-001
BILL:100
COIN:5.00
CANCEL
```

## Security Considerations

### For Production

1. **Validate Amounts**: Check that amounts match expected denominations
   ```python
   VALID_BILLS = [20, 50, 100, 200, 500, 1000]
   if amount not in VALID_BILLS:
       logger.warning(f"Invalid bill amount: {amount}")
       return
   ```

2. **Rate Limiting**: Prevent rapid-fire fake insertions
   ```python
   if time.time() - last_insertion_time < 0.5:
       logger.warning("Too rapid insertion, possible fraud")
       return
   ```

3. **Secure Serial Port**: 
   - Set proper permissions (read-only for Python process)
   - Don't expose serial port to network

4. **Log Everything**: 
   - All insertions should be logged with timestamps
   - Monitor for suspicious patterns

## Troubleshooting

### No Response from Arduino
1. Check USB connection
2. Verify correct COM port
3. Check baud rate (9600)
4. Press reset button on Arduino
5. Re-upload sketch

### Wrong Amounts Detected
1. Check pulse mapping in code
2. Consult acceptor datasheet
3. Use oscilloscope to measure actual pulses
4. Adjust PULSE_TIMEOUT value

### Multiple Pulses Counted as One
1. Increase PULSE_TIMEOUT (currently 200ms)
2. Check debounce delay

### Pulses Missed
1. Decrease PULSE_TIMEOUT
2. Check wiring/connections
3. Verify interrupt pins are correct

## Advanced Features

### Future Enhancements

1. **Denomination Selection**: Some acceptors allow enabling/disabling specific denominations
2. **Acceptance Control**: Enable/disable acceptance programmatically
3. **Status LEDs**: More detailed visual feedback
4. **LCD Display**: Show amounts on local display
5. **Cash Dispenser**: Automated change dispensing
6. **Anti-fraud**: Detect counterfeit bills (hardware dependent)

## References

- [Arduino Serial Reference](https://www.arduino.cc/reference/en/language/functions/communication/serial/)
- [Arduino Interrupts](https://www.arduino.cc/reference/en/language/functions/external-interrupts/attachinterrupt/)
- [Python pySerial Documentation](https://pyserial.readthedocs.io/)

## Support

For hardware-specific questions, consult your bill/coin acceptor's datasheet or contact the manufacturer.

