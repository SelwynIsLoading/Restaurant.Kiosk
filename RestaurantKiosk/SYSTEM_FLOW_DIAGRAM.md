# Restaurant Kiosk - Complete System Flow Diagram

## Overview Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         CUSTOMER SIDE                           │
│                                                                 │
│  ┌──────────────────┐                                          │
│  │   Web Browser    │ ← Customer creates order                 │
│  │   (Kiosk UI)     │ ← Selects CASH payment                   │
│  │                  │ ← Sees real-time cash insertion updates  │
│  └────────┬─────────┘                                          │
│           │ HTTPS Polling (every 1s)                           │
│           │ GET /api/cash-payment/status/{orderNumber}         │
└───────────┼─────────────────────────────────────────────────────┘
            │
            ↓
┌─────────────────────────────────────────────────────────────────┐
│                         VPS SERVER                              │
│                    (ASP.NET Core API)                           │
│                 bochogs-kiosk.store                             │
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐   │
│  │   In-Memory Payment Sessions                           │   │
│  │   {                                                    │   │
│  │     "ORD-12345": {                                     │   │
│  │       "totalRequired": 250.00,                         │   │
│  │       "amountInserted": 150.00,                        │   │
│  │       "isComplete": false                              │   │
│  │     }                                                  │   │
│  │   }                                                    │   │
│  └────────────────────────────────────────────────────────┘   │
│                                                                 │
│  API Endpoints:                                                 │
│  • GET  /api/cash-payment/active-sessions                      │
│  • POST /api/cash-payment/update                               │
│  • GET  /api/cash-payment/status/{orderNumber}                 │
│  • GET  /api/receipt/queue/next                                │
│  • POST /api/receipt/queue/complete/{jobId}                    │
└───────────┬─────────────────────────────┬───────────────────────┘
            │                             │
            │ HTTPS (polling)             │ HTTPS (polling)
            │ Every 5s                    │ Every 2s
            ↓                             ↓
┌─────────────────────────────────────────────────────────────────┐
│                    RASPBERRY PI (Home Network)                  │
│                 kiosk_peripherals.py (Unified)                  │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │              Single Python Process                       │  │
│  │                                                          │  │
│  │  ┌───────────────────┐      ┌───────────────────────┐  │  │
│  │  │  Thread 1         │      │  Thread 2             │  │  │
│  │  │  Cash Reader      │      │  Printer Client       │  │  │
│  │  │                   │      │                       │  │  │
│  │  │  1. Poll VPS      │      │  1. Poll VPS          │  │  │
│  │  │  2. Get active    │      │  2. Get print jobs    │  │  │
│  │  │     sessions      │      │  3. Print receipt     │  │  │
│  │  │  3. Read Arduino  │      │  4. Mark complete     │  │  │
│  │  │  4. Parse data    │      │                       │  │  │
│  │  │  5. POST to VPS   │      │                       │  │  │
│  │  └─────────┬─────────┘      └────────┬──────────────┘  │  │
│  │            │                          │                  │  │
│  │            │ Shared HTTP Session      │                  │  │
│  │            │ Shared Logging           │                  │  │
│  │            │ Shared Configuration     │                  │  │
│  └────────────┼──────────────────────────┼──────────────────┘  │
│               │                          │                     │
│         USB Serial                  USB Serial                │
│         9600 baud                   9600 baud                 │
│               ↓                          ↓                     │
│   ┌──────────────────────┐   ┌──────────────────────┐        │
│   │  Arduino Uno         │   │  Thermal Printer     │        │
│   │  (Cash Acceptor)     │   │  (SHK24 58mm)        │        │
│   │                      │   │                      │        │
│   │  • Bill Acceptor Pin │   │  • ESC/POS Protocol  │        │
│   │  • Coin Acceptor Pin │   │  • Paper Width: 58mm │        │
│   │  • Pulse Detection   │   │  • USB-to-TTL        │        │
│   └──────────┬───────────┘   └──────────────────────┘        │
│              │                                                 │
│        12V Power Supply                                        │
│              │                                                 │
│   ┌──────────▼───────────┐                                    │
│   │  Bill/Coin Acceptor  │                                    │
│   │  (Physical Hardware) │                                    │
│   └──────────────────────┘                                    │
└─────────────────────────────────────────────────────────────────┘
```

## Detailed Message Flow

### Scenario: Customer Pays ₱250 Order with Cash

#### Step 1: Order Creation
```
Browser                    VPS API
   │                         │
   │─── POST /orders ────────>│
   │    { amount: 250 }       │
   │                         │
   │<── 200 OK ──────────────│
   │    { orderNumber: "ORD-12345" }
   │                         │
   │─── Select CASH payment ─>│
   │                         │
   │<── Payment session ─────│
   │    created              │
```

#### Step 2: Raspberry Pi Detects Session
```
Raspberry Pi              VPS API
   │                         │
   │─── Poll (every 5s) ─────>│
   │    GET /active-sessions  │
   │                         │
   │<── 200 OK ──────────────│
   │    { sessions: [        │
   │      { orderNumber: "ORD-12345",
   │        totalRequired: 250,
   │        amountInserted: 0 }
   │    ]}                   │
   │                         │
   │  Auto-select: current_order = "ORD-12345"
