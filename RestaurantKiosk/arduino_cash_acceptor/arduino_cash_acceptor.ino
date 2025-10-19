/*
 * Arduino Cash Acceptor & Receipt Printer Interface for Restaurant Kiosk
 * Compatible with Raspberry Pi Polling Architecture
 * 
 * Reads pulses from bill and coin acceptors and sends data to Raspberry Pi via serial
 * Receives receipt print commands from Pi and prints to thermal printer
 * 
 * HARDWARE CONNECTIONS:
 * ====================
 * Bill Acceptor:
 *   - Pulse/Signal Pin -> Arduino Pin 2 (INT0)
 *   - VCC -> 12V (from external power supply)
 *   - GND -> Common Ground
 * 
 * Coin Acceptor:
 *   - Pulse/Signal Pin -> Arduino Pin 3 (INT1)
 *   - VCC -> 12V (from external power supply)
 *   - GND -> Common Ground
 * 
 * Thermal Printer (Serial):
 *   - TX (Arduino) -> RX (Printer) - Pin 10 (SoftwareSerial)
 *   - RX (Arduino) -> TX (Printer) - Pin 11 (SoftwareSerial)
 *   - VCC -> 5V or external power (check printer voltage!)
 *   - GND -> Common Ground
 * 
 * Raspberry Pi:
 *   - Arduino USB -> Raspberry Pi USB port
 *   - Communicates via serial at 9600 baud
 * 
 * PROTOCOL:
 * =========
 * Arduino → Pi:
 *   "BILL:100"    - ₱100 bill accepted
 *   "BILL:500"    - ₱500 bill accepted
 *   "COIN:5"      - ₱5 coin accepted
 *   "COIN:10"     - ₱10 coin accepted
 *   "READY"       - System ready (sent on startup)
 *   "PRINT:OK"    - Receipt printed successfully
 *   "PRINT:ERROR" - Receipt print failed
 * 
 * Pi → Arduino:
 *   "PING"              - Health check (Arduino responds with "PONG")
 *   "STATUS"            - Request status
 *   "PRINT:START"       - Begin receipt print job
 *   "PRINT:LINE:text"   - Print a line of text
 *   "PRINT:END"         - Finish receipt and cut paper
 * 
 * Version: 3.0 (With Receipt Printer Support)
 * Date: 2025-01-19
 */

// Include SoftwareSerial for thermal printer communication
#include <SoftwareSerial.h>

// ==================== PIN DEFINITIONS ====================
const int BILL_ACCEPTOR_PIN = 2;  // Interrupt pin for bill acceptor (INT0)
const int COIN_ACCEPTOR_PIN = 3;  // Interrupt pin for coin acceptor (INT1)
const int PRINTER_RX_PIN = 10;    // Arduino TX -> Printer RX
const int PRINTER_TX_PIN = 11;    // Arduino RX -> Printer TX
const int LED_PIN = 13;            // Built-in LED for status indication

// ==================== PRINTER SETUP ====================
SoftwareSerial printerSerial(PRINTER_TX_PIN, PRINTER_RX_PIN); // RX, TX
bool printerEnabled = true;        // Set to false to disable printer
bool printJobActive = false;

// ==================== DENOMINATION DEFINITIONS ====================
// Philippine Peso Bill Denominations
const int billValues[] = {10, 20, 50, 100, 200, 500, 1000};
const int NUM_BILL_DENOMINATIONS = 7;

// Philippine Peso Coin Denominations (in PHP)
const int coinValues[] = {1, 5, 10, 20};  // ₱1, ₱5, ₱10, ₱20
const int NUM_COIN_DENOMINATIONS = 4;

// ==================== PULSE COUNTING VARIABLES ====================
volatile unsigned long billPulseCount = 0;
volatile unsigned long coinPulseCount = 0;
volatile unsigned long lastBillPulseTime = 0;
volatile unsigned long lastCoinPulseTime = 0;

// ==================== TIMING CONSTANTS ====================
const unsigned long PULSE_TIMEOUT = 300;      // ms - time to wait after last pulse before processing
const unsigned long DEBOUNCE_DELAY = 30;      // ms - debounce time to prevent false triggers
const unsigned long HEARTBEAT_INTERVAL = 30000; // ms - send heartbeat every 30 seconds

// ==================== STATE VARIABLES ====================
bool billProcessing = false;
bool coinProcessing = false;
unsigned long lastHeartbeat = 0;
unsigned long totalBillsAccepted = 0;
unsigned long totalCoinsAccepted = 0;

