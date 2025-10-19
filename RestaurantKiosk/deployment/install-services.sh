#!/bin/bash

# Install systemd services for Restaurant Kiosk

set -e

print_info() {
    echo -e "\e[34m[INFO]\e[0m $1"
}

print_success() {
    echo -e "\e[32m[SUCCESS]\e[0m $1"
}

print_error() {
    echo -e "\e[31m[ERROR]\e[0m $1"
}

# Check if running as regular user
if [ "$EUID" -eq 0 ]; then 
    print_error "Please do not run as root"
    exit 1
fi

# Get configuration
APP_DIR="/var/www/restaurant-kiosk"
USER_NAME=$(whoami)
DOTNET_PATH="$HOME/.dotnet"

print_info "Creating Restaurant Kiosk systemd service..."

# Create main application service
sudo tee /etc/systemd/system/restaurant-kiosk.service > /dev/null << EOF
[Unit]
Description=Restaurant Kiosk Application
After=network.target postgresql.service

[Service]
Type=notify
User=$USER_NAME
WorkingDirectory=$APP_DIR
ExecStart=$DOTNET_PATH/dotnet $APP_DIR/RestaurantKiosk.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=restaurant-kiosk
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROOT=$DOTNET_PATH

[Install]
WantedBy=multi-user.target
EOF

print_success "Main service file created"

# Create Arduino cash reader service
read -p "Do you want to install Arduino Cash Reader service? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_info "Creating Arduino Cash Reader service..."
    
    sudo tee /etc/systemd/system/arduino-cash-reader.service > /dev/null << EOF
[Unit]
Description=Arduino Cash Reader Service
After=restaurant-kiosk.service

[Service]
Type=simple
User=$USER_NAME
WorkingDirectory=$APP_DIR
ExecStart=/usr/bin/python3 $APP_DIR/arduino_cash_reader.py
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

    print_success "Arduino service file created"
fi

# Reload systemd
print_info "Reloading systemd daemon..."
sudo systemctl daemon-reload

# Enable services
print_info "Enabling services..."
sudo systemctl enable restaurant-kiosk.service

if [[ $REPLY =~ ^[Yy]$ ]]; then
    sudo systemctl enable arduino-cash-reader.service
fi

echo ""
print_success "Services installed successfully!"
echo ""
echo "Service management commands:"
echo "  Start:   sudo systemctl start restaurant-kiosk"
echo "  Stop:    sudo systemctl stop restaurant-kiosk"
echo "  Restart: sudo systemctl restart restaurant-kiosk"
echo "  Status:  sudo systemctl status restaurant-kiosk"
echo "  Logs:    sudo journalctl -u restaurant-kiosk -f"
echo ""
echo "To start the services now:"
echo "  sudo systemctl start restaurant-kiosk"
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "  sudo systemctl start arduino-cash-reader"
fi
echo ""