```

#### Step 3: Cash Insertion (First Bill)
```
Bill Acceptor    Arduino              Python              VPS
     │              │                     │                 │
     │─ ₱100 bill ─>│                     │                 │
     │              │ Count pulses        │                 │
     │              │ (3 pulses = ₱100)   │                 │
     │              │                     │                 │
     │              │─── "BILL:100\n" ───>│                 │
     │              │                     │ Parse: 100.0    │
     │              │                     │ Order: ORD-12345│
     │              │                     │                 │
     │              │                     │─── POST ───────>│
     │              │                     │ /cash-payment/  │
     │              │                     │  update         │
     │              │                     │ { orderNumber:  │
     │              │                     │   "ORD-12345",  │
     │              │                     │   amountAdded:  │
     │              │                     │   100.0 }       │
     │              │                     │                 │
     │              │                     │<── 200 OK ──────│
     │              │                     │ { totalInserted:│
     │              │                     │   100.0,        │
     │              │                     │   remaining:    │
     │              │                     │   150.0,        │
     │              │                     │   isComplete:   │
     │              │                     │   false }       │
     │              │                     │                 │
     │              │<── ✓ Bill: ₱100 ───│                 │
```

#### Step 4: Browser Updates
```
Browser                    VPS API
   │                         │
   │─── Poll (every 1s) ─────>│
   │    GET /status/ORD-12345 │
   │                         │
   │<── 200 OK ──────────────│
   │    { amountInserted: 100.0,
   │      totalRequired: 250.0,
   │      remaining: 150.0,
   │      isComplete: false }
   │                         │
   │  Update UI:             │
   │  "Inserted: ₱100"       │
   │  "Remaining: ₱150"      │
```

#### Step 5: Cash Insertion (Second Bill)
```
Bill Acceptor    Arduino         Python              VPS
     │              │                │                 │
     │─ ₱100 bill ─>│                │                 │
     │              │─ "BILL:100" ──>│── POST ────────>│
     │              │                │   (same flow)   │
     │              │                │<─ 200 OK ───────│
     │              │                │  { totalInserted: 200.0 }
```

#### Step 6: Cash Insertion (Final Coin)
```
Coin Acceptor    Arduino         Python              VPS
     │              │                │                 │
     │─ ₱50 coin ──>│                │                 │
     │              │─ "COIN:50" ───>│── POST ────────>│
     │              │                │                 │
     │              │                │<─ 200 OK ───────│
     │              │                │  { totalInserted: 250.0,
     │              │                │    isComplete: true }
     │              │                │                 │
     │              │                │  current_order = None
```

#### Step 7: Receipt Printing
```
VPS                      Python (Printer)        Thermal Printer
 │                            │                        │
 │─── Queue print job ───────>│                        │
 │    { jobId: "job-123",     │                        │
 │      orderNumber: "ORD-12345",                      │
 │      receipt: {...} }      │                        │
 │                            │                        │
 │                            │─── Initialize ────────>│
 │                            │─── Print header ──────>│
 │                            │─── Print items ───────>│
 │                            │─── Print totals ──────>│
 │                            │─── Cut paper ─────────>│
 │                            │                        │
 │<─── POST complete ─────────│                        │
 │     /queue/complete/job-123│                        │
 │                            │                        │
 │  Remove from queue         │                        │
```

## Arduino Message Protocol

### Arduino → Python (Serial Communication)

```
Time    Arduino Sends              Python Receives         Action
────────────────────────────────────────────────────────────────────
00:00   "READY\n"                  Parse: READY           Log: Arduino ready
        "# Arduino Cash...v2.0\n"  Parse: comment         Log at DEBUG
        "# Polling Arch...\n"      Parse: comment         Log at DEBUG
        
05:32   "BILL:100\n"               Parse: BILL:100        Create CashUpdate
                                   Amount: 100.0          POST to VPS
                                   Order: ORD-12345       
        
05:38   "COIN:5\n"                 Parse: COIN:5          Create CashUpdate
                                   Amount: 5.0            POST to VPS
        
30:00   "# Heartbeat...Bills:2\n"  Parse: comment         Log at DEBUG
        
(Test)  
Manual  Send "TEST:BILL:100\n"     Arduino processes      
via     <────────────────────────  Sends: "BILL:100\n"   Testing mode
Serial                                                     
```

### Python → Arduino (Optional Commands)

```
Python Sends         Arduino Receives       Arduino Responds
───────────────────────────────────────────────────────────────
"PING\n"            Parse command          "PONG\n"
"STATUS\n"          Parse command          "# Status: Bills=2 Coins=1\n"
"RESET\n"           Parse command          "# Counters reset\n"
"TEST:BILL:100\n"   Parse test command     "BILL:100\n"
"TEST:COIN:5\n"     Parse test command     "COIN:5\n"
```

## Configuration Flow

```
┌────────────────────────────────────────────────────────────┐
│  cash_reader_config.json                                   │
│  {                                                         │
│    "vps_api_url": "https://bochogs-kiosk.store",          │
│    "enable_cash_reader": true,                            │
│    "enable_printer": true,                                │
│    "arduino_port": "/dev/ttyUSB0",                        │
│    "arduino_baud_rate": 9600,                             │
│    "printer_serial_port": "/dev/ttyUSB1"                  │
│  }                                                         │
└──────────────────────┬─────────────────────────────────────┘
                       │
                       ↓
