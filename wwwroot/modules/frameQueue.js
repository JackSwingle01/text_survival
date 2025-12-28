import { ProgressDisplay } from './progress.js';

/**
 * Frame Queue - owns all frame sequencing logic.
 *
 * State machine:
 *   IDLE → frame arrives → PROCESSING
 *   PROCESSING → has progress? → ANIMATING (wait for callback)
 *   PROCESSING → no progress? → render, check queue → IDLE or PROCESSING
 *   ANIMATING → animation done → check queue → IDLE or PROCESSING
 */
export const FrameQueue = {
    queue: [],
    state: 'idle',  // 'idle' | 'processing' | 'animating'
    renderCallback: null,  // Set by GameClient
    currentState: null,  // Track current game state for stat deltas

    /**
     * Initialize with the render callback
     */
    init(renderCallback) {
        this.renderCallback = renderCallback;
    },

    /**
     * Queue a frame for processing
     */
    enqueue(frame) {
        if (this.state === 'idle') {
            this.processNext(frame);
        } else {
            this.queue.push(frame);
        }
    },

    /**
     * Process a frame (either passed directly or from queue)
     */
    processNext(frame = null) {
        const nextFrame = frame || this.queue.shift();

        if (!nextFrame) {
            this.state = 'idle';
            return;
        }

        this.state = 'processing';

        // Capture state BEFORE rendering for progress animation
        const isProgress = nextFrame.mode?.type === 'progress';
        const isTravelProgress = nextFrame.mode?.type === 'travel_progress';
        let startState = null;
        if ((isProgress || isTravelProgress) && this.currentState) {
            startState = {
                healthPercent: this.currentState.healthPercent,
                foodPercent: this.currentState.foodPercent,
                waterPercent: this.currentState.waterPercent,
                energyPercent: this.currentState.energyPercent,
                bodyTemp: this.currentState.bodyTemp,
                clockTimeMinutes: this.currentState.clockTimeMinutes,
                airTemp: this.currentState.airTemp,
                fire: this.currentState.fire ? {
                    minutesRemaining: this.currentState.fire.minutesRemaining,
                    phase: this.currentState.fire.phase
                } : null
            };
        }

        // Render the frame content (state, mode, overlays, input)
        this.renderCallback(nextFrame);

        // Store current state for next frame
        if (nextFrame.state) {
            this.currentState = nextFrame.state;
        }

        // Handle progress animation
        if ((isProgress || isTravelProgress) && startState) {
            // Calculate stat deltas by comparing start vs current
            const endState = nextFrame.state;
            const statDeltas = {
                healthPercent: endState.healthPercent - startState.healthPercent,
                foodPercent: endState.foodPercent - startState.foodPercent,
                waterPercent: endState.waterPercent - startState.waterPercent,
                energyPercent: endState.energyPercent - startState.energyPercent,
                bodyTemp: endState.bodyTemp - startState.bodyTemp,
                clockTimeMinutes: endState.clockTimeMinutes - startState.clockTimeMinutes,
                airTemp: endState.airTemp - startState.airTemp,
                fireMinutesRemaining: endState.fire && startState.fire
                    ? endState.fire.minutesRemaining - startState.fire.minutesRemaining
                    : 0
            };

            this.state = 'animating';

            if (isTravelProgress) {
                // Travel progress: animate camera pan synchronized with progress bar
                ProgressDisplay.startWithCamera(
                    nextFrame.mode.estimatedDurationSeconds,
                    nextFrame.mode.activityText,
                    startState,
                    statDeltas,
                    nextFrame.mode.originX,
                    nextFrame.mode.originY,
                    () => this.onAnimationComplete()
                );
            } else {
                // Regular progress
                ProgressDisplay.start(
                    nextFrame.mode.estimatedDurationSeconds,
                    nextFrame.mode.activityText,
                    startState,
                    statDeltas,
                    () => this.onAnimationComplete()
                );
            }
        } else {
            // No animation - process next immediately
            this.processNext();
        }
    },

    /**
     * Called when progress animation completes
     */
    onAnimationComplete() {
        this.processNext();
    },

    /**
     * Clear queue and reset state (e.g., on disconnect)
     */
    reset() {
        this.queue = [];
        this.state = 'idle';
        ProgressDisplay.stop();
    }
};
