/**
 * Mobile UI helpers - sidebar toggling, touch interactions
 */

export class MobileUI {
    static init() {
        this.initSidebarToggle();
        this.initTouchImprovements();
    }

    /**
     * Initialize mobile sidebar toggle functionality
     */
    static initSidebarToggle() {
        const statsToggle = document.getElementById('mobileStatsToggle');
        const backdrop = document.getElementById('mobileSidebarBackdrop');
        const leftSidebar = document.querySelector('.left-sidebar');
        const rightSidebar = document.querySelector('.right-sidebar');

        if (!statsToggle || !backdrop || !leftSidebar) return;

        let currentSidebar = null;

        // Toggle stats (left) sidebar
        statsToggle.addEventListener('click', () => {
            if (currentSidebar === 'left') {
                this.closeSidebars();
            } else {
                this.openSidebar('left');
            }
        });

        // Close on backdrop click
        backdrop.addEventListener('click', () => {
            this.closeSidebars();
        });

        // Close on escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && currentSidebar) {
                this.closeSidebars();
            }
        });

        // Store reference for access in other methods
        this._currentSidebar = null;
    }

    /**
     * Open a specific sidebar
     */
    static openSidebar(side) {
        const backdrop = document.getElementById('mobileSidebarBackdrop');
        const leftSidebar = document.querySelector('.left-sidebar');
        const rightSidebar = document.querySelector('.right-sidebar');

        // Close any open sidebar first
        this.closeSidebars();

        // Open requested sidebar
        if (side === 'left' && leftSidebar) {
            leftSidebar.classList.add('mobile-visible');
            backdrop?.classList.add('visible');
            this._currentSidebar = 'left';
        } else if (side === 'right' && rightSidebar) {
            rightSidebar.classList.add('mobile-visible');
            backdrop?.classList.add('visible');
            this._currentSidebar = 'right';
        }

        // Prevent body scrolling when sidebar open
        document.body.style.overflow = 'hidden';
    }

    /**
     * Close all sidebars
     */
    static closeSidebars() {
        const backdrop = document.getElementById('mobileSidebarBackdrop');
        const leftSidebar = document.querySelector('.left-sidebar');
        const rightSidebar = document.querySelector('.right-sidebar');

        leftSidebar?.classList.remove('mobile-visible');
        rightSidebar?.classList.remove('mobile-visible');
        backdrop?.classList.remove('visible');

        this._currentSidebar = null;

        // Restore body scrolling
        document.body.style.overflow = '';
    }

    /**
     * Initialize touch-specific improvements
     */
    static initTouchImprovements() {
        // Prevent pull-to-refresh on the game container
        const gameContainer = document.querySelector('.game-container');
        if (gameContainer) {
            gameContainer.addEventListener('touchstart', (e) => {
                // Only prevent if at top of scroll
                if (window.scrollY === 0) {
                    // Allow scrolling down but prevent pull-to-refresh
                    const touch = e.touches[0];
                    this._touchStartY = touch.clientY;
                }
            }, { passive: true });

            gameContainer.addEventListener('touchmove', (e) => {
                if (window.scrollY === 0 && this._touchStartY !== undefined) {
                    const touch = e.touches[0];
                    const deltaY = touch.clientY - this._touchStartY;

                    // Prevent pull-to-refresh (pulling down at top)
                    if (deltaY > 0) {
                        e.preventDefault();
                    }
                }
            }, { passive: false });
        }

        // Improve canvas touch handling
        const canvas = document.getElementById('gridCanvas');
        if (canvas) {
            // Only prevent multi-touch (pinch zoom) - allow single touch clicks through
            canvas.addEventListener('touchstart', (e) => {
                if (e.touches.length > 1) {
                    e.preventDefault(); // Prevent pinch zoom
                }
            }, { passive: false });

            canvas.addEventListener('touchmove', (e) => {
                if (e.touches.length > 1) {
                    e.preventDefault(); // Prevent pinch zoom
                }
            }, { passive: false });
        }

        // Add active state to buttons for better touch feedback
        document.addEventListener('touchstart', (e) => {
            if (e.target.matches('.btn, .option-btn, .list-item--clickable')) {
                e.target.classList.add('touch-active');
            }
        }, { passive: true });

        document.addEventListener('touchend', (e) => {
            if (e.target.matches('.btn, .option-btn, .list-item--clickable')) {
                setTimeout(() => {
                    e.target.classList.remove('touch-active');
                }, 150);
            }
        }, { passive: true });
    }

    /**
     * Check if we're on a mobile device
     */
    static isMobile() {
        return window.innerWidth <= 768;
    }

    /**
     * Check if we're on a touch device
     */
    static isTouchDevice() {
        return ('ontouchstart' in window) ||
               (navigator.maxTouchPoints > 0) ||
               (navigator.msMaxTouchPoints > 0);
    }
}