┌────────────────────────────────────────────────────────────┐
│  kiosk_peripherals.py                                      │
│                                                            │
│  config = load_config()                                    │
│                                                            │
│  cash_reader = ArduinoCashReader(                         │
│      port=config["arduino_port"],                         │
│      baud_rate=config["arduino_baud_rate"],               │
│      api_url=config["vps_api_url"]                        │
│  )                                                         │
│                                                            │
│  printer = ReceiptPrinterClient(                          │
│      vps_url=config["vps_api_url"]                        │
│  )                                                         │
│                                                            │
│  Run both in separate threads                             │
└────────────────────────────────────────────────────────────┘
```

## Error Handling Flow

```
┌─────────────────────────────────────────────────────────────┐
│  Potential Errors & Recovery                                │
└─────────────────────────────────────────────────────────────┘

Error: Arduino Disconnected
   │
   ├─> Detect: serial.SerialException
   ├─> Action: log error, close connection
   ├─> Wait: 5 seconds (reconnect_delay)
   └─> Retry: attempt reconnection
       │
       └─> Success: continue operation
           Failure: log, wait, retry

Error: VPS Unreachable
   │
   ├─> Detect: requests.ConnectionError
   ├─> Action: log error
   ├─> Retry: up to 3 attempts with 2s delay
   └─> Result:
       │
       ├─> Success: update sent
       └─> Failure: log error, cash data lost
                    (operator must manually verify)

Error: Invalid Arduino Data
   │
   ├─> Detect: ValueError during parsing
   ├─> Action: log error with original data
   └─> Continue: don't crash, wait for next valid message

Error: Cash Inserted, No Active Order
   │
   ├─> Detect: current_order is None
   ├─> Action: log warning
   └─> Result: cash insertion ignored
               (operator must manually handle)
```

## Performance Metrics

```
Component            Operation              Timing         Notes
──────────────────────────────────────────────────────────────────
Arduino              Pulse detection        < 50ms         Hardware dependent
Arduino              Pulse timeout          300ms          Wait for all pulses
Arduino              Heartbeat interval     30s            Keepalive message

Python (Cash)        VPS polling            5s             Configurable
Python (Cash)        Serial read            0.1s loop      Fast response
Python (Printer)     VPS polling            2s             Configurable
Python (Printer)     Print job              2-5s           Hardware dependent

VPS                  Browser polling        1s             Real-time updates
VPS                  Session timeout        5 minutes      Configurable

End-to-End           Cash → UI update       < 2s           Typical case
End-to-End           Complete payment       Varies         Depends on bills
```

## Security Considerations

```
┌─────────────────────────────────────────────────────────────┐
│  Security Layer                 Implementation               │
├─────────────────────────────────────────────────────────────┤
│  VPS ↔ Python                   HTTPS (TLS 1.2+)            │
│  VPS ↔ Browser                  HTTPS (TLS 1.2+)            │
│  Python ↔ Arduino               USB Serial (local only)      │
│  API Authentication             Optional API Key             │
│  Physical Security              Locked cash acceptor        │
│  Audit Trail                    All transactions logged     │
└─────────────────────────────────────────────────────────────┘
```

## System States

```
State 1: IDLE
├─ No active orders
├─ Python polls VPS (5s interval)
├─ Arduino sends heartbeat (30s interval)
└─ Waiting for customer

State 2: ORDER_CREATED
├─ Order exists on VPS
├─ Payment session created
├─ Browser shows "Insert Cash"
└─ Waiting for cash

State 3: CASH_DETECTION
├─ Python detects active session
├─ Sets current_order
├─ Arduino ready to accept cash
└─ Waiting for physical cash

State 4: CASH_INSERTED
├─ Arduino detects bill/coin
├─ Sends "BILL:xxx" or "COIN:xxx"
├─ Python POSTs to VPS
├─ VPS updates session
├─ Browser shows updated amount
└─ Loop: accept more cash or complete

State 5: PAYMENT_COMPLETE
├─ Total amount reached
├─ VPS marks session complete
├─ Python clears current_order
├─ Receipt queued for printing
└─ Transition to PRINTING

State 6: PRINTING
├─ Print job in VPS queue
├─ Python polls, gets job
├─ Prints receipt
├─ Marks job complete
└─ Return to IDLE
```

---

**Last Updated:** October 17, 2025  
**System Version:** Unified Peripherals v1.0  
**Status:** ✅ Production Ready

