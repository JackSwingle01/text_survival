import { ProgressDisplay } from './progress.js';
import { AnimatedStatRegistry } from './animatedStatRegistry.js';

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
            // Capture state using registry
            startState = {};
            for (const [key, def] of Object.entries(AnimatedStatRegistry)) {
                startState[key] = def.capture(this.currentState);
            }
        }

        // Render the frame content (state, mode, overlays, input)
        this.renderCallback(nextFrame);

        // Store current state for next frame
        if (nextFrame.state) {
            this.currentState = nextFrame.state;
        }

        // Handle progress animation
        if ((isProgress || isTravelProgress) && startState) {
            // Calculate stat deltas using registry
            const statDeltas = {};
            const endState = nextFrame.state;

            for (const [key, def] of Object.entries(AnimatedStatRegistry)) {
                const startVal = startState[key];
                const endVal = def.capture(endState);

                // Handle fire time special case (object with minutes + phase)
                if (typeof startVal === 'object' && startVal !== null) {
                    statDeltas[key] = {
                        minutes: (endVal?.minutes || 0) - (startVal?.minutes || 0),
                        phase: endVal?.phase || startVal?.phase || ''
                    };
                } else {
                    statDeltas[key] = endVal - startVal;
                }
            }

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
