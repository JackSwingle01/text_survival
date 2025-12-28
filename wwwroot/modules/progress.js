import { getGridRenderer } from './grid/CanvasGridRenderer.js';

/**
 * Format minutes since midnight as a clock time string (e.g., "9:30 AM")
 */
function formatClockTime(totalMinutes) {
    // Handle day wrapping (1440 = 24 hours * 60 min)
    totalMinutes = ((totalMinutes % 1440) + 1440) % 1440;
    const hours24 = Math.floor(totalMinutes / 60);
    const mins = Math.floor(totalMinutes % 60);
    const hours12 = hours24 % 12 || 12;
    const ampm = hours24 < 12 ? 'AM' : 'PM';
    return `${hours12}:${mins.toString().padStart(2, '0')} ${ampm}`;
}

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

        // Interpolate survival stats
        const lerp = (start, delta) => start + (delta * progress);

        if (this.statDeltas.healthPercent !== undefined) {
            const current = lerp(this.startState.healthPercent, this.statDeltas.healthPercent);
            this.updateStatBar('healthPercent', current);
        }
        if (this.statDeltas.foodPercent !== undefined) {
            const current = lerp(this.startState.foodPercent, this.statDeltas.foodPercent);
            this.updateStatBar('foodPercent', current);
        }
        if (this.statDeltas.waterPercent !== undefined) {
            const current = lerp(this.startState.waterPercent, this.statDeltas.waterPercent);
            this.updateStatBar('waterPercent', current);
        }
        if (this.statDeltas.energyPercent !== undefined) {
            const current = lerp(this.startState.energyPercent, this.statDeltas.energyPercent);
            this.updateStatBar('energyPercent', current);
        }

        // Interpolate body temperature
        if (this.statDeltas.bodyTemp !== undefined) {
            const current = lerp(this.startState.bodyTemp, this.statDeltas.bodyTemp);
            const tempEl = document.getElementById('bodyTempDisplay');
            if (tempEl) tempEl.textContent = `${current.toFixed(1)}°F`;

            // Also update temperature bar (87-102°F range)
            const tempBar = document.getElementById('tempBar');
            if (tempBar) {
                const tempPct = Math.max(0, Math.min(100, ((current - 87) / 15) * 100));
                tempBar.style.width = tempPct + '%';
            }
        }

        // Interpolate clock time badge
        if (this.statDeltas.clockTimeMinutes !== undefined) {
            const currentMinutes = lerp(this.startState.clockTimeMinutes, this.statDeltas.clockTimeMinutes);
            const timeEl = document.getElementById('badgeTime');
            if (timeEl) timeEl.textContent = formatClockTime(currentMinutes);
        }

        // Interpolate feels-like temperature badge
        if (this.statDeltas.airTemp !== undefined) {
            const currentTemp = lerp(this.startState.airTemp, this.statDeltas.airTemp);
            const tempEl = document.getElementById('badgeFeelsLike');
            if (tempEl) tempEl.textContent = `${Math.round(currentTemp)}°F`;
        }

        // Interpolate fire time remaining (if fire exists)
        if (this.startState.fire && this.statDeltas.fireMinutesRemaining !== undefined) {
            const current = lerp(this.startState.fire.minutesRemaining, this.statDeltas.fireMinutesRemaining);
            const phaseTextEl = document.getElementById('firePhaseText');
            if (phaseTextEl && current > 0) {
                const phaseLabel = this.startState.fire.phase;
                const currentMinutes = Math.round(current);
                const timeDisplay = currentMinutes >= 60
                    ? `${Math.floor(currentMinutes / 60)}hrs`
                    : `${currentMinutes}min`;
                phaseTextEl.textContent = `${phaseLabel} — ${timeDisplay}`;
            }
        }
    },

    /**
     * Update a stat bar to show interpolated value
     * @param {string} statName - Name of the stat
     * @param {number} value - Current interpolated value (0-100)
     */
    updateStatBar(statName, value) {
        // Map from stat names used in animation to actual element IDs
        const idMap = {
            'healthPercent': 'healthBar',
            'foodPercent': 'foodBar',
            'waterPercent': 'waterBar',
            'energyPercent': 'energyBar'
        };

        const barId = idMap[statName];
        if (!barId) return;

        const bar = document.getElementById(barId);
        if (bar) {
            bar.style.width = `${Math.max(0, Math.min(100, value))}%`;
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

        // Only reset progress bar if animation completed normally
        // If interrupted (e.g., by event), leave bar where it was
        const progressBar = document.getElementById('progressBar');
        if (progressBar && this.completed) {
            progressBar.style.width = '0%';
        }
        this.completed = false;  // Reset flag for next animation

        const progressTextEl = document.getElementById('progressText');
        if (progressTextEl) {
            progressTextEl.classList.remove('active');
        }
    }
};
