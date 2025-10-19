#!/bin/bash

# Cloudflare Tunnel Setup Script for Restaurant Kiosk
# This script automates the Cloudflare Tunnel installation and configuration

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

print_warning() {
    echo -e "\e[33m[WARNING]\e[0m $1"
}

echo "=========================================="
echo "Cloudflare Tunnel Setup for Raspberry Pi"
echo "=========================================="
echo ""

# Check if running on Raspberry Pi
if ! grep -q "Raspberry Pi" /proc/cpuinfo 2>/dev/null; then
    print_warning "This doesn't appear to be a Raspberry Pi"
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Get domain information
echo "Domain Configuration"
echo "===================="
echo ""
print_info "Before proceeding, make sure you have:"
echo "  1. A domain name registered"
echo "  2. Domain added to Cloudflare (free account)"
echo "  3. Nameservers updated to Cloudflare"
echo ""
read -p "Have you completed these steps? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    print_error "Please complete Cloudflare setup first"
    echo ""
    echo "Steps:"
    echo "  1. Sign up at https://cloudflare.com"
    echo "  2. Add your domain"
    echo "  3. Update nameservers at your domain registrar"
    echo "  4. Wait for DNS propagation (5-60 minutes)"
    echo ""
    exit 1
fi

echo ""
read -p "Enter your domain name (e.g., restaurant-kiosk.com): " DOMAIN_NAME
if [ -z "$DOMAIN_NAME" ]; then
    print_error "Domain name cannot be empty"
    exit 1
fi

read -p "Do you want to include www subdomain? (y/n) " -n 1 -r
echo
INCLUDE_WWW=$REPLY

read -p "Enter tunnel name [restaurant-kiosk]: " TUNNEL_NAME
TUNNEL_NAME=${TUNNEL_NAME:-restaurant-kiosk}

# Check if cloudflared is already installed
if command -v cloudflared &> /dev/null; then
    print_success "cloudflared is already installed"
    CLOUDFLARED_VERSION=$(cloudflared --version)
    print_info "Version: $CLOUDFLARED_VERSION"
else
    print_info "Installing cloudflared..."
    
    # Download cloudflared
    wget -q https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64
    sudo mv cloudflared-linux-arm64 /usr/local/bin/cloudflared
    sudo chmod +x /usr/local/bin/cloudflared
    
    print_success "cloudflared installed successfully"
fi

# Check if already authenticated
if [ -f "$HOME/.cloudflared/cert.pem" ]; then
    print_success "Already authenticated with Cloudflare"
else
    print_info "Authenticating with Cloudflare..."
    echo ""
    print_warning "A browser window will open. Please login to Cloudflare and authorize the tunnel."
    echo "Press Enter to continue..."
    read
    
    cloudflared tunnel login
    
    if [ -f "$HOME/.cloudflared/cert.pem" ]; then
        print_success "Authentication successful"
    else
        print_error "Authentication failed. Please try again."
        exit 1
    fi
fi

# Check if tunnel already exists
EXISTING_TUNNEL=$(cloudflared tunnel list | grep "$TUNNEL_NAME" | awk '{print $1}' || true)

if [ -n "$EXISTING_TUNNEL" ]; then
    print_warning "Tunnel '$TUNNEL_NAME' already exists with ID: $EXISTING_TUNNEL"
    read -p "Do you want to use the existing tunnel? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        TUNNEL_ID=$EXISTING_TUNNEL
        print_info "Using existing tunnel: $TUNNEL_ID"
    else
        print_info "Creating new tunnel with different name..."
        read -p "Enter new tunnel name: " TUNNEL_NAME
        cloudflared tunnel create "$TUNNEL_NAME"
        TUNNEL_ID=$(cloudflared tunnel list | grep "$TUNNEL_NAME" | awk '{print $1}')
        print_success "Tunnel created with ID: $TUNNEL_ID"
    fi
else
    print_info "Creating Cloudflare Tunnel..."
    cloudflared tunnel create "$TUNNEL_NAME"
    
    # Get tunnel ID
    TUNNEL_ID=$(cloudflared tunnel list | grep "$TUNNEL_NAME" | awk '{print $1}')
    
    if [ -z "$TUNNEL_ID" ]; then
        print_error "Failed to create tunnel"
        exit 1
    fi
    
    print_success "Tunnel created with ID: $TUNNEL_ID"
fi

# Save tunnel info
mkdir -p ~/kiosk-config
cat > ~/kiosk-config/tunnel-info.txt << EOF
Tunnel Name: $TUNNEL_NAME
Tunnel ID: $TUNNEL_ID
Domain: $DOMAIN_NAME
Created: $(date)
EOF

print_info "Tunnel information saved to ~/kiosk-config/tunnel-info.txt"

# Create configuration file
print_info "Creating tunnel configuration..."
mkdir -p ~/.cloudflared

