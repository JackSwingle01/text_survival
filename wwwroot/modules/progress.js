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
        }

        // Interpolate fire time remaining (if fire exists)
        if (this.startState.fire && this.statDeltas.fireMinutesRemaining !== undefined) {
            const current = lerp(this.startState.fire.minutesRemaining, this.statDeltas.fireMinutesRemaining);
            const phaseTextEl = document.getElementById('firePhaseText');
            if (phaseTextEl && current > 0) {
                const phaseLabel = this.startState.fire.phase;
                phaseTextEl.textContent = `${phaseLabel} — ${Math.round(current)} min`;
            }
        }
    },

    /**
     * Update a stat bar to show interpolated value
     * @param {string} statName - Name of the stat
     * @param {number} value - Current interpolated value (0-100)
     */
    updateStatBar(statName, value) {
        const bar = document.querySelector(`[data-stat="${statName}"] .stat-fill`);
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