// ==================== ACCEPTOR TYPE CONFIGURATION ====================
// Set this based on your hardware:
// 1 = Pulse-based (most common) - sends N pulses for denomination
// 2 = Single pulse per insertion - denomination detected by pulse width
// 3 = Serial protocol - acceptor sends denomination via serial (requires different code)
const int ACCEPTOR_TYPE = 1;

// For pulse-based acceptors, define pulse mapping
// Example: 1 pulse = ₱20, 2 pulses = ₱50, 3 pulses = ₱100, etc.
// Adjust these based on your specific acceptor model
const bool USE_PULSE_MAPPING = true;

// ==================== SETUP ====================
void setup() {
  // Initialize serial communication (9600 baud matches Python script)
  Serial.begin(9600);
  while (!Serial) {
    ; // Wait for serial port to connect (needed for some boards)
  }
  
  // Initialize printer serial communication
  if (printerEnabled) {
    printerSerial.begin(9600);  // Most thermal printers use 9600 baud
    delay(100);
    initializePrinter();
  }
  
  // Configure pins
  pinMode(BILL_ACCEPTOR_PIN, INPUT_PULLUP);  // Use internal pull-up resistor
  pinMode(COIN_ACCEPTOR_PIN, INPUT_PULLUP);  // Use internal pull-up resistor
  pinMode(LED_PIN, OUTPUT);
  
  // Attach interrupts (FALLING edge - trigger when signal goes LOW)
  attachInterrupt(digitalPinToInterrupt(BILL_ACCEPTOR_PIN), billPulseISR, FALLING);
  attachInterrupt(digitalPinToInterrupt(COIN_ACCEPTOR_PIN), coinPulseISR, FALLING);
  
  // Startup indication
  digitalWrite(LED_PIN, HIGH);
  delay(500);
  digitalWrite(LED_PIN, LOW);
  
  // Send ready signal to Raspberry Pi
  Serial.println("READY");
  Serial.println("# Arduino Cash Acceptor & Printer v3.0");
  Serial.println("# Polling Architecture + Receipt Printing");
  Serial.println("# Raspberry Pi handles order management");
  
  lastHeartbeat = millis();
}

// ==================== MAIN LOOP ====================
void loop() {
  unsigned long currentTime = millis();
  
  // Check for commands from Raspberry Pi (optional - for diagnostics)
  if (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    
    // Only process non-empty commands
    if (command.length() > 0) {
      handleCommand(command);
    }
  }
  
  // Process bill pulses if timeout has elapsed
  if (billProcessing && (currentTime - lastBillPulseTime > PULSE_TIMEOUT)) {
    processBillPulses();
  }
  
  // Process coin pulses if timeout has elapsed
  if (coinProcessing && (currentTime - lastCoinPulseTime > PULSE_TIMEOUT)) {
    processCoinPulses();
  }
  
  // Send periodic heartbeat (helps Pi detect if Arduino is alive)
  if (currentTime - lastHeartbeat > HEARTBEAT_INTERVAL) {
    sendHeartbeat();
    lastHeartbeat = currentTime;
  }
  
  delay(10);  // Small delay to prevent CPU spinning
}

// Interrupt Service Routine for bill acceptor
void billPulseISR() {
  unsigned long currentTime = millis();
  
  // Debounce
  if (currentTime - lastBillPulseTime > DEBOUNCE_DELAY) {
    billPulseCount++;
    lastBillPulseTime = currentTime;
    billProcessing = true;
    
    // Visual feedback
    digitalWrite(LED_PIN, HIGH);
  }
}

// Interrupt Service Routine for coin acceptor
void coinPulseISR() {
  unsigned long currentTime = millis();
  
  // Debounce
  if (currentTime - lastCoinPulseTime > DEBOUNCE_DELAY) {
    coinPulseCount++;
    lastCoinPulseTime = currentTime;
    coinProcessing = true;
    
    // Visual feedback
    digitalWrite(LED_PIN, HIGH);
  }
}

