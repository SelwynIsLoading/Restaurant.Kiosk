#!/bin/bash
# Log viewer for Kiosk Peripherals
# Usage: ./check_kiosk_logs.sh [option]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_FILE="$SCRIPT_DIR/kiosk_peripherals.log"
SERVICE_NAME="kiosk-peripherals.service"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

show_help() {
    echo "=================================="
    echo "Kiosk Peripherals - Log Viewer"
    echo "=================================="
    echo ""
    echo "Usage: ./check_kiosk_logs.sh [option]"
    echo ""
    echo "Options:"
    echo "  tail, -f, --follow       Follow logs in real-time (default)"
    echo "  all, --all               Show all logs"
    echo "  errors, --errors         Show only errors"
    echo "  cash, --cash             Show only cash reader logs"
    echo "  printer, --printer       Show only printer logs"
    echo "  today, --today           Show today's logs"
    echo "  last <n>, --last <n>     Show last N lines (default 50)"
    echo "  systemd, --systemd       Show systemd service logs"
    echo "  status, --status         Show service status"
    echo "  summary, --summary       Show log statistics"
    echo "  clear, --clear           Clear log file"
    echo "  help, -h, --help         Show this help"
    echo ""
    echo "Examples:"
    echo "  ./check_kiosk_logs.sh                # Follow logs (tail -f)"
    echo "  ./check_kiosk_logs.sh errors         # Show only errors"
    echo "  ./check_kiosk_logs.sh cash           # Show cash reader logs"
    echo "  ./check_kiosk_logs.sh last 100       # Show last 100 lines"
    echo "  ./check_kiosk_logs.sh systemd        # Show systemd logs"
}

check_log_file() {
    if [ ! -f "$LOG_FILE" ]; then
        echo -e "${RED}Log file not found: $LOG_FILE${NC}"
        echo ""
        echo "The log file will be created when kiosk_peripherals.py runs."
        echo ""
        echo "To start the script:"
        echo "  python3 kiosk_peripherals.py"
        echo ""
        echo "Or check if running as service:"
        echo "  sudo systemctl status $SERVICE_NAME"
        exit 1
    fi
}

show_tail() {
    check_log_file
    echo -e "${GREEN}Following logs in real-time...${NC}"
    echo -e "${YELLOW}Press Ctrl+C to stop${NC}"
    echo ""
    tail -f "$LOG_FILE"
}

show_all() {
    check_log_file
    less +G "$LOG_FILE"
}

show_errors() {
    check_log_file
    echo -e "${RED}=== ERROR LOGS ===${NC}"
    grep -i "ERROR\|CRITICAL\|FAIL" "$LOG_FILE" | tail -50
}

show_cash() {
    check_log_file
    echo -e "${GREEN}=== CASH READER LOGS ===${NC}"
    grep "\[CASH\]" "$LOG_FILE" | tail -50
}

show_printer() {
    check_log_file
    echo -e "${BLUE}=== PRINTER LOGS ===${NC}"
    grep "\[PRINTER\]" "$LOG_FILE" | tail -50
}

show_today() {
    check_log_file
    TODAY=$(date +%Y-%m-%d)
    echo -e "${GREEN}=== TODAY'S LOGS ($TODAY) ===${NC}"
    grep "$TODAY" "$LOG_FILE" | less +G
}

show_last() {
    check_log_file
    LINES=${1:-50}
    echo -e "${GREEN}=== LAST $LINES LINES ===${NC}"
    tail -n "$LINES" "$LOG_FILE"
}

show_systemd() {
    echo -e "${GREEN}=== SYSTEMD SERVICE LOGS ===${NC}"
    echo -e "${YELLOW}Following systemd logs... Press Ctrl+C to stop${NC}"
    echo ""
    sudo journalctl -u "$SERVICE_NAME" -f
}

