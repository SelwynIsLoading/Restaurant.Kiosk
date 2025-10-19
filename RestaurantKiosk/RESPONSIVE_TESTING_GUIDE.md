# Responsive Design Testing Guide

## Quick Start Testing

### 1. Browser DevTools Testing (Desktop)

#### Chrome/Edge DevTools
1. Open the application in Chrome/Edge
2. Press `F12` to open DevTools
3. Click the device toolbar icon (`Ctrl+Shift+M` or `Cmd+Shift+M`)
4. Test these preset devices:
   - iPhone SE (375×667)
   - iPhone 12 Pro (390×844)
   - iPad (768×1024)
   - iPad Pro (1024×1366)
   - Raspberry Pi 7" (800×480) - Custom
   - Raspberry Pi 10.1" (1280×800) - Custom

#### Adding Custom Devices
1. In DevTools, click "Edit..." in device dropdown
2. Add Raspberry Pi devices:
   ```
   Name: Raspberry Pi 7"
   Width: 800
   Height: 480
   Device pixel ratio: 1
   User agent: Mozilla/5.0 (X11; Linux armv7l) AppleWebKit/537.36 (KHTML, like Gecko) Chromium/92.0.4515.159 Chrome/92.0.4515.159 Safari/537.36
   ```
   
   ```
   Name: Raspberry Pi 10.1"
   Width: 1280
   Height: 800
   Device pixel ratio: 1
   User agent: Mozilla/5.0 (X11; Linux armv7l) AppleWebKit/537.36 (KHTML, like Gecko) Chromium/92.0.4515.159 Chrome/92.0.4515.159 Safari/537.36
   ```

### 2. Testing on Raspberry Pi

#### Setup Chromium on Raspberry Pi
```bash
# Update system
sudo apt-get update
sudo apt-get upgrade -y

# Install Chromium if not already installed
sudo apt-get install chromium-browser -y

# Open the kiosk application
chromium-browser --start-fullscreen --kiosk http://your-app-url
```

#### Enable Touch Support
```bash
# Check if touch is detected
evtest

# Install touch calibration tool if needed
sudo apt-get install xinput-calibrator
```

### 3. Testing Checklist

#### Visual Testing

##### Home Page
- [ ] Hero section displays correctly
- [ ] Logo scales appropriately
- [ ] CTA buttons are large and tappable
- [ ] Feature cards stack properly on mobile
- [ ] "How It Works" section is readable
- [ ] Images don't overflow containers
- [ ] Spacing looks balanced

##### Kiosk/Menu Page
- [ ] Header with logo and cart button is visible
- [ ] Header is responsive and doesn't overlap
- [ ] Category chips wrap correctly
- [ ] Product grid shows appropriate columns:
  - Desktop: 4 columns
  - Tablet: 3 columns  
  - Mobile: 2 columns
  - Small mobile: 1 column
- [ ] Product cards display all information
- [ ] Product images scale correctly
- [ ] "Add to Cart" buttons are easy to tap
- [ ] Shopping cart drawer opens smoothly
- [ ] Cart items display with proper formatting
- [ ] Quantity controls are easy to use
- [ ] Total price is clearly visible

##### Checkout Page
- [ ] Header displays correctly
- [ ] Order summary is readable
- [ ] Form fields are properly sized
- [ ] Payment method cards are tappable (2×2 grid on mobile)
- [ ] Submit button is prominent and easy to tap
- [ ] Order items list is scrollable
- [ ] Layout stacks correctly on mobile
- [ ] All text is readable without zooming

#### Interaction Testing

##### Touch Interactions
- [ ] Tap feedback works (visual scale animation)
- [ ] Haptic feedback works (if device supports)
- [ ] Long press feedback works
- [ ] No double-tap zoom
- [ ] No accidental context menus
- [ ] Smooth scrolling
- [ ] Drawer swipes open/closed smoothly

##### Forms
- [ ] Inputs don't trigger zoom on focus
- [ ] Keyboard doesn't cover input fields
- [ ] Form validation displays correctly
- [ ] Submit button accessible when keyboard open
- [ ] Can navigate between fields easily

##### Navigation
- [ ] Can tap back button easily
- [ ] Transitions are smooth
- [ ] No janky animations
- [ ] Page doesn't jump on load

#### Performance Testing

