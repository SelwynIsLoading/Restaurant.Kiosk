#!/usr/bin/env python3
"""
Restaurant Kiosk Peripherals Manager
Handles both Arduino cash reader and receipt printer on Raspberry Pi
Polls VPS for active payment sessions and print jobs

This unified script simplifies deployment by managing all peripherals in one process.
"""

import serial
import requests
import time
import json
import logging
from logging.handlers import RotatingFileHandler
import os
import threading
from typing import Optional, Dict, Any
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
# ESC/POS printer library no longer needed - Arduino handles printing directly
# from escpos.printer import Usb, Network, Serial as ESCPOSSerial, File
# from escpos.exceptions import USBNotFoundError, Error as ESCPOSError

# ============================================================================
# CONFIGURATION
# ============================================================================

def load_config():
    """Load configuration from cash_reader_config.json"""
    config_file = Path(__file__).parent / "cash_reader_config.json"
    
    # Default configuration
    default_config = {
        "vps_api_url": "http://localhost:5000",
        "arduino_port": "/dev/ttyUSB0",
        "arduino_baud_rate": 9600,
        "printer_type": "serial",  # serial, usb, network, file
        "printer_serial_port": "/dev/ttyUSB0",
        "printer_serial_baudrate": 9600,
        "printer_usb_vendor_id": "0x04b8",
        "printer_usb_product_id": "0x0e15",
        "reconnect_delay_seconds": 5,
        "connection_timeout_seconds": 10,
        "retry_attempts": 3,
        "cash_poll_interval": 5,
        "printer_poll_interval": 2,
        "api_key": None,
        "environment": "development",
        "enable_cash_reader": True,
        "enable_printer": True
    }
    
    try:
        if config_file.exists():
            with open(config_file, 'r') as f:
                loaded_config = json.load(f)
                default_config.update(loaded_config)
                logger_temp = logging.getLogger(__name__)
                logger_temp.info(f"Loaded configuration from {config_file}")
        else:
            logger_temp = logging.getLogger(__name__)
            logger_temp.warning(f"Config file not found: {config_file}. Using defaults.")
    except Exception as e:
        logger_temp = logging.getLogger(__name__)
        logger_temp.error(f"Error loading config: {e}. Using defaults.")
    
    return default_config

# Load configuration
config = load_config()

# Setup logging with rotation
def setup_logging():
    """Configure logging with rotation and UTF-8 encoding"""
    log_formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
    
    # File handler with rotation (max 10MB, keep 5 backup files)
    # Use UTF-8 encoding to support peso sign (₱) and other Unicode characters
    file_handler = RotatingFileHandler(
        'kiosk_peripherals.log',
        maxBytes=10*1024*1024,  # 10MB
        backupCount=5,
        encoding='utf-8'  # Add UTF-8 encoding
    )
    file_handler.setFormatter(log_formatter)
    
    # Console handler - also set UTF-8 encoding
    console_handler = logging.StreamHandler()
    console_handler.setFormatter(log_formatter)
    # Set UTF-8 encoding for console (Python 3.7+)
    import sys
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stderr.reconfigure(encoding='utf-8')
    
    # Configure root logger
    root_logger = logging.getLogger()
    root_logger.setLevel(logging.INFO)
    root_logger.addHandler(file_handler)
    root_logger.addHandler(console_handler)
    
    return logging.getLogger(__name__)

logger = setup_logging()

# ============================================================================
# CASH READER MODULE
# ============================================================================

@dataclass
class CashUpdate:
    """Represents a cash amount update"""
    order_number: str
    amount_added: float
    timestamp: datetime = None
    
    def __post_init__(self):
        if self.timestamp is None:
            self.timestamp = datetime.now()


