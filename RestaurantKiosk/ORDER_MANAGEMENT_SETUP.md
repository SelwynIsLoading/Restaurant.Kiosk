# Order Management System - Real-Time Kitchen Display

## Overview

This document describes the real-time order management system that allows kitchen staff to view and manage orders as they come in from customer payments.

## Features

### 1. Real-Time Order Updates
- Orders automatically appear on the kitchen display when payment is successful
- No page refresh required - uses SignalR for real-time communication
- Orders are automatically removed when marked as complete

### 2. Order Management Page (`/admin/orders`)
- **Authorization Required**: Only accessible to authenticated admin users
- **Real-Time Statistics**:
  - Active Orders count
  - Total Items count
  - Revenue from active orders
  - Connection status indicator

### 3. Order Cards Display
- Each order is displayed in a card with:
  - Order number and timestamp
  - Order age indicator (e.g., "5m ago", "2h ago")
  - Customer details (name, phone, email)
  - Complete list of ordered items with quantities and prices
  - Special instructions/notes if provided
  - Order summary with subtotal, tax, service charge, and total
  - Payment method used
  - "Mark as Completed" button

### 4. Order Completion
- Kitchen staff can mark orders as complete by clicking the button
- Completed orders are automatically removed from all connected displays
- Order status is updated to "Completed" in the database
- CompletedAt timestamp is recorded

## Technical Implementation

### SignalR Hub
**File**: `RestaurantKiosk/Hubs/OrderHub.cs`

The OrderHub handles real-time communication between the server and clients:
- `NewOrder` event: Broadcast when a new order is paid
- `OrderCompleted` event: Broadcast when an order is marked complete

### Order Repository Enhancements
**Files**: 
- `RestaurantKiosk/Data/Services/IOrderRepository.cs`
- `RestaurantKiosk/Data/Services/OrderRepository.cs`

New methods added:
- `GetActiveOrdersAsync()`: Retrieves all orders with status = Paid
- `MarkOrderAsCompletedAsync(int orderId)`: Updates order status to Completed

### Payment Controller Integration
**File**: `RestaurantKiosk/Controllers/PaymentController.cs`

When a payment is successful (via webhook):
1. Order status is updated to "Paid"
2. Product quantities are decreased
3. SignalR notification is sent to all connected clients
4. Order appears on kitchen displays automatically

### Order Management UI
**File**: `RestaurantKiosk/Components/Pages/AdminConsole/OrderManagement.razor`

Features:
- Responsive card-based layout
- Real-time connection status monitoring
- Automatic reconnection handling
- Visual feedback during order completion
- Smooth animations and transitions

## How It Works

### Workflow

1. **Customer Places Order**:
   - Customer completes payment through checkout
   - Payment webhook is received by the server

2. **Payment Processing**:
   - PaymentController updates order status to "Paid"
   - Product quantities are decreased
   - SignalR broadcasts "NewOrder" event

3. **Kitchen Display**:
   - OrderManagement page receives real-time notification
   - New order card appears automatically (no refresh needed)
   - Order is added to the active orders list

4. **Order Completion**:
   - Kitchen staff clicks "Mark as Completed"
   - Order status updates to "Completed" in database
   - SignalR broadcasts "OrderCompleted" event
   - Order card is removed from all connected displays

### SignalR Events

#### NewOrder Event
```csharp
await _orderHubContext.Clients.All.SendAsync("NewOrder", order.Id, order.OrderNumber);
```
Triggered when: A payment is successfully processed

#### OrderCompleted Event
```csharp
await hubConnection.SendAsync("NotifyOrderCompleted", orderId, order.OrderNumber);
```
Triggered when: An order is marked as completed by kitchen staff

## Configuration

### SignalR Setup in Program.cs
```csharp
// Add SignalR service
builder.Services.AddSignalR();

// Map SignalR hub
app.MapHub<OrderHub>("/orderhub");
```

### NuGet Package Required
- `Microsoft.AspNetCore.SignalR.Client` (version 9.0.9)

## Access Control

The Order Management page is protected with the `[Authorize]` attribute, ensuring only authenticated users can access it.

## Navigation

Access the Order Management page through:
- Admin Console (`/admin`) → Click "Manage Orders" button
- Direct URL: `/admin/orders`

## Browser Compatibility

The real-time features work with any modern browser that supports WebSockets:
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)

## Connection Status

The page displays real-time connection status:
- **Connected** (Green): Successfully connected to SignalR hub
- **Connecting...** (Yellow): Initial connection in progress
- **Reconnecting...** (Yellow): Attempting to reconnect after disconnection
- **Disconnected** (Red): Connection lost
- **Error** (Red): Connection error occurred

SignalR automatically attempts to reconnect if the connection is lost.

## Future Enhancements

Potential improvements for future development:
- Sound/visual notifications for new orders
- Order preparation time tracking
- Order priority management
- Print order tickets
- Order history and analytics
- Multiple kitchen station support
- Mobile app for order management

## Troubleshooting

### Orders Not Appearing
1. Check SignalR connection status on the page
2. Verify the order status in the database (should be "Paid")
3. Check browser console for errors
4. Ensure firewall/proxy allows WebSocket connections

### Connection Issues
- SignalR uses WebSockets by default, falls back to Server-Sent Events or Long Polling
- Check that the `/orderhub` endpoint is accessible
- Verify that the application is running and accessible

### Orders Not Removing After Completion
- Check database to verify order status was updated to "Completed"
- Verify SignalR connection is active
- Check browser console for JavaScript errors

