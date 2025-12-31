import { getGridRenderer } from './grid/CanvasGridRenderer.js';
import { AnimatedStatRegistry, StatRenderer } from './animatedStatRegistry.js';

/**
 * Progress Display Module
 *
 * Pure animation module - just animates the progress bar.
 * FrameQueue owns sequencing; this module just handles visual display.
 */

export const ProgressDisplay = {
    intervalId: null,
    onComplete: null,
    startState: null,
    statDeltas: null,
    completed: false,  // Track whether animation completed (vs interrupted)

    /**
     * Start a local progress animation that runs for the specified duration.
     * @param {number} durationSeconds - Animation duration in seconds
     * @param {string} statusText - Status text to display
     * @param {object} startState - Initial game state (for stat interpolation)
     * @param {object} statDeltas - Stat changes to animate (optional)
     * @param {function} onComplete - Callback when animation completes
     */
    start(durationSeconds, statusText, startState, statDeltas, onComplete) {
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

        // Store state for stat animation
        this.onComplete = onComplete;
        this.startState = startState;
        this.statDeltas = statDeltas;

        const startTime = Date.now();
        const durationMs = durationSeconds * 1000;

        // Animate progress locally
        this.intervalId = setInterval(() => {
            const elapsed = Date.now() - startTime;
            const pct = Math.min(100, Math.round((elapsed / durationMs) * 100));
            const progress = pct / 100; // 0.0 to 1.0

            progressBar.style.width = pct + '%';

            // Animate stats if we have deltas
            if (this.startState && this.statDeltas) {
                this.animateStats(progress);
            }

            // Stop at 100% and call completion callback
            if (pct >= 100) {
                this.completed = true;  // Mark as completed (not interrupted)
                clearInterval(this.intervalId);
                this.intervalId = null;
                if (this.onComplete) {
                    const callback = this.onComplete;
                    this.onComplete = null;
                    this.startState = null;
                    this.statDeltas = null;
                    setTimeout(callback, 150);
                }
            }
        }, 30); // Update every 30ms for smooth animation
    },

    /**
     * Start progress animation with synchronized camera pan.
     * Used for travel to animate both progress bar and map movement together.
     * @param {number} durationSeconds - Animation duration in seconds
     * @param {string} statusText - Status text to display
     * @param {object} startState - Initial game state (for stat interpolation)
     * @param {object} statDeltas - Stat changes to animate
     * @param {number} originX - Origin grid X position (for camera animation)
     * @param {number} originY - Origin grid Y position (for camera animation)
     * @param {function} onComplete - Callback when animation completes
     */
    startWithCamera(durationSeconds, statusText, startState, statDeltas, originX, originY, onComplete) {
        // Start the camera pan animation first (before progress bar)
        const gridRenderer = getGridRenderer();
        if (gridRenderer) {
            gridRenderer.startAnimatedPan(originX, originY, durationSeconds * 1000);
        }

        // Then start the regular progress animation
        this.start(durationSeconds, statusText, startState, statDeltas, onComplete);
    },

    /**
     * Interpolate and display stats during animation
     * @param {number} progress - Animation progress (0.0 to 1.0)
     */
    animateStats(progress) {
        if (!this.startState || !this.statDeltas) return;

        // Generic lerp function
        const lerp = (start, delta) => start + (delta * progress);

        // Iterate through registry and interpolate all stats
        for (const [key, def] of Object.entries(AnimatedStatRegistry)) {
            if (this.statDeltas[key] === undefined) continue;

            const startVal = this.startState[key];
            const delta = this.statDeltas[key];

            // Handle fire time special case (object interpolation)
            let interpolatedValue;
            if (typeof startVal === 'object' && startVal !== null) {
                interpolatedValue = {
                    minutes: lerp(startVal.minutes, delta.minutes),
                    phase: delta.phase || startVal.phase
                };
            } else {
                interpolatedValue = lerp(startVal, delta);
            }

            // Use registry renderer to update DOM
            const renderer = StatRenderer[def.type];
            if (renderer) {
                renderer(interpolatedValue, def);
            }
        }
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

        // Only reset progress bar AND hide UI if animation completed normally
        // If interrupted (e.g., by event), leave bar visible at current percentage
        const progressBar = document.getElementById('progressBar');
        const progressTextEl = document.getElementById('progressText');

        if (this.completed) {
            // Completed normally (reached 100%) - reset and hide
            if (progressBar) {
                progressBar.style.width = '0%';
            }
            if (progressTextEl) {
                progressTextEl.classList.remove('active');
            }
        }
        // If not completed (interrupted), leave bar visible at current percentage

        this.completed = false;  // Reset flag for next animation
    }
};