class ArduinoCashReader:
    """Reads cash acceptor data from Arduino and communicates with kiosk API"""
    
    def __init__(self, port: str, baud_rate: int, api_url: str, api_key: Optional[str] = None):
        self.port = port
        self.baud_rate = baud_rate
        self.api_url = api_url
        self.api_key = api_key
        self.serial_connection: Optional[serial.Serial] = None
        self.current_order: Optional[str] = None
        self.active_sessions: dict = {}
        self.last_poll_time: float = 0
        self.poll_interval: int = config["cash_poll_interval"]
        self.session = requests.Session()
        self.running = False
        
    def poll_active_sessions(self) -> bool:
        """Poll VPS for active payment sessions"""
        try:
            url = f"{self.api_url}/api/cash-payment/active-sessions"
            headers = {}
            if self.api_key:
                headers['X-API-Key'] = self.api_key
            
            response = self.session.get(url, headers=headers, timeout=config["connection_timeout_seconds"])
            
            if response.status_code == 200:
                data = response.json()
                if data.get('success'):
                    new_sessions = {s['orderNumber']: s for s in data.get('sessions', [])}
                    
                    # Detect new sessions
                    for order_num, session_data in new_sessions.items():
                        if order_num not in self.active_sessions:
                            logger.info(f"[CASH] New payment session detected: {order_num} - Amount: ₱{session_data['totalRequired']}")
                            print(f"\n{'='*60}")
                            print(f"NEW ORDER WAITING FOR CASH PAYMENT")
                            print(f"Order Number: {order_num}")
                            print(f"Total Required: ₱{session_data['totalRequired']}")
                            print(f"Please insert cash into the acceptor")
                            print(f"{'='*60}\n")
                    
                    # Detect completed/cancelled sessions
                    for order_num in list(self.active_sessions.keys()):
                        if order_num not in new_sessions:
                            logger.info(f"[CASH] Payment session completed/cancelled: {order_num}")
                            if self.current_order == order_num:
                                self.current_order = None
                    
                    self.active_sessions = new_sessions
                    
                    # Auto-select the first active session if we don't have one
                    if not self.current_order and self.active_sessions:
                        self.current_order = list(self.active_sessions.keys())[0]
                        logger.info(f"[CASH] Auto-selected order for payment: {self.current_order}")
                    
                    return True
                else:
                    logger.warning(f"[CASH] API returned success=false: {data}")
                    return False
            else:
                logger.error(f"[CASH] Failed to poll active sessions: {response.status_code}")
                return False
                
        except requests.exceptions.ConnectionError as e:
            logger.debug(f"[CASH] Connection error polling active sessions: {e}")
            return False
        except Exception as e:
            logger.error(f"[CASH] Error polling active sessions: {e}")
            return False
    
    def connect_arduino(self) -> bool:
        """Establish connection to Arduino"""
        try:
            logger.info(f"[CASH] Connecting to Arduino on {self.port} at {self.baud_rate} baud...")
            self.serial_connection = serial.Serial(
                port=self.port,
                baudrate=self.baud_rate,
                timeout=1
            )
            time.sleep(2)  # Wait for Arduino to reset
            logger.info("[CASH] Successfully connected to Arduino")
            return True
        except serial.SerialException as e:
            logger.error(f"[CASH] Failed to connect to Arduino: {e}")
            return False
        except Exception as e:
            logger.error(f"[CASH] Unexpected error connecting to Arduino: {e}")
            return False
    
    def disconnect_arduino(self):
        """Close Arduino connection"""
        if self.serial_connection and self.serial_connection.is_open:
            self.serial_connection.close()
            logger.info("[CASH] Disconnected from Arduino")
    
    def send_arduino_command(self, command: str) -> bool:
        """Send command to Arduino (for diagnostics)
        
        Supported commands:
        - PING - Health check (Arduino responds with PONG)
        - STATUS - Request statistics
        - RESET - Reset counters
        - TEST:BILL:100 - Simulate bill insertion (testing without hardware)
        - TEST:COIN:5 - Simulate coin insertion (testing without hardware)
        """
        try:
            if self.serial_connection and self.serial_connection.is_open:
                self.serial_connection.write(f"{command}\n".encode('utf-8'))
                logger.debug(f"[CASH] Sent command to Arduino: {command}")
                return True
            else:
                logger.warning("[CASH] Cannot send command - Arduino not connected")
                return False
        except Exception as e:
            logger.error(f"[CASH] Error sending command to Arduino: {e}")
            return False
    
    def send_cash_update(self, cash_update: CashUpdate) -> bool:
        """Send cash amount update to kiosk API"""
        max_retries = config["retry_attempts"]
        
        for attempt in range(max_retries):
            try:
                url = f"{self.api_url}/api/cash-payment/update"
                payload = {
                    "orderNumber": cash_update.order_number,
                    "amountAdded": cash_update.amount_added
                }
                
                headers = {'Content-Type': 'application/json'}
                if self.api_key:
                    headers['X-API-Key'] = self.api_key
                
                logger.info(f"[CASH] Sending cash update: {payload} (Attempt {attempt + 1}/{max_retries})")
                response = self.session.post(url, json=payload, headers=headers, timeout=config["connection_timeout_seconds"])
            
                if response.status_code == 200:
                    data = response.json()
                    logger.info(f"[CASH] Cash update successful: {data}")
                    
                    # Check if payment is complete
                    if data.get('isComplete'):
                        logger.info(f"[CASH] Payment completed for order {cash_update.order_number}")
                        self.current_order = None
                        
                    return True
                else:
                    logger.error(f"[CASH] Failed to send cash update: {response.status_code} - {response.text}")
                    if attempt < max_retries - 1:
                        time.sleep(2)
                        continue
                    return False
                    
            except Exception as e:
                logger.error(f"[CASH] Error sending cash update (attempt {attempt + 1}/{max_retries}): {e}")
                if attempt < max_retries - 1:
                    time.sleep(2)
                    continue
                return False
        
        return False
    
    def parse_arduino_data(self, data: str) -> Optional[CashUpdate]:
        """Parse data received from Arduino
        
        Expected format from Arduino:
        - "BILL:100" - ₱100 bill inserted
        - "COIN:5" - ₱5 coin inserted
        - "READY" - Arduino startup message
        - "PONG" - Response to PING command
        - "# ..." - Comment/debug messages (ignored)
        
        Note: Order number is auto-selected from active sessions (no ORDER command needed)
        """
        try:
            data = data.strip()
            
            if not data:
                return None
            
            # Ignore comment lines (Arduino debug/heartbeat messages)
            if data.startswith("#"):
                logger.debug(f"[CASH] Arduino info: {data}")
                return None
            
            # Handle system messages
            if data in ["READY", "PONG"]:
                logger.info(f"[CASH] Arduino status: {data}")
                return None
            
            # Log all received data at debug level
            logger.debug(f"[CASH] Received from Arduino: {data}")
            
            # Handle cash insertion - only process if we have an active order
            if data.startswith("BILL:") or data.startswith("COIN:"):
                if not self.current_order:
                    logger.warning("[CASH] Received cash data but no active order session. Waiting for order from VPS...")
                    return None
                
                amount = 0.0
                
                if data.startswith("BILL:"):
                    amount = float(data.split(":", 1)[1])
                    logger.info(f"[CASH] Bill inserted: ₱{amount} for order {self.current_order}")
                    print(f"✓ Bill: ₱{amount} (Order: {self.current_order})")
                elif data.startswith("COIN:"):
                    amount = float(data.split(":", 1)[1])
                    logger.info(f"[CASH] Coin inserted: ₱{amount} for order {self.current_order}")
                    print(f"✓ Coin: ₱{amount} (Order: {self.current_order})")
                
                if amount > 0:
                    return CashUpdate(
                        order_number=self.current_order,
                        amount_added=amount
                    )
            else:
                # Unknown command - log but don't error
                logger.debug(f"[CASH] Unhandled Arduino message: {data}")
                return None
            
            return None
            
        except ValueError as e:
            logger.error(f"[CASH] Error parsing amount from '{data}': {e}")
            return None
        except Exception as e:
            logger.error(f"[CASH] Error parsing Arduino data: {e}")
            return None
    
    def cancel_payment(self, order_number: str) -> bool:
        """Cancel payment for an order"""
        try:
            url = f"{self.api_url}/api/cash-payment/cancel/{order_number}"
            response = self.session.post(url, timeout=5)
            
            if response.status_code == 200:
                logger.info(f"[CASH] Payment cancelled successfully for order {order_number}")
                return True
            else:
                logger.error(f"[CASH] Failed to cancel payment: {response.status_code}")
                return False
                
        except Exception as e:
            logger.error(f"[CASH] Error cancelling payment: {e}")
            return False
    
    def run(self):
        """Main loop to read Arduino data and send updates"""
        logger.info("[CASH] Starting cash reader loop...")
        self.running = True
        
        while self.running:
            try:
                # Poll VPS for active payment sessions periodically
                current_time = time.time()
                if current_time - self.last_poll_time >= self.poll_interval:
                    self.poll_active_sessions()
                    self.last_poll_time = current_time
                    
                    # Display current status
                    if self.active_sessions and self.current_order:
                        session = self.active_sessions.get(self.current_order)
                        if session:
                            logger.debug(f"[CASH] Current order: {self.current_order} - "
                                      f"₱{session['amountInserted']}/₱{session['totalRequired']}")
                
                # Connect to Arduino if not connected
                if not self.serial_connection or not self.serial_connection.is_open:
                    if not self.connect_arduino():
                        time.sleep(config["reconnect_delay_seconds"])
                        continue
                
                # Read line from Arduino
                if self.serial_connection.in_waiting > 0:
                    line = self.serial_connection.readline().decode('utf-8', errors='ignore')
                    
                    # Parse and process the data
                    cash_update = self.parse_arduino_data(line)
                    
                    if cash_update:
                        self.send_cash_update(cash_update)
                
                time.sleep(0.1)
                
            except serial.SerialException as e:
                logger.error(f"[CASH] Serial communication error: {e}")
                self.disconnect_arduino()
                time.sleep(config["reconnect_delay_seconds"])
                
            except Exception as e:
                logger.error(f"[CASH] Unexpected error in read loop: {e}")
                time.sleep(1)
        
        self.disconnect_arduino()
        logger.info("[CASH] Cash reader stopped")