##### Load Time
- [ ] First paint < 1.5s
- [ ] Interactive < 3s
- [ ] Images load progressively
- [ ] No layout shift during load

##### Scrolling
- [ ] Smooth 60fps scrolling
- [ ] No jank when scrolling product list
- [ ] Cart drawer scrolls smoothly
- [ ] Checkout page scrolls without lag

##### Responsiveness
- [ ] Button presses feel instant
- [ ] Navigation transitions smoothly
- [ ] No lag when opening drawer
- [ ] Form inputs respond immediately

#### Browser Compatibility

##### Desktop Browsers
- [ ] Chrome (latest)
- [ ] Firefox (latest)
- [ ] Edge (latest)
- [ ] Safari (latest)

##### Mobile Browsers
- [ ] Chrome Mobile (Android)
- [ ] Samsung Internet
- [ ] Safari (iOS)
- [ ] Firefox Mobile

##### Raspberry Pi
- [ ] Chromium (Raspberry Pi OS)
- [ ] Full-screen/kiosk mode works

### 4. Common Issues and Solutions

#### Issue: Text is too small
**Test**: Check if font-size is below 14px on mobile
**Fix**: Verify media queries in `app.css`

#### Issue: Buttons are hard to tap
**Test**: Measure button size (should be ≥44px)
**Fix**: Check `.mud-button-root` styles in `app.css`

#### Issue: Horizontal scrollbar appears
**Test**: Scroll horizontally, check for overflow
**Fix**: Add `max-width: 100%` to oversized elements

#### Issue: Layout breaks on rotation
**Test**: Rotate device or change orientation in DevTools
**Fix**: Verify orientation handler in `kiosk-touch.js`

#### Issue: Images don't scale
**Test**: Resize browser, check if images overflow
**Fix**: Ensure `max-width: 100%` on images

#### Issue: Inputs trigger zoom on iOS
**Test**: Tap input field on iPhone
**Fix**: Verify font-size is ≥16px

### 5. Automated Testing Script

Create a file `test-responsive.js`:

```javascript
// Run in browser console
(function() {
    const breakpoints = [
        { name: 'Small Mobile', width: 375 },
        { name: 'Mobile', width: 480 },
        { name: 'Tablet', width: 768 },
        { name: 'Desktop', width: 1024 },
        { name: 'Large Desktop', width: 1440 }
    ];
    
    console.log('🧪 Starting Responsive Design Tests...\n');
    
    breakpoints.forEach(bp => {
        console.log(`📱 Testing ${bp.name} (${bp.width}px)`);
        window.resizeTo(bp.width, 900);
        
        // Check touch targets
        const buttons = document.querySelectorAll('button, .mud-button');
        const smallButtons = Array.from(buttons).filter(btn => {
            const rect = btn.getBoundingClientRect();
            return rect.width < 44 || rect.height < 44;
        });
        
        if (smallButtons.length > 0) {
            console.warn(`⚠️  Found ${smallButtons.length} buttons smaller than 44px`);
        } else {
            console.log('✅ All buttons meet touch target size');
        }
        
        // Check for horizontal scroll
        if (document.documentElement.scrollWidth > window.innerWidth) {
            console.warn('⚠️  Horizontal scroll detected');
        } else {
            console.log('✅ No horizontal scroll');
        }
        
        console.log('');
    });
    
    console.log('✅ Tests complete!');
})();
```

### 6. Visual Regression Testing

#### Using Browser Extensions
1. Install "Full Page Screen Capture" extension
2. Take screenshots at each breakpoint
3. Compare against baseline screenshots

#### Manual Comparison
1. Home page at 375px, 768px, 1024px
2. Kiosk page at 375px, 768px, 1024px
3. Checkout page at 375px, 768px, 1024px

### 7. Accessibility Testing

#### Keyboard Navigation
- [ ] Can tab through all interactive elements
- [ ] Focus indicators are visible
- [ ] Can operate without mouse/touch
- [ ] Modal/drawer traps focus correctly

#### Screen Reader Testing
- [ ] Images have alt text
- [ ] Buttons have descriptive labels
- [ ] Form fields have labels
- [ ] Landmarks are properly marked

#### Color Contrast
- [ ] Text meets 4.5:1 contrast ratio
- [ ] Interactive elements are distinguishable
- [ ] Works in high contrast mode

