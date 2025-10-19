#!/bin/bash

# Restaurant Kiosk Deployment Script for Raspberry Pi
# This script automates the deployment process

set -e  # Exit on error

echo "=================================="
echo "Restaurant Kiosk Deployment Script"
echo "=================================="
echo ""

# Configuration
APP_DIR="/var/www/restaurant-kiosk"
BACKUP_DIR="/var/www/restaurant-kiosk.backup.$(date +%Y%m%d_%H%M%S)"
SERVICE_NAME="restaurant-kiosk"
ARDUINO_SERVICE_NAME="arduino-cash-reader"

# Check if running as root
if [ "$EUID" -eq 0 ]; then 
    echo "Please do not run as root"
    exit 1
fi

# Function to print colored output
print_info() {
    echo -e "\e[34m[INFO]\e[0m $1"
}

print_success() {
    echo -e "\e[32m[SUCCESS]\e[0m $1"
}

print_error() {
    echo -e "\e[31m[ERROR]\e[0m $1"
}

print_warning() {
    echo -e "\e[33m[WARNING]\e[0m $1"
}

# Check if services are running
if sudo systemctl is-active --quiet $SERVICE_NAME; then
    print_info "Stopping $SERVICE_NAME service..."
    sudo systemctl stop $SERVICE_NAME
fi

if sudo systemctl is-active --quiet $ARDUINO_SERVICE_NAME; then
    print_info "Stopping $ARDUINO_SERVICE_NAME service..."
    sudo systemctl stop $ARDUINO_SERVICE_NAME
fi

# Backup existing installation
if [ -d "$APP_DIR" ]; then
    print_info "Creating backup of existing installation..."
    sudo cp -r $APP_DIR $BACKUP_DIR
    print_success "Backup created at $BACKUP_DIR"
fi

# Create app directory if it doesn't exist
if [ ! -d "$APP_DIR" ]; then
    print_info "Creating application directory..."
    sudo mkdir -p $APP_DIR
fi

# Extract new version (assuming the zip file is in current directory)
if [ -f "restaurant-kiosk.zip" ]; then
    print_info "Extracting new version..."
    unzip -o restaurant-kiosk.zip -d temp_deploy
    sudo rm -rf $APP_DIR/*
    sudo mv temp_deploy/* $APP_DIR/
    rm -rf temp_deploy
    print_success "New version extracted"
else
    print_error "restaurant-kiosk.zip not found in current directory"
    exit 1
fi

# Set permissions
print_info "Setting permissions..."
sudo chown -R pi:pi $APP_DIR
chmod +x $APP_DIR/RestaurantKiosk

# Copy Arduino script if it exists
if [ -f "$APP_DIR/arduino_cash_reader.py" ]; then
    print_info "Arduino cash reader script found"
fi

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET runtime not found. Please install .NET 9 runtime first."
    exit 1
fi

# Run database migrations (if needed)
read -p "Do you want to run database migrations? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_info "Running database migrations..."
    cd $APP_DIR
    export ASPNETCORE_ENVIRONMENT=Production
    # Note: This requires EF Core tools to be installed
    # dotnet ef database update
    print_warning "Manual migration may be required. See RASPBERRY_PI_DEPLOYMENT.md"
fi

# Start services
print_info "Starting services..."
sudo systemctl start $SERVICE_NAME

if sudo systemctl is-enabled --quiet $ARDUINO_SERVICE_NAME; then
    sudo systemctl start $ARDUINO_SERVICE_NAME
fi

# Wait a bit and check if services started successfully
sleep 3

if sudo systemctl is-active --quiet $SERVICE_NAME; then
    print_success "$SERVICE_NAME is running"
else
    print_error "$SERVICE_NAME failed to start. Check logs with: sudo journalctl -u $SERVICE_NAME -n 50"
    exit 1
fi

if sudo systemctl is-enabled --quiet $ARDUINO_SERVICE_NAME; then
    if sudo systemctl is-active --quiet $ARDUINO_SERVICE_NAME; then
        print_success "$ARDUINO_SERVICE_NAME is running"
    else
        print_warning "$ARDUINO_SERVICE_NAME failed to start. Check logs with: sudo journalctl -u $ARDUINO_SERVICE_NAME -n 50"
    fi
fi

echo ""
print_success "Deployment completed successfully!"
echo ""
echo "Quick commands:"
echo "  - View logs: sudo journalctl -u $SERVICE_NAME -f"
echo "  - Restart app: sudo systemctl restart $SERVICE_NAME"
echo "  - Check status: sudo systemctl status $SERVICE_NAME"
echo "  - Access app: http://localhost:5000"
echo ""

