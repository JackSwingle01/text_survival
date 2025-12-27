/**
 * Progress Display Module
 *
 * Pure animation module - just animates the progress bar.
 * FrameQueue owns sequencing; this module just handles visual display.
 */

export const ProgressDisplay = {
    intervalId: null,
    onComplete: null,

    /**
     * Start a local progress animation that runs for the specified duration.
     * @param {number} durationSeconds - Animation duration in seconds
     * @param {string} statusText - Status text to display
     * @param {function} onComplete - Callback when animation completes
     */
    start(durationSeconds, statusText, onComplete) {
        this.stop();  // Always clean up first

        const progressTextEl = document.getElementById('progressText');
        const progressIcon = document.getElementById('progressIcon');
        const progressBar = document.getElementById('progressBar');

        // Reset bar to 0% at start
        progressBar.style.width = '0%';

        // Show progress UI
        progressTextEl.textContent = statusText || 'Working...';
        progressTextEl.classList.add('active');
        progressIcon.textContent = 'pending';

        // Store completion callback
        this.onComplete = onComplete;

        const startTime = Date.now();
        const durationMs = durationSeconds * 1000;

        // Animate progress locally
        this.intervalId = setInterval(() => {
            const elapsed = Date.now() - startTime;
            const pct = Math.min(100, Math.round((elapsed / durationMs) * 100));
            progressBar.style.width = pct + '%';

            // Stop at 100% and call completion callback
            if (pct >= 100) {
                clearInterval(this.intervalId);
                this.intervalId = null;
                if (this.onComplete) {
                    // Capture and clear callback BEFORE calling - the callback may start
                    // a new animation that sets its own onComplete, which we must not overwrite
                    const callback = this.onComplete;
                    this.onComplete = null;
                    // Brief pause at 100% so user can see completion before next frame
                    setTimeout(callback, 150);
                }
            }
        }, 50); // Update every 50ms for smooth animation
    },

    /**
     * Stop any running local progress animation.
     */
    stop() {
        if (this.intervalId) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }

        // Clear callback to prevent stale state
        this.onComplete = null;

        // Reset progress bar
        const progressBar = document.getElementById('progressBar');
        if (progressBar) {
            progressBar.style.width = '0%';
        }

        const progressTextEl = document.getElementById('progressText');
        if (progressTextEl) {
            progressTextEl.classList.remove('active');
        }
    }
};
