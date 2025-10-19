#!/usr/bin/env python3
"""
Arduino Cash Reader API for Restaurant Kiosk
Reads bill and coin acceptor data from Arduino and sends updates to the ASP.NET Core API

Configuration:
  - For VPS deployment: Set vps_api_url in cash_reader_config.json
  - The Python script makes OUTGOING connections to the VPS
  - Dynamic home IP is NOT a problem - only VPS needs stable address
"""

import serial
import requests
import time
import json
import logging
import os
from typing import Optional
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

# Load configuration from file
def load_config():
    """Load configuration from cash_reader_config.json"""
    config_file = Path(__file__).parent / "cash_reader_config.json"
    
    # Default configuration
    default_config = {
        "vps_api_url": "http://localhost:5000",
        "arduino_port": "/dev/ttyUSB0",
        "baud_rate": 9600,
        "reconnect_delay_seconds": 5,
        "connection_timeout_seconds": 10,
        "retry_attempts": 3,
        "api_key": None,
        "environment": "development"
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

# Configuration from config file
KIOSK_API_URL = config["vps_api_url"]
ARDUINO_PORT = config["arduino_port"]
BAUD_RATE = config["baud_rate"]
RECONNECT_DELAY = config["reconnect_delay_seconds"]
CONNECTION_TIMEOUT = config["connection_timeout_seconds"]
RETRY_ATTEMPTS = config["retry_attempts"]
API_KEY = config.get("api_key")

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('cash_reader.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)


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
    
    def __init__(self, port: str, baud_rate: int, api_url: str):
        self.port = port
        self.baud_rate = baud_rate
        self.api_url = api_url
        self.serial_connection: Optional[serial.Serial] = None
        self.current_order: Optional[str] = None
        self.active_sessions: dict = {}  # Cache of active payment sessions
        self.last_poll_time: float = 0
        self.poll_interval: int = 5  # Poll for new sessions every 5 seconds
        self.session = requests.Session()
        
    def poll_active_sessions(self) -> bool:
        """Poll VPS for active payment sessions"""
        try:
            url = f"{self.api_url}/api/cash-payment/active-sessions"
            headers = {}
            if API_KEY:
                headers['X-API-Key'] = API_KEY
            
            response = self.session.get(url, headers=headers, timeout=CONNECTION_TIMEOUT)
            
            if response.status_code == 200:
                data = response.json()
                if data.get('success'):
                    new_sessions = {s['orderNumber']: s for s in data.get('sessions', [])}
                    
                    # Detect new sessions
                    for order_num, session_data in new_sessions.items():
                        if order_num not in self.active_sessions:
                            logger.info(f"New payment session detected: {order_num} - Amount: ₱{session_data['totalRequired']}")
                            print(f"\n{'='*60}")
                            print(f"NEW ORDER WAITING FOR CASH PAYMENT")
                            print(f"Order Number: {order_num}")
                            print(f"Total Required: ₱{session_data['totalRequired']}")
                            print(f"Please insert cash into the acceptor")
                            print(f"{'='*60}\n")
                    
                    # Detect completed/cancelled sessions
                    for order_num in list(self.active_sessions.keys()):
                        if order_num not in new_sessions:
                            logger.info(f"Payment session completed/cancelled: {order_num}")
                            if self.current_order == order_num:
                                self.current_order = None
                    
                    self.active_sessions = new_sessions
                    
                    # Auto-select the first active session if we don't have one
                    if not self.current_order and self.active_sessions:
                        self.current_order = list(self.active_sessions.keys())[0]
                        logger.info(f"Auto-selected order for payment: {self.current_order}")
                    
                    return True
                else:
                    logger.warning(f"API returned success=false: {data}")
                    return False
            else:
                logger.error(f"Failed to poll active sessions: {response.status_code}")
                return False
                
        except requests.exceptions.ConnectionError as e:
            logger.error(f"Connection error polling active sessions: {e}")
            return False
        except Exception as e:
            logger.error(f"Error polling active sessions: {e}")
            return False
    
    def connect_arduino(self) -> bool:
        """Establish connection to Arduino"""
        try:
            logger.info(f"Connecting to Arduino on {self.port} at {self.baud_rate} baud...")
            self.serial_connection = serial.Serial(
                port=self.port,
                baudrate=self.baud_rate,
                timeout=1
            )
            time.sleep(2)  # Wait for Arduino to reset
            logger.info("Successfully connected to Arduino")
            return True
        except serial.SerialException as e:
            logger.error(f"Failed to connect to Arduino: {e}")
            return False
        except Exception as e:
            logger.error(f"Unexpected error connecting to Arduino: {e}")
            return False
    
    def disconnect_arduino(self):
        """Close Arduino connection"""
        if self.serial_connection and self.serial_connection.is_open:
            self.serial_connection.close()
            logger.info("Disconnected from Arduino")
    
    def send_cash_update(self, cash_update: CashUpdate) -> bool:
        """Send cash amount update to kiosk API"""
        max_retries = RETRY_ATTEMPTS
        
        for attempt in range(max_retries):
            try:
                url = f"{self.api_url}/api/cash-payment/update"
                payload = {
                    "orderNumber": cash_update.order_number,
                    "amountAdded": cash_update.amount_added
                }
                
                headers = {'Content-Type': 'application/json'}
                if API_KEY:
                    headers['X-API-Key'] = API_KEY
                
                logger.info(f"Sending cash update to {url}: {payload} (Attempt {attempt + 1}/{max_retries})")
                response = self.session.post(url, json=payload, headers=headers, timeout=CONNECTION_TIMEOUT)
            
                if response.status_code == 200:
                    data = response.json()
                    logger.info(f"Cash update successful: {data}")
                    
                    # Check if payment is complete
                    if data.get('isComplete'):
                        logger.info(f"Payment completed for order {cash_update.order_number}")
                        self.current_order = None
                        
                    return True
                else:
                    logger.error(f"Failed to send cash update: {response.status_code} - {response.text}")
                    if attempt < max_retries - 1:
                        logger.info(f"Retrying in 2 seconds...")
                        time.sleep(2)
                        continue
                    return False
                    
            except requests.exceptions.Timeout:
                logger.error(f"Request timeout sending cash update (attempt {attempt + 1}/{max_retries})")
                if attempt < max_retries - 1:
                    logger.info(f"Retrying in 2 seconds...")
                    time.sleep(2)
                    continue
                return False
            except requests.exceptions.ConnectionError as e:
                logger.error(f"Connection error - cannot reach VPS API at {url}: {e}")
                if attempt < max_retries - 1:
                    logger.info(f"Retrying in 2 seconds...")
                    time.sleep(2)
                    continue
                return False
            except Exception as e:
                logger.error(f"Error sending cash update: {e}")
                if attempt < max_retries - 1:
                    logger.info(f"Retrying in 2 seconds...")
                    time.sleep(2)
                    continue
                return False
        
        return False
    
    def parse_arduino_data(self, data: str) -> Optional[CashUpdate]:
        """Parse data received from Arduino
        
        Expected format from Arduino:
        - "BILL:100" - ₱100 bill inserted
        - "COIN:5" - ₱5 coin inserted
        - "CANCEL" - Cancel current order
        
        Note: Order number is auto-selected from active sessions (no need for ORDER: command)
        """
        try:
            data = data.strip()
            
            if not data:
                return None
            
            logger.debug(f"Received from Arduino: {data}")
            
            # Handle cancel command
            if data == "CANCEL":
                if self.current_order:
                    logger.info(f"Cancelling payment for order: {self.current_order}")
                    self.cancel_payment(self.current_order)
                    self.current_order = None
                return None
            
            # Handle cash insertion
            if not self.current_order:
                logger.warning("Received cash data but no active order session. Waiting for order from VPS...")
                return None
            
            amount = 0.0
            
            if data.startswith("BILL:"):
                amount = float(data.split(":", 1)[1])
                logger.info(f"Bill inserted: ₱{amount} for order {self.current_order}")
                print(f"✓ Bill: ₱{amount} (Order: {self.current_order})")
            elif data.startswith("COIN:"):
                amount = float(data.split(":", 1)[1])
                logger.info(f"Coin inserted: ₱{amount} for order {self.current_order}")
                print(f"✓ Coin: ₱{amount} (Order: {self.current_order})")
            else:
                logger.warning(f"Unknown Arduino command: {data}")
                return None
            
            if amount > 0:
                return CashUpdate(
                    order_number=self.current_order,
                    amount_added=amount
                )
            
            return None
            
        except ValueError as e:
            logger.error(f"Error parsing amount: {e}")
            return None
        except Exception as e:
            logger.error(f"Error parsing Arduino data: {e}")
            return None
    
    def cancel_payment(self, order_number: str) -> bool:
        """Cancel payment for an order"""
        try:
            url = f"{self.api_url}/api/cash-payment/cancel/{order_number}"
            response = self.session.post(url, timeout=5)
            
            if response.status_code == 200:
                logger.info(f"Payment cancelled successfully for order {order_number}")
                return True
            else:
                logger.error(f"Failed to cancel payment: {response.status_code}")
                return False
                
        except Exception as e:
            logger.error(f"Error cancelling payment: {e}")
            return False
    
    def read_loop(self):
        """Main loop to read Arduino data and send updates"""
        logger.info("Starting cash reader loop...")
        logger.info("Polling VPS for active payment sessions...")
        
        while True:
            try:
                # Poll VPS for active payment sessions periodically
                current_time = time.time()
                if current_time - self.last_poll_time >= self.poll_interval:
                    self.poll_active_sessions()
                    self.last_poll_time = current_time
                    
                    # Display current status
                    if self.active_sessions:
                        logger.info(f"Active payment sessions: {len(self.active_sessions)}")
                        if self.current_order:
                            session = self.active_sessions.get(self.current_order)
                            if session:
                                logger.info(f"Current order: {self.current_order} - "
                                          f"₱{session['amountInserted']}/₱{session['totalRequired']}")
                    else:
                        logger.debug("No active payment sessions. Waiting for orders...")
                
                # Connect to Arduino if not connected
                if not self.serial_connection or not self.serial_connection.is_open:
                    if not self.connect_arduino():
                        logger.warning(f"Retrying Arduino connection in {RECONNECT_DELAY} seconds...")
                        time.sleep(RECONNECT_DELAY)
                        continue
                
                # Read line from Arduino
                if self.serial_connection.in_waiting > 0:
                    line = self.serial_connection.readline().decode('utf-8', errors='ignore')
                    
                    # Parse and process the data
                    cash_update = self.parse_arduino_data(line)
                    
                    if cash_update:
                        # Send update to API
                        self.send_cash_update(cash_update)
                
                time.sleep(0.1)  # Small delay to prevent CPU spinning
                
            except serial.SerialException as e:
                logger.error(f"Serial communication error: {e}")
                self.disconnect_arduino()
                time.sleep(RECONNECT_DELAY)
                
            except KeyboardInterrupt:
                logger.info("Shutting down cash reader...")
                break
                
            except Exception as e:
                logger.error(f"Unexpected error in read loop: {e}")
                time.sleep(1)
        
        self.disconnect_arduino()


def main():
    """Main entry point"""
    logger.info("=" * 60)
    logger.info("Restaurant Kiosk - Arduino Cash Reader (Polling Mode)")
    logger.info("=" * 60)
    logger.info(f"Environment: {config.get('environment', 'development')}")
    logger.info(f"Arduino Port: {ARDUINO_PORT}")
    logger.info(f"Baud Rate: {BAUD_RATE}")
    logger.info(f"VPS API URL: {KIOSK_API_URL}")
    logger.info(f"Connection Timeout: {CONNECTION_TIMEOUT}s")
    logger.info(f"Retry Attempts: {RETRY_ATTEMPTS}")
    logger.info(f"API Key Configured: {'Yes' if API_KEY else 'No'}")
    logger.info("=" * 60)
    logger.info("POLLING ARCHITECTURE:")
    logger.info("- Raspberry Pi polls VPS every 5 seconds for active payment sessions")
    logger.info("- When new order detected, cash acceptor is ready to receive payment")
    logger.info("- Arduino sends cash data → Python → VPS (updates session)")
    logger.info("- Browser polls VPS for status updates")
    logger.info("=" * 60)
    logger.info("NOTE: Dynamic home IP is OK - only VPS needs static address")
    logger.info("Python makes OUTGOING connections to VPS (works through NAT)")
    logger.info("=" * 60)
    
    reader = ArduinoCashReader(
        port=ARDUINO_PORT,
        baud_rate=BAUD_RATE,
        api_url=KIOSK_API_URL
    )
    
    try:
        reader.read_loop()
    except Exception as e:
        logger.error(f"Fatal error: {e}")
    finally:
        reader.disconnect_arduino()
        logger.info("Cash reader stopped")


if __name__ == "__main__":
    main()

