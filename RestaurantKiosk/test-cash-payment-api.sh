#!/bin/bash
# Test script for Cash Payment API endpoints
# Run this on your VPS to verify the API is accessible

echo "=================================="
echo "Cash Payment API Test Script"
echo "=================================="
echo ""

# Configuration
BASE_URL="${1:-http://localhost:5000}"
echo "Testing API at: $BASE_URL"
echo ""

# Test 1: Check if server is running
echo "Test 1: Server Health Check"
echo "----------------------------"
if curl -s -o /dev/null -w "%{http_code}" "$BASE_URL" | grep -q "200\|302"; then
    echo "✓ Server is running"
else
    echo "✗ Server is NOT responding"
    echo "  Make sure the app is running: sudo systemctl status restaurant-kiosk"
    exit 1
fi
echo ""

# Test 2: Initialize payment session
echo "Test 2: Initialize Payment Session"
echo "-----------------------------------"
RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" \
  -X POST "$BASE_URL/api/cash-payment/init" \
  -H "Content-Type: application/json" \
  -d '{"orderNumber":"TEST-001","totalAmount":100.00}')

HTTP_STATUS=$(echo "$RESPONSE" | grep "HTTP_STATUS" | cut -d: -f2)
BODY=$(echo "$RESPONSE" | sed '/HTTP_STATUS/d')

echo "Status Code: $HTTP_STATUS"
echo "Response Body: $BODY"

if [ "$HTTP_STATUS" == "200" ]; then
    echo "✓ Initialize endpoint working"
elif [ "$HTTP_STATUS" == "404" ]; then
    echo "✗ 404 Not Found - Controller not mapped or route incorrect"
    echo "  Check Program.cs has: app.MapControllers();"
    echo "  Check controller has: [Route(\"api/cash-payment\")]"
elif [ "$HTTP_STATUS" == "401" ]; then
    echo "✗ 401 Unauthorized - API key required"
    echo "  Set CashPayment:ApiKey to null in appsettings.json for testing"
else
    echo "✗ Unexpected status code: $HTTP_STATUS"
fi
echo ""

# Test 3: Check active sessions
echo "Test 3: Get Active Sessions"
echo "----------------------------"
RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" \
  -X GET "$BASE_URL/api/cash-payment/active-sessions")

HTTP_STATUS=$(echo "$RESPONSE" | grep "HTTP_STATUS" | cut -d: -f2)
BODY=$(echo "$RESPONSE" | sed '/HTTP_STATUS/d')

echo "Status Code: $HTTP_STATUS"
echo "Response Body: $BODY"

if [ "$HTTP_STATUS" == "200" ]; then
    echo "✓ Active sessions endpoint working"
    # Parse session count
    COUNT=$(echo "$BODY" | grep -o '"count":[0-9]*' | cut -d: -f2)
    echo "  Active sessions: ${COUNT:-0}"
elif [ "$HTTP_STATUS" == "404" ]; then
    echo "✗ 404 Not Found"
else
    echo "✗ Unexpected status code: $HTTP_STATUS"
fi
echo ""

# Test 4: Check status endpoint
echo "Test 4: Get Payment Status"
echo "---------------------------"
RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" \
  -X GET "$BASE_URL/api/cash-payment/status/TEST-001")

HTTP_STATUS=$(echo "$RESPONSE" | grep "HTTP_STATUS" | cut -d: -f2)
BODY=$(echo "$RESPONSE" | sed '/HTTP_STATUS/d')

echo "Status Code: $HTTP_STATUS"
echo "Response Body: $BODY"

if [ "$HTTP_STATUS" == "200" ]; then
    echo "✓ Status endpoint working"
elif [ "$HTTP_STATUS" == "404" ]; then
    echo "⚠ 404 Not Found - This is OK if no session exists for TEST-001"
else
    echo "✗ Unexpected status code: $HTTP_STATUS"
fi
echo ""

# Summary
echo "=================================="
echo "Summary"
echo "=================================="
echo ""
echo "If you see 404 errors on all endpoints:"
echo "  1. Check if controllers are registered in Program.cs"
echo "  2. Run: grep -n 'MapControllers' RestaurantKiosk/Program.cs"
echo "  3. Check if app is published correctly: ls -la /var/www/kiosk/"
echo "  4. Check nginx proxy config if using reverse proxy"
echo ""
echo "If you see 404 only on browser but curl works:"
echo "  1. Check HttpClient BaseAddress in CashPayment.razor"
echo "  2. Check for CORS issues in browser console"
echo ""
echo "Next steps:"
echo "  - Check application logs: sudo journalctl -u restaurant-kiosk -n 50"
echo "  - Try with API key if configured"
echo "  - Test from browser console: fetch('$BASE_URL/api/cash-payment/active-sessions').then(r=>r.json()).then(console.log)"

