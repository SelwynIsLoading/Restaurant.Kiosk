// Kiosk Touchscreen Enhancements
(function() {
    'use strict';

    // Haptic feedback for supported devices
    function hapticFeedback(duration = 50) {
        if ('vibrate' in navigator) {
            navigator.vibrate(duration);
        }
    }

    // Enhanced touch feedback for buttons
    function addTouchFeedback() {
        const buttonSelectors = [
            'button',
            '.mud-button',
            '.mud-button-root',
            '.mud-icon-button',
            '.mud-fab',
            '.mud-chip',
            '.mud-card',
            '.payment-method-card',
            '.category-btn',
            '.add-to-cart-btn',
            '.cart-button',
            '.payment-btn',
            '.submit-order-btn',
            '.checkout-btn',
            'a[role="button"]',
            '[role="button"]'
        ];
        
        const buttons = document.querySelectorAll(buttonSelectors.join(', '));
        
        buttons.forEach(button => {
            // Skip if already enhanced
            if (button.dataset.touchEnhanced) return;
            button.dataset.touchEnhanced = 'true';
            
            let touchStartTime;
            
            button.addEventListener('touchstart', function(e) {
                touchStartTime = Date.now();
                hapticFeedback(30);
                
                // Add active state class
                this.classList.add('touch-active');
                
                // Visual feedback
                const originalTransform = this.style.transform || '';
                this.style.transform = originalTransform ? `${originalTransform} scale(0.95)` : 'scale(0.95)';
                this.style.transition = 'transform 0.1s cubic-bezier(0.4, 0, 0.2, 1)';
            }, { passive: true });
            
            button.addEventListener('touchend', function(e) {
                const touchDuration = Date.now() - touchStartTime;
                
                // Remove active state class
                this.classList.remove('touch-active');
                
                // Restore original state
                setTimeout(() => {
                    this.style.transform = '';
                }, 100);
                
                // Long press feedback
                if (touchDuration > 500) {
                    hapticFeedback(100);
                }
            }, { passive: true });
            
            button.addEventListener('touchcancel', function(e) {
                this.classList.remove('touch-active');
                this.style.transform = '';
            }, { passive: true });
        });
    }
    
    // Observer to handle dynamically added buttons
    function observeDynamicButtons() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.addedNodes.length) {
                    addTouchFeedback();
                }
            });
        });
        
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    // Prevent zoom on double tap (iOS Safari)
    function preventZoom() {
        let lastTouchEnd = 0;
        document.addEventListener('touchend', function(event) {
            const now = (new Date()).getTime();
            if (now - lastTouchEnd <= 300) {
                event.preventDefault();
            }
            lastTouchEnd = now;
        }, { passive: false });
        
        // Prevent pinch zoom
        document.addEventListener('gesturestart', function(e) {
            e.preventDefault();
        }, { passive: false });
        
        document.addEventListener('gesturechange', function(e) {
            e.preventDefault();
        }, { passive: false });
        
        document.addEventListener('gestureend', function(e) {
            e.preventDefault();
        }, { passive: false });
    }

    // Prevent context menu on long press
    function preventContextMenu() {
        document.addEventListener('contextmenu', function(e) {
            e.preventDefault();
        });
    }

    // Auto-hide cursor for kiosk mode
    function hideCursor() {
        let cursorTimer;
        document.addEventListener('mousemove', function() {
            document.body.style.cursor = 'default';
            clearTimeout(cursorTimer);
            cursorTimer = setTimeout(function() {
                document.body.style.cursor = 'none';
            }, 3000);
        });
    }

    // Enhanced scroll behavior for touch
    function enhanceScroll() {
        // Enable smooth scrolling
        document.documentElement.style.scrollBehavior = 'smooth';
        
        // Improve momentum scrolling on iOS
        document.body.style.webkitOverflowScrolling = 'touch';
        
        // Passive event listeners for better performance
        document.addEventListener('touchstart', function() {}, {passive: true});
        document.addEventListener('touchmove', function() {}, {passive: true});
        
        // Add scroll position restoration
        if ('scrollRestoration' in history) {
            history.scrollRestoration = 'manual';
        }
    }
    
    // Improve click delay on mobile
    function removeClickDelay() {
        // Modern browsers handle this automatically with viewport meta tag
        // This is a fallback for older browsers
        document.body.style.touchAction = 'manipulation';
        
        // Add CSS class for styling
        if ('ontouchstart' in window) {
            document.documentElement.classList.add('touch-device');
        } else {
            document.documentElement.classList.add('no-touch');
        }
    }

    // Kiosk mode detection and setup
    function setupKioskMode() {
        // Check if running in kiosk mode
        const isKiosk = window.navigator.standalone || 
                       window.matchMedia('(display-mode: fullscreen)').matches ||
                       window.matchMedia('(display-mode: standalone)').matches;
        
        if (isKiosk) {
            document.body.classList.add('kiosk-mode');
            
            // Hide browser UI elements
            if (window.navigator.standalone) {
                // iOS Safari standalone mode
                document.body.style.paddingTop = '0';
            }
        }
    }

    // Auto-refresh for kiosk mode (optional)
    function setupAutoRefresh() {
        // Refresh every 30 minutes to prevent memory leaks
        setInterval(function() {
            if (document.hidden) {
                location.reload();
            }
        }, 30 * 60 * 1000);
    }

    // Touch gesture handling
    function setupTouchGestures() {
        let startX, startY, startTime;
        let currentX, currentY;
        
        document.addEventListener('touchstart', function(e) {
            if (e.touches.length === 1) {
                const touch = e.touches[0];
                startX = touch.clientX;
                startY = touch.clientY;
                currentX = startX;
                currentY = startY;
                startTime = Date.now();
            }
        }, { passive: true });
        
        document.addEventListener('touchmove', function(e) {
            if (e.touches.length === 1) {
                const touch = e.touches[0];
                currentX = touch.clientX;
                currentY = touch.clientY;
            }
        }, { passive: true });
        
        document.addEventListener('touchend', function(e) {
            if (!startX || !startY) return;
            
            const endX = currentX;
            const endY = currentY;
            const endTime = Date.now();
            
            const diffX = startX - endX;
            const diffY = startY - endY;
            const diffTime = endTime - startTime;
            
            const absDiffX = Math.abs(diffX);
            const absDiffY = Math.abs(diffY);
            
            // Swipe detection
            if (diffTime < 500) {
                if (absDiffX > absDiffY && absDiffX > 100) {
                    // Horizontal swipe
                    if (diffX > 0) {
                        document.dispatchEvent(new CustomEvent('swipeLeft', { 
                            detail: { distance: absDiffX, duration: diffTime }
                        }));
                    } else {
                        document.dispatchEvent(new CustomEvent('swipeRight', { 
                            detail: { distance: absDiffX, duration: diffTime }
                        }));
                    }
                } else if (absDiffY > absDiffX && absDiffY > 100) {
                    // Vertical swipe
                    if (diffY > 0) {
                        document.dispatchEvent(new CustomEvent('swipeUp', { 
                            detail: { distance: absDiffY, duration: diffTime }
                        }));
                    } else {
                        document.dispatchEvent(new CustomEvent('swipeDown', { 
                            detail: { distance: absDiffY, duration: diffTime }
                        }));
                    }
                }
            }
            
            // Tap detection
            if (absDiffX < 10 && absDiffY < 10 && diffTime < 300) {
                document.dispatchEvent(new CustomEvent('tap', {
                    detail: { x: endX, y: endY }
                }));
            }
            
            // Long press is handled in addTouchFeedback
            
            startX = startY = currentX = currentY = null;
        }, { passive: true });
    }
    
    // Orientation change handler
    function handleOrientationChange() {
        window.addEventListener('orientationchange', function() {
            // Force layout recalculation
            setTimeout(function() {
                window.scrollTo(0, 0);
                document.body.style.height = window.innerHeight + 'px';
                setTimeout(function() {
                    document.body.style.height = '';
                }, 100);
            }, 100);
        });
    }
    
    // Improve input focus on mobile
    function improveMobileFocus() {
        const inputs = document.querySelectorAll('input, textarea, select');
        
        inputs.forEach(input => {
            input.addEventListener('focus', function() {
                // Scroll into view with some padding
                setTimeout(() => {
                    this.scrollIntoView({ 
                        behavior: 'smooth', 
                        block: 'center' 
                    });
                }, 300);
            });
        });
    }
    
    // Improve touch target sizes dynamically
    function improveTouchTargets() {
        const minTouchSize = 44; // 44px minimum touch target
        
        const elements = document.querySelectorAll('button, a, input[type="checkbox"], input[type="radio"]');
        
        elements.forEach(element => {
            const rect = element.getBoundingClientRect();
            
            if (rect.width < minTouchSize || rect.height < minTouchSize) {
                // Add padding to increase touch target
                const currentPadding = parseInt(window.getComputedStyle(element).padding) || 0;
                const neededPadding = Math.max(0, (minTouchSize - Math.min(rect.width, rect.height)) / 2);
                
                if (neededPadding > currentPadding) {
                    element.style.padding = `${neededPadding}px`;
                }
            }
        });
    }

    // Initialize all kiosk enhancements
    function init() {
        console.log('Initializing kiosk touch enhancements...');
        
        try {
            addTouchFeedback();
            observeDynamicButtons();
            preventZoom();
            preventContextMenu();
            hideCursor();
            enhanceScroll();
            removeClickDelay();
            setupKioskMode();
            setupTouchGestures();
            handleOrientationChange();
            improveMobileFocus();
            
            // Delay touch target improvements to allow layout to settle
            setTimeout(improveTouchTargets, 500);
            
            // Only setup auto-refresh in production
            if (window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1') {
                setupAutoRefresh();
            }
            
            console.log('Kiosk touch enhancements initialized successfully');
            console.log('Device type:', 'ontouchstart' in window ? 'Touch device' : 'Non-touch device');
            console.log('Screen size:', window.innerWidth + 'x' + window.innerHeight);
            console.log('User agent:', navigator.userAgent);
        } catch (error) {
            console.error('Error initializing kiosk enhancements:', error);
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Re-initialize after Blazor updates
    window.addEventListener('load', function() {
        setTimeout(init, 1000);
    });
    
    // Re-run touch feedback setup after Blazor re-renders
    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', function() {
            setTimeout(addTouchFeedback, 100);
        });
    }
    
    // Periodically re-enhance new buttons (for dynamic content)
    setInterval(function() {
        addTouchFeedback();
    }, 2000);

})();