# ============================================================================
# RECEIPT PRINTER MODULE
# ============================================================================

class ReceiptPrinterClient:
    """Client that polls VPS for print jobs and sends to Arduino for printing"""
    
    def __init__(self, vps_url: str, arduino_serial_connection=None):
        self.vps_url = vps_url
        self.arduino_connection = arduino_serial_connection
        self.session = requests.Session()
        self.running = False
        self.use_arduino_printer = True  # Set to False to use direct printer connection
        
    
    def _safe_text(self, text: str) -> str:
        """Convert text to safe ASCII-only format"""
        try:
            # Convert to ASCII, replacing non-ASCII characters with '?'
            return text.encode('ascii', errors='replace').decode('ascii')
        except Exception as e:
            logger.warning(f"[PRINTER] Error converting text to ASCII: {e}")
            return text
    
    def check_for_print_jobs(self) -> Optional[Dict[str, Any]]:
        """Poll VPS for pending print jobs"""
        try:
            url = f"{self.vps_url}/api/receipt/queue/next"
            response = self.session.get(url, timeout=5)
            
            if response.status_code == 200:
                data = response.json()
                if data.get('hasPrintJob'):
                    return data
            elif response.status_code == 204:
                return None
            else:
                logger.warning(f"[PRINTER] Unexpected status code: {response.status_code}")
                
        except requests.exceptions.RequestException as e:
            logger.debug(f"[PRINTER] Error checking for print jobs: {e}")
        
        return None
    
    def print_receipt(self, receipt_data: Dict[str, Any]) -> bool:
        """Print a receipt via Arduino with slow, controlled transmission"""
        try:
            order_number = receipt_data.get('orderNumber', 'N/A')
            logger.info(f"[PRINTER] Printing receipt via Arduino for order: {order_number}")
            
            if not self.arduino_connection or not self.arduino_connection.is_open:
                logger.error("[PRINTER] Arduino not connected")
                return False
            
            # Clear any pending data in serial buffer
            self.arduino_connection.reset_input_buffer()
            self.arduino_connection.reset_output_buffer()
            
            # Start print job
            logger.info(f"[PRINTER] Starting print job for order {order_number}")
            self._send_arduino_command("PRINT:START")
            time.sleep(1.0)  # Give Arduino plenty of time to initialize printer
            
            # Send receipt content line by line with significant delay
            lines = self._generate_receipt_lines(receipt_data)
            total_lines = len(lines)
            
            logger.info(f"[PRINTER] Generated {total_lines} receipt lines total")
            
            for i, line in enumerate(lines):
                # Build the full command that will be sent
                full_command = f"PRINT:LINE:{line}"
                command_length = len(full_command)
                
                # Log every line with full command details
                print(f"[PRINTER] Sending line {i+1}/{total_lines}: ({command_length} bytes) '{full_command}'")
                logger.info(f"[PRINTER] >>> Line {i+1}/{total_lines} ({command_length} bytes): '{full_command}'")
                
                # Check if command is safe before sending
                if command_length > 63:
                    logger.error(f"[PRINTER] ⚠️ BUFFER OVERFLOW RISK! Command is {command_length} bytes (max 64)")
                
                self._send_arduino_command(f"PRINT:LINE:{line}")
                
                # Much slower transmission with longer delays
                # Arduino needs time to process and print each line
                line_length = len(line)
                if line_length > 30:
                    time.sleep(0.4)  # Long lines need much more time
                elif line_length > 0:
                    time.sleep(0.3)  # Normal lines - be very conservative
                else:
                    time.sleep(0.1)  # Empty lines
            
            # End print job (cut paper)
            logger.info(f"[PRINTER] All {total_lines} lines sent, cutting paper...")
            time.sleep(0.5)  # Extra delay before cut command
            self._send_arduino_command("PRINT:END")
            time.sleep(3)  # Wait even longer for paper feed and cut
            
            logger.info(f"[PRINTER] Receipt printed successfully for order: {order_number}")
            return True
            
        except Exception as e:
            logger.error(f"[PRINTER] Error printing receipt: {e}")
            return False
    
    def _send_arduino_command(self, command: str):
        """Send command to Arduino with strict length check"""
        if self.arduino_connection and self.arduino_connection.is_open:
            # Arduino Serial buffer is 64 bytes by default
            # Be very conservative - max 50 chars to be safe
            if len(command) > 50:
                logger.error(f"[PRINTER] Command TOO LONG ({len(command)} chars)! Line: '{command}'")
                logger.error(f"[PRINTER] Truncating to 50 chars...")
                command = command[:50]
            
            # Log what we're actually sending
            logger.debug(f"[PRINTER] Sending ({len(command)} chars): {command}")
            
            self.arduino_connection.write(f"{command}\n".encode('utf-8'))
            self.arduino_connection.flush()
            time.sleep(0.02)  # Small delay after flushing
    
    def _generate_receipt_lines(self, data: Dict[str, Any]) -> list:
        """Generate receipt lines - NO SEPARATOR LINES to avoid printer issues"""
        lines = []
        
        # Header - Centered
        name = self._safe_text(data.get('restaurantName', 'Restaurant'))
        address = self._safe_text(data.get('restaurantAddress', ''))
        phone = self._safe_text(data.get('restaurantPhone', ''))
        
        lines.append(self._center_text(name, 28))
        if address:
            lines.append(self._center_text(address, 28))
        if phone:
            lines.append(self._center_text(phone, 28))
        lines.append('')
        lines.append('')
        
        # Order details
        order_num = self._safe_text(data.get('orderNumber', 'N/A'))[:20]
        order_date = self._safe_text(data.get('orderDate', ''))[:25]
        
        lines.append('Order: ' + order_num)
        lines.append('Date: ' + order_date)
        
        if data.get('customerName'):
            customer = self._safe_text(data['customerName'])[:20]
            lines.append('Cust: ' + customer)
        
        lines.append('')
        lines.append('')
        
        # Items header
        lines.append('ITEM             QTY  PRICE')
        lines.append('')
        
        # Items - Build each item line
        items_list = data.get('items', [])
        logger.info(f"[PRINTER] Processing {len(items_list)} items for receipt")
        
        for idx, item in enumerate(items_list):
            item_name = self._safe_text(item.get('productName', 'Unknown'))
            qty = item.get('quantity', 0)
            price = item.get('lineTotal', 0.0)
            
            # Truncate and pad name to 16 chars
            if len(item_name) > 16:
                item_name = item_name[:16]
            item_name = item_name.ljust(16)
            
            # Format numbers
            qty_str = str(qty).rjust(3)
            price_str = f'{price:.2f}'.rjust(8)
            
            # Build line (27 chars total)
            item_line = item_name + ' ' + qty_str + price_str
            
            logger.info(f"[PRINTER] Item {idx+1} line: '{item_line}'")
            lines.append(item_line)
        
        lines.append('')
        lines.append('')
        
        # Totals
        subtotal = data.get('subTotal', 0)
        tax = data.get('tax', 0)
        total = data.get('totalAmount', 0)
        
        lines.append('Subtotal:' + f'{subtotal:.2f}'.rjust(18))
        
        if tax > 0:
            lines.append('VAT:' + f'{tax:.2f}'.rjust(23))
        
        lines.append('')
        lines.append('TOTAL:' + f'{total:.2f}'.rjust(21))
        lines.append('')
        lines.append('')
        
        # Payment info
        payment_method = self._safe_text(data.get('paymentMethod', 'N/A'))[:15]
        lines.append('Pay: ' + payment_method)
        lines.append('')
        
        if data.get('amountPaid'):
            amount_paid = data['amountPaid']
            lines.append('Paid:' + f'{amount_paid:.2f}'.rjust(22))
            
            change = data.get('change', 0)
            if change > 0:
                lines.append('Change:' + f'{change:.2f}'.rjust(20))
        
        lines.append('')
        lines.append('')
        
        # Footer - Centered
        lines.append(self._center_text('THANK YOU!', 28))
        lines.append(self._center_text('Please come again', 28))
        lines.append('')
        lines.append(self._center_text('Have a great day!', 28))
        lines.append('')
        lines.append('')
        lines.append('')
        
        return lines
    
    def _center_text(self, text: str, width: int) -> str:
        """Manually center text to avoid encoding issues"""
        text = text.strip()
        if len(text) >= width:
            return text[:width]
        
        total_padding = width - len(text)
        left_padding = total_padding // 2
        right_padding = total_padding - left_padding
        
        result = (' ' * left_padding) + text + (' ' * right_padding)
        
        # Safety check - ensure we don't exceed width
        if len(result) > width:
            result = result[:width]
        
        return result
    
    
    def mark_job_completed(self, job_id: str) -> bool:
        """Notify VPS that print job is completed"""
        try:
            url = f"{self.vps_url}/api/receipt/queue/complete/{job_id}"
            response = self.session.post(url, timeout=5)
            return response.status_code == 200
        except Exception as e:
            logger.error(f"[PRINTER] Error marking job completed: {e}")
            return False
    
    def mark_job_failed(self, job_id: str, error: str) -> bool:
        """Notify VPS that print job failed"""
        try:
            url = f"{self.vps_url}/api/receipt/queue/failed/{job_id}"
            response = self.session.post(url, json={"error": error}, timeout=5)
            return response.status_code == 200
        except Exception as e:
            logger.error(f"[PRINTER] Error marking job failed: {e}")
            return False
    
    def run(self):
        """Main loop - poll for print jobs"""
        logger.info("[PRINTER] Starting receipt printer loop (Arduino mode)...")
        self.running = True
        
        poll_interval = config["printer_poll_interval"]
        
        while self.running:
            try:
                # Wait for Arduino connection to be available
                if not self.arduino_connection or not self.arduino_connection.is_open:
                    time.sleep(1)
                    continue
                
                # Check for print jobs
                job_data = self.check_for_print_jobs()
                
                if job_data:
                    job_id = job_data.get('jobId')
                    receipt_data = job_data.get('receipt')
                    
                    logger.info(f"[PRINTER] Received print job: {job_id}")
                    
                    # Print receipt via Arduino
                    success = self.print_receipt(receipt_data)
                    
                    # Notify VPS
                    if success:
                        self.mark_job_completed(job_id)
                    else:
                        self.mark_job_failed(job_id, "Printing failed")
                
                time.sleep(poll_interval)
                
            except Exception as e:
                logger.error(f"[PRINTER] Error in main loop: {e}")
                time.sleep(poll_interval)
        
        logger.info("[PRINTER] Receipt printer stopped")


