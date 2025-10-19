#!/usr/bin/env python3
"""
Receipt Printer API for Restaurant Kiosk
Handles thermal receipt printing via ESC/POS protocol on Raspberry Pi
"""

import requests
import time
import json
import logging
from typing import Optional, Dict, Any
from datetime import datetime
from flask import Flask, request, jsonify
from escpos.printer import Usb, Network, Serial, File
from escpos.exceptions import USBNotFoundError, Error as ESCPOSError

# Configuration
# Printer Model: SHK24 (58mm thermal printer)
PRINTER_TYPE = "serial"  # Options: "usb", "network", "serial", "file"

# USB Configuration (for direct USB printers)
USB_VENDOR_ID = 0x04b8  # Epson - change to your printer's vendor ID
USB_PRODUCT_ID = 0x0e15  # Epson TM-T20 - change to your printer's product ID

# Network Configuration (if using network printer)
NETWORK_HOST = "192.168.1.100"
NETWORK_PORT = 9100

# Serial Configuration (for USB-to-TTL adapters)
# SHK24 Printer Settings
SERIAL_PORT = "/dev/ttyUSB0"  # Common: /dev/ttyUSB0, /dev/ttyAMA0, /dev/serial0
SERIAL_BAUDRATE = 9600  # SHK24 default: 9600 (can also try 19200 if needed)

# File Configuration (for testing without physical printer)
FILE_PATH = "/tmp/receipt.txt"