// ==================== COMMAND HANDLER ====================
void handleCommand(String command) {
  if (command == "PING") {
    // Health check from Raspberry Pi
    Serial.println("PONG");
  }
  else if (command == "STATUS") {
    // Send current status
    Serial.print("# Status: Bills=");
    Serial.print(totalBillsAccepted);
    Serial.print(" Coins=");
    Serial.print(totalCoinsAccepted);
    Serial.print(" Printer=");
    Serial.println(printerEnabled ? "OK" : "DISABLED");
  }
  else if (command == "RESET") {
    // Reset counters
    totalBillsAccepted = 0;
    totalCoinsAccepted = 0;
    Serial.println("# Counters reset");
  }
  else if (command.startsWith("TEST:BILL:")) {
    // Test mode: simulate bill insertion
    int amount = command.substring(10).toInt();
    Serial.print("BILL:");
    Serial.println(amount);
    quickFlash(2);
  }
  else if (command.startsWith("TEST:COIN:")) {
    // Test mode: simulate coin insertion
    int amount = command.substring(10).toInt();
    Serial.print("COIN:");
    Serial.println(amount);
    quickFlash(1);
  }
  else if (command == "PRINT:START") {
    // Start print job
    handlePrintStart();
  }
  else if (command.startsWith("PRINT:LINE:")) {
    // Print a line of text
    String text = command.substring(11);
    handlePrintLine(text);
  }
  else if (command == "PRINT:END") {
    // End print job and cut paper
    handlePrintEnd();
  }
  else if (command == "PRINT:TEST") {
    // Test printer
    testPrinter();
  }
  else {
    // Unknown command
    Serial.print("# Unknown command: ");
    Serial.println(command);
  }
}

// ==================== BILL PROCESSING ====================
void processBillPulses() {
  if (billPulseCount > 0) {
    int billValue = 0;
    
    if (USE_PULSE_MAPPING) {
      // Custom pulse mapping logic for irregular pulse counts
      if (billPulseCount == 1) billValue = 10;
      else if (billPulseCount == 2) billValue = 20;
      else if (billPulseCount == 5) billValue = 50;
      else if (billPulseCount == 10) billValue = 100;
      else if (billPulseCount == 20) billValue = 200;
      else if (billPulseCount == 50) billValue = 500;  // special case
      else if (billPulseCount == 100) billValue = 1000;
      else {
        billValue = 1000; // default to largest
        Serial.print("# Warning: Unexpected bill pulse count: ");
        Serial.println(billPulseCount);
      }
    } else {
      // Alternative: All bills trigger same pulse, use default value
      // This is common for simple acceptors that don't differentiate
      billValue = 100;  // Default to ₱100
    }
    
    // Send to Raspberry Pi (Pi handles order association)
    Serial.print("BILL:");
    Serial.println(billValue);
    
    // Update statistics
    totalBillsAccepted++;
    
    // Visual feedback
    quickFlash(2);
  }
  
  // Reset for next bill
  billPulseCount = 0;
  billProcessing = false;
  digitalWrite(LED_PIN, LOW);
}

// ==================== COIN PROCESSING ====================
void processCoinPulses() {
  if (coinPulseCount > 0) {
    int coinValue = 0;
    
    if (USE_PULSE_MAPPING) {
      // Map pulse count to coin denomination
      // Common mapping: 1 pulse = ₱1, 5 = ₱5, 10 = ₱10, 20 = ₱20
      // Note: Some acceptors send 1 pulse per peso value
      if (coinPulseCount <= 20) {
        // Direct pulse-to-peso mapping for common acceptors
        if (coinPulseCount == 1) coinValue = 1;
        else if (coinPulseCount == 5) coinValue = 5;
        else if (coinPulseCount == 10) coinValue = 10;
        else if (coinPulseCount == 20) coinValue = 20;
        else {
          // Round to nearest valid denomination
          if (coinPulseCount < 3) coinValue = 1;
          else if (coinPulseCount < 8) coinValue = 5;
          else if (coinPulseCount < 15) coinValue = 10;
          else coinValue = 20;
        }
      } else {
        // Too many pulses, default to largest
        coinValue = coinValues[NUM_COIN_DENOMINATIONS - 1];
        Serial.print("# Warning: Unexpected coin pulse count: ");
        Serial.println(coinPulseCount);
      }
    } else {
      // Alternative: All coins trigger same pulse, use default value
      coinValue = 5;  // Default to ₱5
    }
    
    // Send to Raspberry Pi (Pi handles order association)
    Serial.print("COIN:");
    Serial.println(coinValue);
    
    // Update statistics
    totalCoinsAccepted++;
    
    // Visual feedback
    quickFlash(1);
  }
  
  // Reset for next coin
  coinPulseCount = 0;
  coinProcessing = false;
  digitalWrite(LED_PIN, LOW);
}