cat > ~/.cloudflared/config.yml << EOF
tunnel: $TUNNEL_ID
credentials-file: /home/$(whoami)/.cloudflared/$TUNNEL_ID.json

ingress:
  # Main domain
  - hostname: $DOMAIN_NAME
    service: http://localhost:5000
EOF

if [[ $INCLUDE_WWW =~ ^[Yy]$ ]]; then
    cat >> ~/.cloudflared/config.yml << EOF
  
  # WWW subdomain
  - hostname: www.$DOMAIN_NAME
    service: http://localhost:5000
EOF
fi

cat >> ~/.cloudflared/config.yml << EOF
  
  # Catch-all rule (required)
  - service: http_status:404
EOF

print_success "Configuration file created"

# Route DNS
print_info "Configuring DNS routing..."

# Route main domain
cloudflared tunnel route dns "$TUNNEL_NAME" "$DOMAIN_NAME" || print_warning "DNS route may already exist"

if [[ $INCLUDE_WWW =~ ^[Yy]$ ]]; then
    cloudflared tunnel route dns "$TUNNEL_NAME" "www.$DOMAIN_NAME" || print_warning "DNS route may already exist"
fi

print_success "DNS routes configured"

# Test configuration
print_info "Testing tunnel configuration..."
if cloudflared tunnel info "$TUNNEL_NAME" &> /dev/null; then
    print_success "Tunnel configuration is valid"
else
    print_error "Tunnel configuration test failed"
    exit 1
fi

# Create systemd service
print_info "Creating systemd service..."

sudo tee /etc/systemd/system/cloudflared.service > /dev/null << EOF
[Unit]
Description=Cloudflare Tunnel
After=network.target

[Service]
Type=simple
User=$(whoami)
ExecStart=/usr/local/bin/cloudflared tunnel --config /home/$(whoami)/.cloudflared/config.yml run $TUNNEL_NAME
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

print_success "Systemd service created"

# Enable and start service
print_info "Enabling and starting cloudflared service..."
sudo systemctl daemon-reload
sudo systemctl enable cloudflared
sudo systemctl start cloudflared

# Wait a bit for service to start
sleep 3

# Check service status
if sudo systemctl is-active --quiet cloudflared; then
    print_success "Cloudflared service is running!"
else
    print_error "Failed to start cloudflared service"
    print_info "Check logs with: sudo journalctl -u cloudflared -n 50"
    exit 1
fi

# Update application configuration
APP_CONFIG="/var/www/restaurant-kiosk/appsettings.Production.json"
if [ -f "$APP_CONFIG" ]; then
    print_info "Updating application configuration..."
    
    # Backup existing config
    sudo cp "$APP_CONFIG" "$APP_CONFIG.backup"
    
    print_warning "Please manually update the following in $APP_CONFIG:"
    echo "  - BaseUrl: https://$DOMAIN_NAME"
    echo "  - Xendit.CallbackUrl: https://$DOMAIN_NAME/api/payment/callback"
    echo ""
    read -p "Would you like to open the file now? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        sudo nano "$APP_CONFIG"
        
        # Restart application
        if sudo systemctl is-active --quiet restaurant-kiosk; then
            print_info "Restarting application..."
            sudo systemctl restart restaurant-kiosk
        fi
    fi
else
    print_warning "Application not deployed yet. Deploy first, then update appsettings.Production.json"
fi

echo ""
print_success "==========================================="
print_success "Cloudflare Tunnel Setup Complete!"
print_success "==========================================="
echo ""
echo "Configuration Summary:"
echo "  - Tunnel Name: $TUNNEL_NAME"
echo "  - Tunnel ID: $TUNNEL_ID"
echo "  - Domain: https://$DOMAIN_NAME"
if [[ $INCLUDE_WWW =~ ^[Yy]$ ]]; then
    echo "  - WWW Domain: https://www.$DOMAIN_NAME"
fi
echo ""
echo "Your application should now be accessible at:"
echo "  🌐 https://$DOMAIN_NAME"
echo ""
echo "Next steps:"
echo "  1. Wait 1-2 minutes for DNS to propagate"
echo "  2. Visit https://$DOMAIN_NAME to verify"
echo "  3. Configure Xendit webhook: https://$DOMAIN_NAME/api/payment/callback"
echo "  4. Update appsettings.Production.json if not done already"
echo ""
echo "Useful commands:"
echo "  - Check tunnel status: sudo systemctl status cloudflared"
echo "  - View tunnel logs: sudo journalctl -u cloudflared -f"
echo "  - Restart tunnel: sudo systemctl restart cloudflared"
echo "  - Tunnel info: cloudflared tunnel info $TUNNEL_NAME"
echo ""
echo "Tunnel info saved to: ~/kiosk-config/tunnel-info.txt"
echo ""

