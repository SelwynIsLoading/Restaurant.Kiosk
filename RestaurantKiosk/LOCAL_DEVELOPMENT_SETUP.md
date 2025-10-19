# Local Development Setup for Xendit Integration

This guide helps you set up local development with Xendit e-wallet payments using ngrok.

## Why ngrok is needed?

Xendit e-wallet payments (GCash/Maya) require callback URLs to be accessible from the internet. Since your local development server (localhost) is not accessible from the internet, you need to use ngrok to create a public tunnel.

## Setup Instructions

### 1. Install ngrok

1. Go to https://ngrok.com/
2. Sign up for a free account
3. Download ngrok for your operating system
4. Extract and add ngrok to your PATH

### 2. Get your ngrok authtoken

1. Log in to your ngrok dashboard
2. Go to **Getting Started** > **Your Authtoken**
3. Copy your authtoken

### 3. Configure ngrok

```bash
ngrok config add-authtoken YOUR_AUTHTOKEN_HERE
```

### 4. Start your application

```bash
cd RestaurantKiosk
dotnet run
```

Your application will start on `https://localhost:5001` (or similar).

### 5. Create ngrok tunnel

In a new terminal window:

```bash
ngrok http 5001
```

ngrok will display something like:
```
Session Status                online
Account                       your-email@example.com
Version                       3.x.x
Region                        United States (us)
Latency                       -
Web Interface                 http://127.0.0.1:4040
Forwarding                    https://abc123.ngrok.io -> http://localhost:5001
```

### 6. Configure Xendit Dashboard

1. Go to your Xendit Dashboard
2. Navigate to **Settings** > **Callback URLs**
3. Add your ngrok URL: `https://abc123.ngrok.io/api/callback/payment/callback`
4. Save the configuration

### 7. Test the integration

1. Open your application using the ngrok URL: `https://abc123.ngrok.io`
2. Add items to cart and proceed to checkout
3. Select GCash or Maya payment method
4. Complete the payment flow

## Important Notes

- **ngrok URL changes**: Free ngrok URLs change every time you restart ngrok. You'll need to update the callback URL in Xendit Dashboard each time.
- **ngrok Pro**: Consider upgrading to ngrok Pro for static domains if you're doing frequent development.
- **Security**: Never commit ngrok URLs or authtokens to version control.

## Alternative Solutions

- **ngrok Pro**: Provides static domains that don't change
- **Cloud deployment**: Deploy to a cloud service for testing
- **Local tunnel services**: Other services like localtunnel.me

## Troubleshooting

- **Callback URL not found**: Make sure you've added the ngrok URL to Xendit Dashboard
- **ngrok not working**: Check if your authtoken is configured correctly
- **URL changed**: Update the callback URL in Xendit Dashboard when ngrok restarts