# ============================================================================
# MAIN PROGRAM
# ============================================================================

def main():
    """Main entry point - runs both cash reader and printer sharing Arduino connection"""
    logger.info("=" * 80)
    logger.info("Restaurant Kiosk - Peripherals Manager (Arduino Unified)")
    logger.info("=" * 80)
    logger.info(f"Environment: {config.get('environment', 'development')}")
    logger.info(f"VPS API URL: {config['vps_api_url']}")
    logger.info(f"API Key Configured: {'Yes' if config.get('api_key') else 'No'}")
    logger.info("")
    logger.info("Enabled Modules:")
    logger.info(f"  - Cash Reader: {'✓' if config['enable_cash_reader'] else '✗'}")
    logger.info(f"  - Receipt Printer (via Arduino): {'✓' if config['enable_printer'] else '✗'}")
    logger.info("")
    
    threads = []
    cash_reader = None
    
    try:
        # Start cash reader thread (it creates the Arduino connection)
        if config["enable_cash_reader"]:
            logger.info("[CASH] Configuration:")
            logger.info(f"  - Arduino Port: {config['arduino_port']}")
            logger.info(f"  - Baud Rate: {config['arduino_baud_rate']}")
            logger.info(f"  - Poll Interval: {config['cash_poll_interval']}s")
            logger.info("")
            
            cash_reader = ArduinoCashReader(
                port=config["arduino_port"],
                baud_rate=config["arduino_baud_rate"],
                api_url=config["vps_api_url"],
                api_key=config.get("api_key")
            )
            
            cash_thread = threading.Thread(target=cash_reader.run, name="CashReader", daemon=False)
            cash_thread.start()
            threads.append((cash_thread, cash_reader))
            logger.info("[CASH] Cash reader thread started")
            
            # Give Arduino time to connect
            time.sleep(3)
        
        # Start printer thread (shares Arduino connection)
        if config["enable_printer"] and cash_reader:
            logger.info("[PRINTER] Configuration:")
            logger.info(f"  - Mode: Arduino Serial (shared connection)")
            logger.info(f"  - Arduino Port: {config['arduino_port']}")
            logger.info(f"  - Poll Interval: {config['printer_poll_interval']}s")
            logger.info("")
            
            # Share the Arduino serial connection
            printer_client = ReceiptPrinterClient(
                config["vps_api_url"],
                arduino_serial_connection=cash_reader.serial_connection
            )
            
            printer_thread = threading.Thread(target=printer_client.run, name="PrinterClient", daemon=False)
            printer_thread.start()
            threads.append((printer_thread, printer_client))
            logger.info("[PRINTER] Receipt printer thread started (using Arduino)")
        
        logger.info("=" * 80)
        logger.info("All modules started successfully")
        logger.info("Arduino handles both cash reading AND receipt printing!")
        logger.info("Press Ctrl+C to stop")
        logger.info("=" * 80)
        
        # Wait for threads to finish
        for thread, _ in threads:
            thread.join()
            
    except KeyboardInterrupt:
        logger.info("\nShutdown signal received...")
        
        # Stop all modules
        for thread, module in threads:
            logger.info(f"Stopping {thread.name}...")
            module.running = False
        
        # Wait for threads to finish
        for thread, _ in threads:
            thread.join(timeout=5)
            
        logger.info("All modules stopped")
        
    except Exception as e:
        logger.error(f"Fatal error: {e}", exc_info=True)
    finally:
        logger.info("Peripherals manager stopped")


if __name__ == "__main__":
    main()