# Flask app configuration
FLASK_HOST = "0.0.0.0"
FLASK_PORT = 5001

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('receipt_printer.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

# Initialize Flask app
app = Flask(__name__)


class ReceiptPrinter:
    """Manages thermal receipt printing"""
    
    def __init__(self):
        self.printer = None
        self.printer_type = PRINTER_TYPE
        
    def connect(self) -> bool:
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
                self.printer = File(FILE_PATH)
            else:
                logger.error(f"Unknown printer type: {self.printer_type}")
                return False
                
            logger.info("Successfully connected to printer")
            return True
            
        except USBNotFoundError:
            logger.error(f"USB printer not found (VID: 0x{USB_VENDOR_ID:04x}, PID: 0x{USB_PRODUCT_ID:04x})")
            return False
        except Exception as e:
            logger.error(f"Failed to connect to printer: {e}")
            return False
    
    def disconnect(self):
        """Close printer connection"""
        try:
            if self.printer:
                self.printer.close()
                logger.info("Disconnected from printer")
        except Exception as e:
            logger.error(f"Error disconnecting from printer: {e}")
    
    def print_receipt(self, receipt_data: Dict[str, Any]) -> bool:
        """Print a receipt from structured data"""
        try:
            if not self.printer:
                if not self.connect():
                    return False
            
            logger.info(f"Printing receipt for order: {receipt_data.get('orderNumber')}")
            
            # Initialize printer
            self.printer.hw('INIT')
            
            # Print header
            self._print_header(receipt_data)
            
            # Print order details
            self._print_order_details(receipt_data)
            
            # Print items
            self._print_items(receipt_data.get('items', []))
            
            # Print totals
            self._print_totals(receipt_data)
            
            # Print payment info
            self._print_payment_info(receipt_data)
            
            # Print footer
            self._print_footer(receipt_data)
            
            # Cut paper
            self.printer.cut()
            
            logger.info(f"Receipt printed successfully for order: {receipt_data.get('orderNumber')}")
            return True
            
        except ESCPOSError as e:
            logger.error(f"ESC/POS error printing receipt: {e}")
            return False
        except Exception as e:
            logger.error(f"Error printing receipt: {e}")
            return False
    
    def _print_header(self, data: Dict[str, Any]):
        """Print receipt header"""
        # Restaurant name (centered, large text)
        self.printer.set(align='center', text_type='B', width=2, height=2)
        self.printer.text(data.get('restaurantName', 'Restaurant Name') + '\n')
        
        # Restaurant details (centered, normal text)
        self.printer.set(align='center', text_type='normal')
        self.printer.text(data.get('restaurantAddress', '') + '\n')
        self.printer.text(data.get('restaurantPhone', '') + '\n')
        self.printer.text(data.get('restaurantEmail', '') + '\n')
        self.printer.text('\n')
        
        # Separator line
        self.printer.text('=' * 32 + '\n')
        self.printer.text('\n')
    
    def _print_order_details(self, data: Dict[str, Any]):
        """Print order information"""
        self.printer.set(align='left', text_type='normal')
        
        # Order number (bold)
        self.printer.set(text_type='B')
        self.printer.text(f"Order #: {data.get('orderNumber', 'N/A')}\n")
        self.printer.set(text_type='normal')
        
        # Date and time
        order_date = data.get('orderDate', datetime.now().strftime('%Y-%m-%d %H:%M:%S'))
        self.printer.text(f"Date: {order_date}\n")
        
        # Customer info (if provided)
        if data.get('customerName'):
            self.printer.text(f"Customer: {data['customerName']}\n")
        
        self.printer.text('\n')
        self.printer.text('-' * 32 + '\n')
        self.printer.text('\n')
    
    def _print_items(self, items: list):
        """Print order items"""
        self.printer.set(align='left', text_type='normal')
        
        # Header
        self.printer.text(f"{'Item':<20} {'Qty':>4} {'Amount':>7}\n")
        self.printer.text('-' * 32 + '\n')
        
        # Items
        for item in items:
            name = item.get('productName', 'Unknown')
            qty = item.get('quantity', 0)
            price = item.get('lineTotal', 0.0)
            
            # Truncate long names
            if len(name) > 20:
                name = name[:17] + '...'
            
            self.printer.text(f"{name:<20} {qty:>4} {price:>7.2f}\n")
            
            # Print notes if any
            if item.get('notes'):
                self.printer.set(text_type='U')
                self.printer.text(f"  Note: {item['notes']}\n")
                self.printer.set(text_type='normal')
        
        self.printer.text('\n')
    
    def _print_totals(self, data: Dict[str, Any]):
        """Print totals section"""
        self.printer.set(align='left', text_type='normal')
        self.printer.text('-' * 32 + '\n')
        
        subtotal = data.get('subTotal', 0.0)
        tax = data.get('tax', 0.0)
        service_charge = data.get('serviceCharge', 0.0)
        total = data.get('totalAmount', 0.0)
        
        self.printer.text(f"{'Subtotal:':<24} {subtotal:>7.2f}\n")
        
        if tax > 0:
            self.printer.text(f"{'VAT (12%):':<24} {tax:>7.2f}\n")
        
        if service_charge > 0:
            self.printer.text(f"{'Service Charge:':<24} {service_charge:>7.2f}\n")
        
        self.printer.text('-' * 32 + '\n')
        
        # Total (bold, larger)
        self.printer.set(text_type='B', width=2, height=2)
        self.printer.text(f"TOTAL: PHP {total:.2f}\n")
        self.printer.set(text_type='normal', width=1, height=1)
        
        self.printer.text('\n')
    
    def _print_payment_info(self, data: Dict[str, Any]):
        """Print payment information"""
        self.printer.set(align='left', text_type='normal')
        
        payment_method = data.get('paymentMethod', 'N/A')
        self.printer.text(f"Payment Method: {payment_method}\n")
        
        # Cash payment details
        if payment_method.lower() == 'cash' and data.get('amountPaid'):
            amount_paid = data.get('amountPaid', 0.0)
            change = data.get('change', 0.0)
            
            self.printer.text(f"Amount Paid: PHP {amount_paid:.2f}\n")
            if change > 0:
                self.printer.set(text_type='B')
                self.printer.text(f"Change: PHP {change:.2f}\n")
                self.printer.set(text_type='normal')
        
        self.printer.text('\n')
    
    def _print_footer(self, data: Dict[str, Any]):
        """Print receipt footer"""
        self.printer.set(align='center', text_type='normal')
        
        self.printer.text('=' * 32 + '\n')
        self.printer.text('\n')
        
        # Thank you message
        self.printer.set(text_type='B')
        self.printer.text('Thank You!\n')
        self.printer.set(text_type='normal')
        self.printer.text('Please come again\n')
        self.printer.text('\n')
        
        # Footer message
        if data.get('footerMessage'):
            self.printer.text(data['footerMessage'] + '\n')
            self.printer.text('\n')
        
        # Status indicator
        self.printer.text(f"Status: {data.get('status', 'PAID')}\n")
        self.printer.text('\n')
        
        # QR code (if provided)
        if data.get('qrData'):
            try:
                self.printer.qr(data['qrData'], size=6)
                self.printer.text('\n')
            except Exception as e:
                logger.warning(f"Could not print QR code: {e}")
        
        self.printer.text('\n')
    
    def test_print(self) -> bool:
        """Print a test receipt"""
        try:
            if not self.printer:
                if not self.connect():
                    return False
            
            logger.info("Printing test receipt...")
            
            self.printer.hw('INIT')
            self.printer.set(align='center', text_type='B', width=2, height=2)
            self.printer.text('TEST RECEIPT\n')
            self.printer.set(align='center', text_type='normal', width=1, height=1)
            self.printer.text('\n')
            self.printer.text(f"Date: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
            self.printer.text('\n')
            self.printer.text('Printer is working correctly!\n')
            self.printer.text('\n')
            self.printer.text('=' * 32 + '\n')
            self.printer.text('\n')
            self.printer.cut()
            
            logger.info("Test receipt printed successfully")
            return True
            
        except Exception as e:
            logger.error(f"Error printing test receipt: {e}")
            return False


# Global printer instance
printer = ReceiptPrinter()


# Flask API endpoints
@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        'status': 'ok',
        'service': 'receipt-printer',
        'timestamp': datetime.now().isoformat()
    })


