#!/usr/bin/env python3
"""
Receipt Printer Client for Restaurant Kiosk
Runs on Raspberry Pi and polls the VPS for print jobs
This solves the NAT/firewall issue since Pi initiates the connection
"""

import requests
import time
import json
import logging
from typing import Optional, Dict, Any
from datetime import datetime
from escpos.printer import Usb, Network, Serial, File
from escpos.exceptions import USBNotFoundError, Error as ESCPOSError

# Configuration
# Printer Model: SHK24 (58mm thermal printer via USB-to-TTL)
VPS_API_URL = "https://bochogs-kiosk.store"  # Your VPS URL
POLL_INTERVAL = 2  # seconds between checking for new print jobs
PRINTER_TYPE = "serial"  # Using USB-to-TTL adapter

# USB Configuration (for direct USB printers)
USB_VENDOR_ID = 0x04b8
USB_PRODUCT_ID = 0x0e15

# Serial Configuration (for USB-to-TTL adapters)
# SHK24 Printer Settings
SERIAL_PORT = "/dev/ttyUSB0"  # Change based on your system
SERIAL_BAUDRATE = 9600  # SHK24 default: 9600

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('receipt_printer_client.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)


class ReceiptPrinterClient:
    """Client that polls VPS for print jobs and prints them"""
    
    def __init__(self, vps_url: str):
        self.vps_url = vps_url
        self.printer = None
        self.printer_type = PRINTER_TYPE
        self.session = requests.Session()
        
    def connect_printer(self) -> bool:
        """Establish connection to printer"""
        try:
            logger.info(f"Connecting to {self.printer_type} printer...")
            
            if self.printer_type == "usb":
                self.printer = Usb(USB_VENDOR_ID, USB_PRODUCT_ID)
            elif self.printer_type == "network":
                self.printer = Network(NETWORK_HOST, NETWORK_PORT)
            elif self.printer_type == "serial":
                self.printer = Serial(SERIAL_PORT, baudrate=SERIAL_BAUDRATE)
            elif self.printer_type == "file":
                self.printer = File("/tmp/receipt.txt")
            
            logger.info("Successfully connected to printer")
            return True
            
        except Exception as e:
            logger.error(f"Failed to connect to printer: {e}")
            return False
    
    def check_for_print_jobs(self) -> Optional[Dict[str, Any]]:
        """Poll VPS for pending print jobs"""
        try:
            url = f"{self.vps_url}/api/receipt/queue/next"
            response = self.session.get(url, timeout=5)
            
            if response.status_code == 200:
                data = response.json()
                if data.get('hasPrintJob'):
                    return data.get('receiptData')
            elif response.status_code == 204:
                # No print jobs pending
                return None
            else:
                logger.warning(f"Unexpected status code: {response.status_code}")
                
        except requests.exceptions.RequestException as e:
            logger.error(f"Error checking for print jobs: {e}")
        
        return None
    
    def print_receipt(self, receipt_data: Dict[str, Any]) -> bool:
        """Print a receipt"""
        try:
            if not self.printer:
                if not self.connect_printer():
                    return False
            
            order_number = receipt_data.get('orderNumber', 'N/A')
            logger.info(f"Printing receipt for order: {order_number}")
            
            # Initialize printer
            self.printer.hw('INIT')
            
            # Print header
            self._print_header(receipt_data)
            self._print_order_details(receipt_data)
            self._print_items(receipt_data.get('items', []))
            self._print_totals(receipt_data)
            self._print_payment_info(receipt_data)
            self._print_footer(receipt_data)
            
            # Cut paper
            self.printer.cut()
            
            logger.info(f"Receipt printed successfully for order: {order_number}")
            return True
            
        except Exception as e:
            logger.error(f"Error printing receipt: {e}")
            return False
    
    def _print_header(self, data: Dict[str, Any]):
        """Print receipt header"""
        self.printer.set(align='center', text_type='B', width=2, height=2)
        self.printer.text(data.get('restaurantName', 'Restaurant') + '\n')
        self.printer.set(align='center', text_type='normal')
        self.printer.text(data.get('restaurantAddress', '') + '\n')
        self.printer.text(data.get('restaurantPhone', '') + '\n')
        self.printer.text('\n')
        self.printer.text('=' * 32 + '\n\n')
    
    def _print_order_details(self, data: Dict[str, Any]):
        """Print order information"""
        self.printer.set(align='left', text_type='B')
        self.printer.text(f"Order #: {data.get('orderNumber', 'N/A')}\n")
        self.printer.set(text_type='normal')
        self.printer.text(f"Date: {data.get('orderDate', '')}\n")
        if data.get('customerName'):
            self.printer.text(f"Customer: {data['customerName']}\n")
        self.printer.text('\n' + '-' * 32 + '\n\n')
    
    def _print_items(self, items: list):
        """Print order items"""
        self.printer.text(f"{'Item':<20} {'Qty':>4} {'Amount':>7}\n")
        self.printer.text('-' * 32 + '\n')
        
        for item in items:
            name = item.get('productName', 'Unknown')[:20]
            qty = item.get('quantity', 0)
            price = item.get('lineTotal', 0.0)
            self.printer.text(f"{name:<20} {qty:>4} {price:>7.2f}\n")
        
        self.printer.text('\n')
    
    def _print_totals(self, data: Dict[str, Any]):
        """Print totals"""
        self.printer.text('-' * 32 + '\n')
        self.printer.text(f"{'Subtotal:':<24} {data.get('subTotal', 0):>7.2f}\n")
        
        if data.get('tax', 0) > 0:
            self.printer.text(f"{'VAT (12%):':<24} {data['tax']:>7.2f}\n")
        
        self.printer.text('-' * 32 + '\n')
        self.printer.set(text_type='B', width=2, height=2)
        self.printer.text(f"TOTAL: PHP {data.get('totalAmount', 0):.2f}\n")
        self.printer.set(text_type='normal', width=1, height=1)
        self.printer.text('\n')
    
    def _print_payment_info(self, data: Dict[str, Any]):
        """Print payment information"""
        self.printer.text(f"Payment: {data.get('paymentMethod', 'N/A')}\n")
        
        if data.get('amountPaid'):
            self.printer.text(f"Paid: PHP {data['amountPaid']:.2f}\n")
            if data.get('change', 0) > 0:
                self.printer.set(text_type='B')
                self.printer.text(f"Change: PHP {data['change']:.2f}\n")
                self.printer.set(text_type='normal')
        
        self.printer.text('\n')
    
    def _print_footer(self, data: Dict[str, Any]):
        """Print receipt footer"""
        self.printer.set(align='center')
        self.printer.text('=' * 32 + '\n')
        self.printer.set(text_type='B')
        self.printer.text('Thank You!\n')
        self.printer.set(text_type='normal')
        self.printer.text('Please come again\n\n')
        
        if data.get('qrData'):
            try:
                self.printer.qr(data['qrData'], size=6)
                self.printer.text('\n')
            except:
                pass
    
    def mark_job_completed(self, job_id: str) -> bool:
        """Notify VPS that print job is completed"""
        try:
            url = f"{self.vps_url}/api/receipt/queue/complete/{job_id}"
            response = self.session.post(url, timeout=5)
            return response.status_code == 200
        except Exception as e:
            logger.error(f"Error marking job completed: {e}")
            return False
    
    def mark_job_failed(self, job_id: str, error: str) -> bool:
        """Notify VPS that print job failed"""
        try:
            url = f"{self.vps_url}/api/receipt/queue/failed/{job_id}"
            response = self.session.post(url, json={"error": error}, timeout=5)
            return response.status_code == 200
        except Exception as e:
            logger.error(f"Error marking job failed: {e}")
            return False
    
    def run(self):
        """Main loop - poll for print jobs"""
        logger.info("=" * 60)
        logger.info("Receipt Printer Client - Polling Mode")
        logger.info(f"VPS URL: {self.vps_url}")
        logger.info(f"Poll Interval: {POLL_INTERVAL}s")
        logger.info("=" * 60)
        
        # Connect to printer on startup
        self.connect_printer()
        
        while True:
            try:
                # Check for print jobs
                job_data = self.check_for_print_jobs()
                
                if job_data:
                    job_id = job_data.get('jobId')
                    receipt_data = job_data.get('receipt')
                    
                    logger.info(f"Received print job: {job_id}")
                    
                    # Print receipt
                    success = self.print_receipt(receipt_data)
                    
                    # Notify VPS
                    if success:
                        self.mark_job_completed(job_id)
                    else:
                        self.mark_job_failed(job_id, "Printing failed")
                
                time.sleep(POLL_INTERVAL)
                
            except KeyboardInterrupt:
                logger.info("Shutting down...")
                break
            except Exception as e:
                logger.error(f"Error in main loop: {e}")
                time.sleep(POLL_INTERVAL)


def main():
    client = ReceiptPrinterClient(VPS_API_URL)
    
    try:
        client.run()
    except Exception as e:
        logger.error(f"Fatal error: {e}")
    finally:
        logger.info("Receipt printer client stopped")


if __name__ == "__main__":
    main()

