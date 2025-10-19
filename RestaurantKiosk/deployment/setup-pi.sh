#!/bin/bash

# Initial Raspberry Pi Setup Script for Restaurant Kiosk
# Run this script on a fresh Raspberry Pi OS installation

set -e

echo "========================================="
echo "Raspberry Pi Restaurant Kiosk Setup"
echo "========================================="
echo ""

# Check if running on Raspberry Pi
if ! grep -q "Raspberry Pi" /proc/cpuinfo 2>/dev/null; then
    echo "Warning: This doesn't appear to be a Raspberry Pi"
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

print_info() {
    echo -e "\e[34m[INFO]\e[0m $1"
}

print_success() {
    echo -e "\e[32m[SUCCESS]\e[0m $1"
}

print_error() {
    echo -e "\e[31m[ERROR]\e[0m $1"
}

# Update system
print_info "Updating system packages..."
sudo apt update && sudo apt upgrade -y

# Install basic tools
print_info "Installing basic tools..."
sudo apt install -y git curl wget vim nano htop unzip

# Install .NET 9 Runtime
print_info "Installing .NET 9 Runtime..."
if ! command -v dotnet &> /dev/null; then
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --channel 9.0 --runtime aspnetcore
    
    # Add to PATH
    echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
    echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
    export DOTNET_ROOT=$HOME/.dotnet
    export PATH=$PATH:$HOME/.dotnet
    
    rm dotnet-install.sh
    print_success ".NET 9 Runtime installed"
else
    print_success ".NET is already installed"
fi

# Install PostgreSQL
print_info "Installing PostgreSQL..."
sudo apt install -y postgresql postgresql-contrib

# Start PostgreSQL
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Get database configuration
echo ""
echo "Database Configuration"
echo "======================"
read -p "Enter database name [restaurant_kiosk]: " DB_NAME
DB_NAME=${DB_NAME:-restaurant_kiosk}

read -p "Enter database user [kiosk_user]: " DB_USER
DB_USER=${DB_USER:-kiosk_user}

read -sp "Enter database password: " DB_PASSWORD
echo ""

if [ -z "$DB_PASSWORD" ]; then
    print_error "Password cannot be empty"
    exit 1
fi

# Create database and user
print_info "Creating database and user..."
sudo -u postgres psql << EOF
CREATE DATABASE $DB_NAME;
CREATE USER $DB_USER WITH PASSWORD '$DB_PASSWORD';
GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;
\c $DB_NAME
GRANT ALL ON SCHEMA public TO $DB_USER;
EOF

print_success "Database created successfully"

# Save connection string for reference
mkdir -p ~/kiosk-config
cat > ~/kiosk-config/connection-string.txt << EOF
Server=127.0.0.1;Port=5432;Database=$DB_NAME;User Id=$DB_USER;Password=$DB_PASSWORD;
EOF
chmod 600 ~/kiosk-config/connection-string.txt

# Install Python packages for Arduino
print_info "Installing Python packages for Arduino integration..."
sudo apt install -y python3-pip python3-venv

# Create virtual environment for Arduino script
python3 -m venv ~/arduino-env
source ~/arduino-env/bin/activate
pip install pyserial
deactivate

# Add user to dialout group for serial access
print_info "Adding user to dialout group for serial port access..."
sudo usermod -a -G dialout $USER

# Install Nginx (optional)
read -p "Do you want to install Nginx reverse proxy? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_info "Installing Nginx..."
    sudo apt install -y nginx
    sudo systemctl enable nginx
    print_success "Nginx installed"
fi

# Install Chromium for kiosk mode
read -p "Do you want to install Chromium for kiosk mode? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_info "Installing Chromium and kiosk tools..."
    sudo apt install -y chromium-browser unclutter xdotool
    print_success "Chromium installed"
fi

# Create application directory
print_info "Creating application directory..."
sudo mkdir -p /var/www/restaurant-kiosk
sudo chown -R $USER:$USER /var/www/restaurant-kiosk

# Create backup directory
mkdir -p ~/backups

# Performance optimization
read -p "Do you want to apply performance optimizations? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    print_info "Applying performance optimizations..."
    
    # GPU memory allocation
    if ! grep -q "gpu_mem=256" /boot/firmware/config.txt; then
        echo "gpu_mem=256" | sudo tee -a /boot/firmware/config.txt
    fi
    
    print_success "Performance optimizations applied"
    print_info "Reboot required for changes to take effect"
fi

echo ""
print_success "========================================="
print_success "Setup completed successfully!"
print_success "========================================="
echo ""
echo "Configuration Summary:"
echo "  - Database: $DB_NAME"
echo "  - DB User: $DB_USER"
echo "  - Connection string saved to: ~/kiosk-config/connection-string.txt"
echo "  - Application directory: /var/www/restaurant-kiosk"
echo ""
echo "Next steps:"
echo "  1. Upload your application (restaurant-kiosk.zip)"
echo "  2. Run ./deploy.sh to deploy the application"
echo "  3. Configure systemd services (see RASPBERRY_PI_DEPLOYMENT.md)"
echo "  4. Set up kiosk mode (see RASPBERRY_PI_DEPLOYMENT.md)"
echo ""
echo "IMPORTANT: You need to logout and login again for group changes to take effect!"
echo ""

