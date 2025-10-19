# PowerShell Script to Build and Deploy Restaurant Kiosk to Raspberry Pi
# Run this on your Windows development machine

param(
    [Parameter(Mandatory=$false)]
    [string]$PiHost = "restaurant-kiosk.local",
    
    [Parameter(Mandatory=$false)]
    [string]$PiUser = "pi",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Restaurant Kiosk Deployment to Raspberry Pi" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ProjectPath = Join-Path $ProjectRoot "RestaurantKiosk.csproj"
$PublishDir = Join-Path $ProjectRoot "publish"
$ZipFile = Join-Path $ScriptDir "restaurant-kiosk.zip"

# Check if project file exists
if (-not (Test-Path $ProjectPath)) {
    Write-Error-Custom "Project file not found at: $ProjectPath"
    exit 1
}

# Build Application
if (-not $SkipBuild) {
    Write-Info "Building application for Raspberry Pi (linux-arm64)..."
    
    # Clean previous publish
    if (Test-Path $PublishDir) {
        Remove-Item -Recurse -Force $PublishDir
    }
    
    try {
        # Publish for Raspberry Pi
        dotnet publish $ProjectPath `
            -c Release `
            -r linux-arm64 `
            --self-contained false `
            -o $PublishDir `
            /p:PublishTrimmed=false
        
        Write-Success "Build completed successfully"
    }
    catch {
        Write-Error-Custom "Build failed: $_"
        exit 1
    }
    
    # Create deployment package
    Write-Info "Creating deployment package..."
    
    if (Test-Path $ZipFile) {
        Remove-Item $ZipFile
    }
    
    Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipFile
    Write-Success "Deployment package created: $ZipFile"
    
    # Show package size
    $FileSize = (Get-Item $ZipFile).Length / 1MB
    Write-Info "Package size: $([math]::Round($FileSize, 2)) MB"
}
else {
    Write-Warning-Custom "Skipping build (using existing package)"
    
    if (-not (Test-Path $ZipFile)) {
        Write-Error-Custom "No existing package found at: $ZipFile"
        exit 1
    }
}

# Deploy to Raspberry Pi
if (-not $SkipDeploy) {
    Write-Info "Deploying to Raspberry Pi ($PiUser@$PiHost)..."
    
    # Check if SSH is available
    try {
        $null = Get-Command ssh -ErrorAction Stop
    }
    catch {
        Write-Error-Custom "SSH not found. Please install OpenSSH client."
        Write-Info "Install with: Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0"
        exit 1
    }
    
    # Test connection
    Write-Info "Testing connection to Raspberry Pi..."
    try {
        $result = ssh -o BatchMode=yes -o ConnectTimeout=5 "$PiUser@$PiHost" "echo 'Connection successful'" 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning-Custom "SSH connection test failed. You may need to enter password."
        }
        else {
            Write-Success "Connection successful"
        }
    }
    catch {
        Write-Warning-Custom "Could not test connection: $_"
    }
    
    # Transfer deployment package
    Write-Info "Transferring deployment package..."
    try {
        scp $ZipFile "${PiUser}@${PiHost}:~/"
        Write-Success "Package transferred successfully"
    }
    catch {
        Write-Error-Custom "Failed to transfer package: $_"
        exit 1
    }
    
    # Check if deploy script exists on Pi
    Write-Info "Checking for deployment script on Raspberry Pi..."
    $deployScriptExists = ssh "$PiUser@$PiHost" "test -f ~/deploy.sh && echo 'exists' || echo 'missing'" 2>$null
    
    if ($deployScriptExists -ne "exists") {
        Write-Warning-Custom "Deployment script not found on Raspberry Pi"
        Write-Info "Transferring deployment script..."
        
        $deployScript = Join-Path $ScriptDir "deploy.sh"
        if (Test-Path $deployScript) {
            scp $deployScript "${PiUser}@${PiHost}:~/"
            ssh "$PiUser@$PiHost" "chmod +x ~/deploy.sh"
            Write-Success "Deployment script transferred"
        }
        else {
            Write-Error-Custom "Deploy script not found at: $deployScript"
            exit 1
        }
    }
    
    # Run deployment on Pi
    Write-Info "Running deployment on Raspberry Pi..."
    Write-Host ""
    Write-Host "======= Raspberry Pi Deployment Output =======" -ForegroundColor Yellow
    
    try {
        ssh -t "$PiUser@$PiHost" "cd ~ && ./deploy.sh"
        Write-Host "===============================================" -ForegroundColor Yellow
        Write-Host ""
        Write-Success "Deployment completed successfully!"
    }
    catch {
        Write-Host "===============================================" -ForegroundColor Yellow
        Write-Host ""
        Write-Error-Custom "Deployment failed: $_"
        exit 1
    }
    
    # Show application URL
    Write-Host ""
    Write-Info "Application should be running at: http://$PiHost:5000"
    Write-Info "Or: http://$(ssh "$PiUser@$PiHost" "hostname -I | awk '{print `$1}'"):5000"
    
    # Offer to view logs
    Write-Host ""
    $viewLogs = Read-Host "Do you want to view application logs? (y/n)"
    if ($viewLogs -eq 'y' -or $viewLogs -eq 'Y') {
        Write-Info "Viewing logs (press Ctrl+C to exit)..."
        ssh "$PiUser@$PiHost" "sudo journalctl -u restaurant-kiosk -f"
    }
}
else {
    Write-Warning-Custom "Skipping deployment (package created only)"
}

Write-Host ""
Write-Success "Script completed!"
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Cyan
Write-Host "  - SSH to Pi:        ssh $PiUser@$PiHost"
Write-Host "  - View logs:        ssh $PiUser@$PiHost 'sudo journalctl -u restaurant-kiosk -f'"
Write-Host "  - Restart app:      ssh $PiUser@$PiHost 'sudo systemctl restart restaurant-kiosk'"
Write-Host "  - Check status:     ssh $PiUser@$PiHost 'sudo systemctl status restaurant-kiosk'"
Write-Host ""

