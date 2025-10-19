#!/bin/bash
# Installation script for unified kiosk peripherals manager
# This script helps migrate from separate cash reader and printer scripts to the unified version

set -e

echo "=========================================="
echo "Kiosk Peripherals - Unified Installation"
echo "=========================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Configuration
INSTALL_DIR="/home/pi/kiosk"
SERVICE_FILE="kiosk-peripherals.service"
CONFIG_FILE="cash_reader_config.json"

echo -e "${YELLOW}This script will:${NC}"
echo "1. Stop and disable old separate services (if they exist)"
echo "2. Install the unified kiosk_peripherals.py script"
echo "3. Set up the unified systemd service"
echo "4. Start the new unified service"
echo ""
read -p "Continue? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Installation cancelled."
    exit 1
fi

# Create installation directory
echo -e "${GREEN}Creating installation directory...${NC}"
mkdir -p $INSTALL_DIR
cd $INSTALL_DIR

# Stop old services if they exist
echo -e "${GREEN}Stopping old services (if they exist)...${NC}"
if systemctl is-active --quiet receipt-printer-client.service; then
    echo "  - Stopping receipt-printer-client.service"
    sudo systemctl stop receipt-printer-client.service
    sudo systemctl disable receipt-printer-client.service
fi

if systemctl is-active --quiet arduino-cash-reader.service; then
    echo "  - Stopping arduino-cash-reader.service"
    sudo systemctl stop arduino-cash-reader.service
    sudo systemctl disable arduino-cash-reader.service
fi

# Backup old scripts if they exist
echo -e "${GREEN}Backing up old scripts...${NC}"
if [ -f "arduino_cash_reader.py" ]; then
    mv arduino_cash_reader.py arduino_cash_reader.py.backup.$(date +%Y%m%d_%H%M%S)
    echo "  - Backed up arduino_cash_reader.py"
fi

if [ -f "receipt_printer_client.py" ]; then
    mv receipt_printer_client.py receipt_printer_client.py.backup.$(date +%Y%m%d_%H%M%S)
    echo "  - Backed up receipt_printer_client.py"
fi

# Check if new script exists
if [ ! -f "kiosk_peripherals.py" ]; then
    echo -e "${RED}Error: kiosk_peripherals.py not found in current directory${NC}"
    echo "Please copy kiosk_peripherals.py to $INSTALL_DIR first"
    exit 1
fi

# Make script executable
chmod +x kiosk_peripherals.py

# Check configuration file
if [ ! -f "$CONFIG_FILE" ]; then
    echo -e "${YELLOW}Warning: $CONFIG_FILE not found${NC}"
    echo "Please create $CONFIG_FILE before starting the service"
    echo "Example configuration available in kiosk_peripherals_config.example.json"
fi

# Install Python dependencies
echo -e "${GREEN}Installing Python dependencies...${NC}"
echo "  - Using apt for system packages (recommended)..."
sudo apt update
sudo apt install -y python3-serial python3-requests

echo "  - Installing python-escpos via pip (not available in apt)..."
sudo pip3 install python-escpos --break-system-packages 2>/dev/null || sudo pip3 install python-escpos

echo "  ✓ Dependencies installed system-wide"

# Install systemd service
echo -e "${GREEN}Installing systemd service...${NC}"
if [ -f "$SERVICE_FILE" ]; then
    sudo cp $SERVICE_FILE /etc/systemd/system/
    sudo chmod 644 /etc/systemd/system/$SERVICE_FILE
    sudo systemctl daemon-reload
    echo "  - Service file installed"
else
    echo -e "${RED}Error: $SERVICE_FILE not found${NC}"
    echo "Please copy deployment/$SERVICE_FILE to $INSTALL_DIR first"
    exit 1
fi

# Create log directory
sudo mkdir -p /var/log
sudo touch /var/log/kiosk-peripherals.log
sudo chown pi:pi /var/log/kiosk-peripherals.log

# Clean up old service files (optional)
echo -e "${GREEN}Cleaning up old service files...${NC}"
if [ -f "/etc/systemd/system/receipt-printer-client.service" ]; then
    read -p "Remove old receipt-printer-client.service? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        sudo rm /etc/systemd/system/receipt-printer-client.service
        echo "  - Removed receipt-printer-client.service"
    fi
fi

if [ -f "/etc/systemd/system/arduino-cash-reader.service" ]; then
    read -p "Remove old arduino-cash-reader.service? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        sudo rm /etc/systemd/system/arduino-cash-reader.service
        echo "  - Removed arduino-cash-reader.service"
    fi
fi

sudo systemctl daemon-reload

# Enable and start service
echo -e "${GREEN}Enabling and starting unified service...${NC}"
sudo systemctl enable kiosk-peripherals.service

echo ""
echo -e "${YELLOW}Ready to start the service!${NC}"
echo ""
read -p "Start kiosk-peripherals.service now? (y/n) " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    sudo systemctl start kiosk-peripherals.service
    sleep 2
    
    echo ""
    echo "=========================================="
    echo -e "${GREEN}Installation Complete!${NC}"
    echo "=========================================="
    echo ""
    echo "Service Status:"
    sudo systemctl status kiosk-peripherals.service --no-pager
    echo ""
    echo "Useful Commands:"
    echo "  View logs:       sudo journalctl -u kiosk-peripherals.service -f"
    echo "  Check status:    sudo systemctl status kiosk-peripherals.service"
    echo "  Stop service:    sudo systemctl stop kiosk-peripherals.service"
    echo "  Restart service: sudo systemctl restart kiosk-peripherals.service"
    echo ""
else
    echo ""
    echo "=========================================="
    echo -e "${GREEN}Installation Complete!${NC}"
    echo "=========================================="
    echo ""
    echo "Service installed but not started."
    echo "To start manually: sudo systemctl start kiosk-peripherals.service"
    echo ""
fi

echo -e "${YELLOW}Next Steps:${NC}"
echo "1. Verify configuration in $INSTALL_DIR/$CONFIG_FILE"
echo "2. Check serial ports for Arduino and printer (ls -l /dev/ttyUSB*)"
echo "3. Monitor logs for any issues"
echo ""
echo "For more information, see PERIPHERALS_UNIFIED_SETUP.md"