@app.route('/api/receipt/print', methods=['POST'])
def print_receipt():
    """Print a receipt from JSON data"""
    try:
        receipt_data = request.get_json()
        
        if not receipt_data:
            return jsonify({
                'success': False,
                'message': 'No receipt data provided'
            }), 400
        
        logger.info(f"Received print request for order: {receipt_data.get('orderNumber')}")
        
        success = printer.print_receipt(receipt_data)
        
        if success:
            return jsonify({
                'success': True,
                'message': 'Receipt printed successfully',
                'orderNumber': receipt_data.get('orderNumber')
            })
        else:
            return jsonify({
                'success': False,
                'message': 'Failed to print receipt'
            }), 500
            
    except Exception as e:
        logger.error(f"Error in print_receipt endpoint: {e}")
        return jsonify({
            'success': False,
            'message': str(e)
        }), 500


@app.route('/api/receipt/test', methods=['POST'])
def test_print_receipt():
    """Print a test receipt"""
    try:
        success = printer.test_print()
        
        if success:
            return jsonify({
                'success': True,
                'message': 'Test receipt printed successfully'
            })
        else:
            return jsonify({
                'success': False,
                'message': 'Failed to print test receipt'
            }), 500
            
    except Exception as e:
        logger.error(f"Error in test_print endpoint: {e}")
        return jsonify({
            'success': False,
            'message': str(e)
        }), 500


@app.route('/api/receipt/status', methods=['GET'])
def printer_status():
    """Get printer connection status"""
    try:
        if printer.printer:
            return jsonify({
                'success': True,
                'connected': True,
                'printerType': printer.printer_type
            })
        else:
            # Try to connect
            connected = printer.connect()
            return jsonify({
                'success': True,
                'connected': connected,
                'printerType': printer.printer_type
            })
            
    except Exception as e:
        logger.error(f"Error checking printer status: {e}")
        return jsonify({
            'success': False,
            'connected': False,
            'message': str(e)
        }), 500


def main():
    """Main entry point"""
    logger.info("=" * 60)
    logger.info("Restaurant Kiosk - Receipt Printer Service")
    logger.info("=" * 60)
    logger.info(f"Printer Type: {PRINTER_TYPE}")
    logger.info(f"Flask API: http://{FLASK_HOST}:{FLASK_PORT}")
    logger.info("=" * 60)
    
    # Try to connect to printer on startup
    printer.connect()
    
    # Start Flask server
    try:
        app.run(host=FLASK_HOST, port=FLASK_PORT, debug=False)
    except Exception as e:
        logger.error(f"Fatal error: {e}")
    finally:
        printer.disconnect()
        logger.info("Receipt printer service stopped")


if __name__ == "__main__":
    main()

