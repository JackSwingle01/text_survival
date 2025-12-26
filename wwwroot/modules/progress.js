/**
 * Progress Display Module
 *
 * Handles client-side progress animation for long-running operations.
 * Displays a segmented progress bar that animates locally while waiting for server response.
 */

export const ProgressDisplay = {
    intervalId: null,

    /**
     * Start a local progress animation that runs for the specified duration.
     * Client animates the progress bar locally instead of waiting for server updates.
     */
    start(durationSeconds, statusText) {
        const statusTextEl = document.getElementById('statusText');
        const statusIcon = document.getElementById('statusIcon');
        const progressContainer = document.getElementById('progressSegmentBar');
        const progressPercent = document.getElementById('progressPercent');
        const actionsArea = document.getElementById('actionsArea');

        // Show progress UI
        statusTextEl.textContent = statusText || 'Working...';
        statusIcon.style.display = '';
        progressContainer.style.display = '';
        progressPercent.style.display = '';

        // Clear actions while progress is running
        this.clearElement(actionsArea);

        const startTime = Date.now();
        const durationMs = durationSeconds * 1000;

        // Animate progress locally
        this.intervalId = setInterval(() => {
            const elapsed = Date.now() - startTime;
            const pct = Math.min(100, Math.round((elapsed / durationMs) * 100));
            this.renderSegments(pct, pct >= 100);
            progressPercent.textContent = pct + '%';

            // Stop at 100% (server response will arrive and stop animation properly)
            if (pct >= 100) {
                clearInterval(this.intervalId);
                this.intervalId = null;
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
    },

    /**
     * Render progress bar segments at the given percentage.
     */
    renderSegments(percent, complete = false) {
        const container = document.getElementById('progressSegmentBar');
        if (!container.classList.contains('progress')) {
            container.classList.add('progress');
        }
        this.clearElement(container);

        // Create single fill div instead of segments
        const fill = document.createElement('div');
        fill.className = 'progress-fill';
        fill.style.width = percent + '%';

        if (complete) {
            container.classList.add('complete');
        } else {
            container.classList.remove('complete');
        }

        container.appendChild(fill);
    },

    /**
     * Helper to clear all children from a DOM element.
     */
    clearElement(el) {
        while (el.firstChild) {
            el.removeChild(el.firstChild);
        }
    }
};
