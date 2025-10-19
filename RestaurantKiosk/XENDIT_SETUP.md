# Xendit Payment Integration Setup

This document provides instructions for setting up Xendit payment integration for the Restaurant Kiosk application.

## ✅ Integration Status

The Xendit payment integration has been successfully implemented and is ready for testing!

### Latest Updates:
- ✅ **Direct PaymentService Integration** - Checkout page now uses PaymentService directly instead of API calls
- ✅ **HttpClient URI Issue Fixed** - Resolved "invalid request URI" error
- ✅ **Simplified Architecture** - Removed unnecessary API controller layer for checkout flow

## Prerequisites

1. Xendit account (sign up at https://xendit.co)
2. Access to Xendit Dashboard
3. Valid API keys for sandbox environment

## Setup Instructions

### 1. Get Xendit API Keys

1. Log in to your Xendit Dashboard
2. Navigate to **Settings** > **API Keys**
3. Copy your **Secret Key** for sandbox environment (NOT the public key)
4. Copy your **Webhook Token** (if available)

**Important:** You need the **Secret API Key** (starts with `xnd_development_` or `xnd_production_`) for server-side API calls, not the public key.

### 2. Configure Application Settings

⚠️ **Action Required!** You need to update your `appsettings.json` with your **Secret API Key**:

```json
{
  "Xendit": {
    "ApiKey": "YOUR_SECRET_API_KEY_HERE",
    "WebhookToken": "bNN4aceNmP7W9Z6DF6K2Bu5vYnoDzk7TzchGeNlHew5OZUmk",
    "IsSandbox": true,
    "BaseUrl": "https://api.xendit.co"
  }
}
```

**Important:** 
- Replace `YOUR_SECRET_API_KEY_HERE` with your actual **Secret API Key** from Xendit Dashboard
- The Secret Key should start with `xnd_development_` (for sandbox) or `xnd_production_` (for live)
- Do NOT use the Public Key - it won't work for server-side API calls

### 3. Configure Callback URLs (Required for E-Wallet Payments)

**Important:** E-wallet payments (GCash/Maya) require callback URLs to be configured in your Xendit dashboard.

1. In Xendit Dashboard, go to **Settings** > **Callback URLs**
2. Add your callback URL: `https://yourdomain.com/api/callback/payment/callback`
3. For local development, you can use: `https://yourdomain.ngrok.io/api/callback/payment/callback` (using ngrok)
4. Save the configuration

**Note:** Without configured callback URLs, e-wallet payments will fail with "CALLBACK_URL_NOT_FOUND" error.

### 4. Configure Webhooks (Optional)

To receive real-time payment notifications:

1. In Xendit Dashboard, go to **Settings** > **Webhooks**
2. Add webhook URL: `https://yourdomain.com/api/payment/webhook`
3. Select events:
   - `invoice.paid`
   - `ewallet.charge.succeeded`
   - `ewallet.charge.failed`
4. Copy the webhook token and update `appsettings.json`

### 5. Test Payment Methods

#### GCash Testing
- Use test phone numbers provided by Xendit
- Phone numbers are automatically formatted to international format (+63 for Philippines)
- Test amounts: Use small amounts (e.g., 1 PHP)
- Test OTP: Use test OTP codes from Xendit documentation

#### Maya Testing
- Use test phone numbers provided by Xendit
- Phone numbers are automatically formatted to international format (+63 for Philippines)
- Test amounts: Use small amounts (e.g., 1 PHP)
- Test OTP: Use test OTP codes from Xendit documentation

#### Card Testing
- Use test card numbers provided by Xendit
- Test amounts: Use small amounts (e.g., 1 PHP)
- Test CVV: Use any 3-digit number

### 6. Supported Payment Methods

The integration supports:

1. **Cash** - Traditional cash payment (no Xendit integration)
2. **GCash** - E-wallet payment through Xendit
3. **Maya** - E-wallet payment through Xendit
4. **Card/Others** - Credit/Debit cards and other payment methods through Xendit Invoice

### 7. Payment Flow

1. Customer selects items and proceeds to checkout
2. Customer fills in personal information
3. Customer selects payment method
4. For digital payments:
   - System creates payment request with Xendit
   - Customer is redirected to Xendit payment page
   - Customer completes payment
   - Customer is redirected back to success/failure page
5. For cash payments:
   - Order is placed directly
   - Customer proceeds to counter for payment

### 8. Error Handling

The system includes comprehensive error handling:

- Payment creation failures
- Network connectivity issues
- Invalid payment data
- Webhook verification failures

### 9. Security Considerations

- API keys are stored in configuration files (use environment variables in production)
- Webhook verification is implemented
- All payment data is handled securely through Xendit
- No sensitive payment information is stored locally

### 10. Production Deployment

Before going live:

1. Replace sandbox API keys with production keys
2. Set `IsSandbox` to `false` in configuration
3. Update webhook URLs to production URLs
4. Test all payment methods thoroughly
5. Implement proper logging and monitoring
6. Set up proper error alerting

### 11. Troubleshooting

Common issues and solutions:

- **401 Unauthorized Error**: 
  - ✅ **Fixed!** Make sure you're using the **Secret API Key**, not the Public Key
  - The Secret Key should start with `xnd_development_` (sandbox) or `xnd_production_` (live)
  - Public keys (starting with `xnd_public_`) are only for client-side operations
- **400 API Validation Error (mobile_number)**: 
  - ✅ **Fixed!** Phone numbers are now automatically formatted to international format
  - The system converts local numbers (09123456789) to international format (+639123456789)
  - Xendit requires phone numbers in E.164 format (+63XXXXXXXXXX)
- **CALLBACK_URL_NOT_FOUND Error**: 
  - ⚠️ **Action Required!** Configure callback URLs in your Xendit Dashboard
  - Go to **Settings** > **Callback URLs** in Xendit Dashboard
  - Add your callback URL: `https://yourdomain.com/api/callback/payment/callback`
  - For local development, use ngrok: `https://yourdomain.ngrok.io/api/callback/payment/callback`
- **Payment creation fails**: Check API key and network connectivity
- **Webhook not received**: Verify webhook URL and token
- **Redirect issues**: Check success/failure URLs in payment creation
- **Test payments not working**: Verify test credentials and amounts

### 12. Support

For Xendit-specific issues:
- Xendit Documentation: https://docs.xendit.co
- Xendit Support: support@xendit.co

For application-specific issues:
- Check application logs
- Verify configuration settings
- Test with Xendit sandbox environment

## Testing Checklist

- [ ] API key configured correctly
- [ ] **Callback URLs configured in Xendit Dashboard** (Required for e-wallet payments)
- [ ] Webhook token configured (if using webhooks)
- [ ] GCash payments working
- [ ] Maya payments working
- [ ] Card payments working
- [ ] Success page redirects working
- [ ] Failure page redirects working
- [ ] Webhook notifications working (if configured)
- [ ] Error handling working properly
- [ ] Cash payments working (no Xendit integration)