// ==================== LED FEEDBACK ====================
void quickFlash(int times) {
  for (int i = 0; i < times; i++) {
    digitalWrite(LED_PIN, HIGH);
    delay(50);
    digitalWrite(LED_PIN, LOW);
    delay(50);
  }
}

void sendHeartbeat() {
  // Send heartbeat to let Pi know Arduino is alive
  Serial.print("# Heartbeat - Bills:");
  Serial.print(totalBillsAccepted);
  Serial.print(" Coins:");
  Serial.println(totalCoinsAccepted);
}

// ==================== PRINTER FUNCTIONS ====================

void initializePrinter() {
  // Initialize thermal printer with ESC/POS commands
  delay(500);  // Wait for printer to power up
  
  // ESC @ - Initialize printer
  printerSerial.write(27);  // ESC
  printerSerial.write(64);  // @
  delay(100);
  
  Serial.println("# Printer initialized");
}

void handlePrintStart() {
  // Start a new print job
  if (!printerEnabled) {
    Serial.println("PRINT:ERROR:DISABLED");
    return;
  }
  
  printJobActive = true;
  
  // Initialize printer for this job
  printerSerial.write(27);  // ESC
  printerSerial.write(64);  // @ - Initialize
  delay(200);  // Longer delay for printer to fully initialize
  
  Serial.println("# Print job started");
}

void handlePrintLine(String text) {
  // Print a line of text with slow, controlled output
  if (!printerEnabled) {
    Serial.println("# Print line ignored - printer disabled");
    return;
  }
  
  if (!printJobActive) {
    Serial.println("# Print line ignored - no active job");
    return;
  }
  
  // Debug: show what we received
  Serial.print("# Printing: ");
  Serial.println(text);
  
  // Send text to printer one character at a time (pure ASCII only)
  int charsSent = 0;
  for (int i = 0; i < text.length(); i++) {
    char c = text.charAt(i);
    
    // Only send printable ASCII characters (32-126)
    if (c >= 32 && c <= 126) {
      printerSerial.write(c);
      charsSent++;
      delayMicroseconds(500);  // Small delay between characters
    } else if (c == '\n') {
      printerSerial.write('\n');
      charsSent++;
      delay(10);  // Longer delay after newline to let printer advance paper
    } else if (c == ' ') {
      printerSerial.write(' ');
      charsSent++;
      delayMicroseconds(500);
    }
  }
  
  // Always add newline at end
  printerSerial.write('\n');
  delay(10);
  
  // Debug: confirm characters sent
  Serial.print("# Sent ");
  Serial.print(charsSent);
  Serial.println(" chars to printer");
  
  delay(20);  // Additional delay after each line to let printer process
}

void handlePrintEnd() {
  // End print job and cut paper
  if (!printerEnabled || !printJobActive) {
    Serial.println("PRINT:ERROR:NO_JOB");
    return;
  }
  
  // Feed more paper before cut to ensure footer is visible
  for (int i = 0; i < 6; i++) {
    printerSerial.write('\n');
    delay(100);
  }
  
  delay(300);  // Extra delay before cut
  
  // Cut paper - ESC i (full cut)
  printerSerial.write(27);  // ESC
  printerSerial.write(105); // i
  delay(800);  // Longer wait for cut to complete
  
  printJobActive = false;
  Serial.println("PRINT:OK");
  
  quickFlash(3);  // Visual feedback
}

void testPrinter() {
  // Test printer with a simple receipt
  if (!printerEnabled) {
    Serial.println("PRINT:ERROR:DISABLED");
    return;
  }
  
  Serial.println("# Testing printer...");
  
  // Initialize
  printerSerial.write(27);  // ESC
  printerSerial.write(64);  // @ - Initialize
  delay(100);
  
  // Print test text
  printerSerial.println("================================");
  printerSerial.println("  PRINTER TEST");
  printerSerial.println("================================");
  printerSerial.println();
  printerSerial.println("Arduino Cash & Print System");
  printerSerial.println("Version 3.0");
  printerSerial.println();
  printerSerial.println("If you can read this,");
  printerSerial.println("the printer is working!");
  printerSerial.println();
  printerSerial.println("================================");
  printerSerial.println();
  
  // Feed and cut
  printerSerial.write('\n');
  printerSerial.write('\n');
  delay(200);
  
  printerSerial.write(27);  // ESC
  printerSerial.write(105); // i - Cut
  delay(500);
  
  Serial.println("PRINT:OK:TEST");
  quickFlash(3);
}

