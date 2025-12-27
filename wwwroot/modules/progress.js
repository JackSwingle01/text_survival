/**
 * Progress Display Module
 *
 * Handles client-side progress animation for long-running operations.
 * Displays a progress bar that animates locally while waiting for server response.
 */

import { Utils } from './utils.js';

export const ProgressDisplay = {
    intervalId: null,

    /**
     * Start a local progress animation that runs for the specified duration.
     * Client animates the progress bar locally instead of waiting for server updates.
     */
    start(durationSeconds, statusText) {
        const progressTextEl = document.getElementById('progressText');
        const progressIcon = document.getElementById('progressIcon');
        const progressBar = document.getElementById('progressBar');
        const actionsArea = document.getElementById('actionButtons');

        // Show progress UI
        progressTextEl.textContent = statusText || 'Working...';
        progressTextEl.classList.add('active');
        progressIcon.textContent = 'pending';

        // Clear actions while progress is running
        Utils.clearElement(actionsArea);

        const startTime = Date.now();
        const durationMs = durationSeconds * 1000;

        // Animate progress locally
        this.intervalId = setInterval(() => {
            const elapsed = Date.now() - startTime;
            const pct = Math.min(100, Math.round((elapsed / durationMs) * 100));
            progressBar.style.width = pct + '%';

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