### 8. Real Device Testing

#### Priority Devices
1. **Raspberry Pi 7" Touchscreen**
   - Resolution: 800×480
   - Test in kiosk mode
   - Verify touch works
   
2. **Raspberry Pi 10.1" Touchscreen**
   - Resolution: 1280×800
   - Test in full screen
   - Verify performance

3. **iPhone SE or similar**
   - Small screen edge case
   - Test in Safari
   
4. **iPad**
   - Tablet layout
   - Test in both orientations

5. **Android Phone**
   - Test in Chrome
   - Test in Samsung Internet

#### Testing Procedure for Each Device
1. Open application
2. Navigate through all pages
3. Complete a full order flow:
   - Browse products
   - Add items to cart
   - Modify quantities
   - Proceed to checkout
   - Fill form
   - Select payment method
4. Rotate device (portrait/landscape)
5. Test with different browser zoom levels
6. Clear cache and reload

### 9. Performance Testing Tools

#### Lighthouse (Chrome DevTools)
1. Open DevTools
2. Go to "Lighthouse" tab
3. Select "Mobile" device
4. Run audit
5. Aim for scores:
   - Performance: >90
   - Accessibility: 100
   - Best Practices: >90

#### WebPageTest
1. Go to webpagetest.org
2. Enter your URL
3. Select "Mobile 3G - Slow" connection
4. Choose location closest to deployment
5. Run test
6. Check metrics:
   - First Contentful Paint < 1.5s
   - Speed Index < 3s
   - Time to Interactive < 3.5s

### 10. Sign-off Checklist

Before deploying to production:

#### Functionality
- [ ] All pages load correctly
- [ ] All features work on mobile
- [ ] Forms submit successfully
- [ ] Payment flows work
- [ ] Cart persists correctly
- [ ] Navigation works smoothly

#### Performance
- [ ] Lighthouse score >90
- [ ] No console errors
- [ ] No 404 errors
- [ ] Images optimized
- [ ] Smooth animations

#### Responsive Design
- [ ] Works on all breakpoints
- [ ] No horizontal scroll
- [ ] Touch targets ≥44px
- [ ] Text is readable
- [ ] Images scale properly

#### Accessibility
- [ ] Keyboard navigation works
- [ ] Focus indicators visible
- [ ] Screen reader compatible
- [ ] Color contrast passes
- [ ] Alt text present

#### Browser Support
- [ ] Chrome/Chromium ✓
- [ ] Firefox ✓
- [ ] Safari ✓
- [ ] Edge ✓
- [ ] Mobile browsers ✓

#### Device Testing
- [ ] Raspberry Pi 7" ✓
- [ ] Raspberry Pi 10.1" ✓
- [ ] iPhone ✓
- [ ] Android phone ✓
- [ ] iPad ✓

## Continuous Testing

### Regression Testing
Run tests after any UI changes:
1. Verify all breakpoints still work
2. Check touch targets haven't shrunk
3. Confirm no new horizontal scroll
4. Test on real devices

### Monitoring
Set up monitoring for:
- Page load times
- Error rates by device type
- User drop-off points
- Touch interaction success rates

## Resources

### Tools
- Chrome DevTools
- Firefox Responsive Design Mode
- BrowserStack (for cross-browser testing)
- Lighthouse
- WebPageTest
- WAVE (accessibility checker)

### Documentation
- `RESPONSIVE_DESIGN_SUMMARY.md` - Implementation details
- MDN Web Docs - Responsive design guide
- WCAG 2.1 Guidelines - Accessibility standards

### Support
- Check browser console for debug logs
- Review `kiosk-touch.js` for touch issues
- Inspect CSS in DevTools for layout issues
- Test with different user agents

---

## Quick Test Commands

```bash
# Test on different screen sizes quickly
# Run these in browser console

# Small Mobile (375px)
window.resizeTo(375, 667)

# Mobile (480px)  
window.resizeTo(480, 800)

# Tablet (768px)
window.resizeTo(768, 1024)

# Desktop (1024px)
window.resizeTo(1024, 768)

# Raspberry Pi 7"
window.resizeTo(800, 480)

# Raspberry Pi 10.1"
window.resizeTo(1280, 800)
```

**Happy Testing! 🧪**