// ==================== TESTING & DOCUMENTATION ====================
/*
 * TESTING WITHOUT HARDWARE:
 * =========================
 * Open Arduino Serial Monitor (9600 baud) and send these commands:
 * 
 * TEST:BILL:100    - Simulate ₱100 bill insertion
 * TEST:BILL:500    - Simulate ₱500 bill insertion
 * TEST:COIN:5      - Simulate ₱5 coin insertion
 * TEST:COIN:10     - Simulate ₱10 coin insertion
 * PING             - Check if Arduino is responding
 * STATUS           - Get current statistics
 * RESET            - Reset counters
 * 
 * TESTING WITH RASPBERRY PI:
 * ==========================
 * 1. Connect Arduino to Raspberry Pi via USB
 * 2. Run: python3 arduino_cash_reader.py
 * 3. Arduino will send "READY" on startup
 * 4. Insert cash - Arduino sends "BILL:xxx" or "COIN:xxx"
 * 5. Pi automatically associates with active order from VPS
 * 
 * HARDWARE SETUP:
 * ===============
 * Most bill/coin acceptors have 3 wires:
 * - RED (or +12V): Connect to 12V power supply
 * - BLACK (or GND): Connect to ground (shared with Arduino GND)
 * - WHITE/YELLOW (or PULSE/COIN): Connect to Arduino interrupt pin
 * 
 * Common Acceptor Models:
 * - JY-15A/B Bill Acceptor
 * - CH-926 Coin Acceptor
 * - Comparable units from Suzo-Happ, ICT, MEI
 * 
 * PULSE PROTOCOL TYPES:
 * =====================
 * 
 * Type 1: Pulse Count (Most Common)
 * ----------------------------------
 * - Each denomination sends different number of pulses
 * - Example: ₱20=1 pulse, ₱50=2, ₱100=3, ₱200=4, ₱500=5, ₱1000=6
 * - Current code uses this method (USE_PULSE_MAPPING = true)
 * - Adjust billValues[] array to match your acceptor
 * 
 * Type 2: Pulse Width Modulation
 * -------------------------------
 * - Single pulse per insertion
 * - Denomination detected by pulse duration
 * - Requires measuring pulse width (modify ISR code)
 * 
 * Type 3: Serial Protocol
 * -----------------------
 * - Acceptor sends denomination via UART/RS232
 * - Easier but requires different Arduino code
 * - Look for "protocol" or "serial" in acceptor datasheet
 * 
 * Type 4: Parallel Lines
 * ----------------------
 * - Each denomination has separate signal pin
 * - Requires more Arduino pins
 * - Less common for budget acceptors
 * 
 * CALIBRATION STEPS:
 * ==================
 * 1. Upload this code to Arduino
 * 2. Open Serial Monitor (9600 baud)
 * 3. Insert ₱20 bill - count pulses shown in serial output
 * 4. Insert ₱50 bill - count pulses
 * 5. Continue for all denominations
 * 6. Update pulse mapping in code based on observed counts
 * 7. Re-upload and test
 * 
 * TROUBLESHOOTING:
 * ================
 * - No pulses detected: Check wiring, check acceptor power (12V)
 * - Wrong amounts: Adjust pulse mapping (billValues[] and coinValues[])
 * - Multiple triggers: Increase DEBOUNCE_DELAY
 * - Missed insertions: Decrease DEBOUNCE_DELAY or check connections
 * - Arduino not responding: Check USB cable, check serial baud rate (9600)
 * 
 * SECURITY NOTES:
 * ===============
 * - Bill/coin acceptors can be fooled by fake currency
 * - Use acceptors with UV detection, magnetic scanning, or size verification
 * - Consider acceptors with "reject" mechanism for invalid bills
 * - Log all transactions with timestamps for audit trail
 * 
 * ARCHITECTURE SUMMARY:
 * =====================
 * Arduino: Detects cash → Sends "BILL:xxx" or "COIN:xxx"
 *     ↓
 * Raspberry Pi: Receives serial data → POSTs to VPS API
 *     ↓
 * VPS: Updates payment session (in-memory)
 *     ↓
 * Browser: Polls VPS → Updates UI
 *     ↓
 * Customer: Sees real-time cash insertion feedback
 * 
 * No ORDER command needed! Pi handles order discovery by polling VPS.
 * 
 * Version: 2.0
 * Compatible with: Polling Architecture (Raspberry Pi + VPS)
 * Last Updated: 2025-01-15
 */