show_status() {
    echo -e "${GREEN}=== SERVICE STATUS ===${NC}"
    echo ""
    
    # Check if running as service
    if systemctl is-active --quiet "$SERVICE_NAME" 2>/dev/null; then
        echo -e "${GREEN}✓ Service is running${NC}"
        sudo systemctl status "$SERVICE_NAME" --no-pager
    else
        echo -e "${YELLOW}⚠ Service is not running (or not installed as service)${NC}"
        echo ""
        echo "Check if running manually:"
        ps aux | grep kiosk_peripherals | grep -v grep || echo "  Not running"
        echo ""
        echo "To start the service:"
        echo "  sudo systemctl start $SERVICE_NAME"
    fi
    
    echo ""
    echo -e "${GREEN}=== LOG FILE INFO ===${NC}"
    if [ -f "$LOG_FILE" ]; then
        echo "Location: $LOG_FILE"
        echo "Size: $(du -h "$LOG_FILE" | cut -f1)"
        echo "Lines: $(wc -l < "$LOG_FILE")"
        echo "Last modified: $(stat -c %y "$LOG_FILE" 2>/dev/null || stat -f '%Sm' "$LOG_FILE" 2>/dev/null)"
        echo ""
        echo "Backup logs:"
        ls -lh kiosk_peripherals.log.* 2>/dev/null || echo "  None"
    else
        echo -e "${YELLOW}Log file does not exist yet${NC}"
    fi
}

show_summary() {
    check_log_file
    echo -e "${GREEN}=== LOG SUMMARY ===${NC}"
    echo ""
    
    TOTAL_LINES=$(wc -l < "$LOG_FILE")
    ERROR_COUNT=$(grep -c "ERROR" "$LOG_FILE" 2>/dev/null || echo 0)
    WARN_COUNT=$(grep -c "WARNING\|WARN" "$LOG_FILE" 2>/dev/null || echo 0)
    CASH_LINES=$(grep -c "\[CASH\]" "$LOG_FILE" 2>/dev/null || echo 0)
    PRINTER_LINES=$(grep -c "\[PRINTER\]" "$LOG_FILE" 2>/dev/null || echo 0)
    
    # Count specific events
    BILLS_INSERTED=$(grep -c "Bill inserted" "$LOG_FILE" 2>/dev/null || echo 0)
    COINS_INSERTED=$(grep -c "Coin inserted" "$LOG_FILE" 2>/dev/null || echo 0)
    PAYMENTS_COMPLETED=$(grep -c "Payment completed" "$LOG_FILE" 2>/dev/null || echo 0)
    RECEIPTS_PRINTED=$(grep -c "Receipt printed successfully" "$LOG_FILE" 2>/dev/null || echo 0)
    
    echo "Total lines: $TOTAL_LINES"
    echo "Errors: $ERROR_COUNT"
    echo "Warnings: $WARN_COUNT"
    echo ""
    echo "Module Activity:"
    echo "  Cash reader logs: $CASH_LINES"
    echo "  Printer logs: $PRINTER_LINES"
    echo ""
    echo "Operations:"
    echo "  Bills inserted: $BILLS_INSERTED"
    echo "  Coins inserted: $COINS_INSERTED"
    echo "  Payments completed: $PAYMENTS_COMPLETED"
    echo "  Receipts printed: $RECEIPTS_PRINTED"
    echo ""
    
    if [ "$ERROR_COUNT" -gt 0 ]; then
        echo -e "${RED}Recent errors:${NC}"
        grep "ERROR" "$LOG_FILE" | tail -5
    fi
}

clear_logs() {
    if [ ! -f "$LOG_FILE" ]; then
        echo -e "${YELLOW}No log file to clear${NC}"
        exit 0
    fi
    
    echo -e "${YELLOW}Are you sure you want to clear the log file?${NC}"
    echo "Current size: $(du -h "$LOG_FILE" | cut -f1)"
    read -p "Type 'yes' to confirm: " -r
    echo
    
    if [[ $REPLY == "yes" ]]; then
        > "$LOG_FILE"
        echo -e "${GREEN}✓ Log file cleared${NC}"
    else
        echo -e "${YELLOW}Cancelled${NC}"
    fi
}

# Main script
case "${1:-tail}" in
    tail|-f|--follow)
        show_tail
        ;;
    all|--all)
        show_all
        ;;
    errors|--errors)
        show_errors
        ;;
    cash|--cash)
        show_cash
        ;;
    printer|--printer)
        show_printer
        ;;
    today|--today)
        show_today
        ;;
    last|--last)
        show_last "${2:-50}"
        ;;
    systemd|--systemd)
        show_systemd
        ;;
    status|--status)
        show_status
        ;;
    summary|--summary)
        show_summary
        ;;
    clear|--clear)
        clear_logs
        ;;
    help|-h|--help)
        show_help
        ;;
    *)
        echo -e "${RED}Unknown option: $1${NC}"
        echo ""
        show_help
        exit 1
        ;;
esac

