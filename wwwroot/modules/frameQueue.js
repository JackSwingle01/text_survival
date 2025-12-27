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
        console.log(`[FrameQueue] enqueue, state=${this.state}, queueLen=${this.queue.length}`);

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
            console.log('[FrameQueue] Queue empty, state=idle');
            return;
        }

        this.state = 'processing';
        console.log(`[FrameQueue] Processing frame, mode=${nextFrame.mode?.type}`);

        // Render the frame content (state, mode, overlays, input)
        this.renderCallback(nextFrame);

        // Handle progress animation if in progress mode
        if (nextFrame.mode?.type === 'progress') {
            this.state = 'animating';
            ProgressDisplay.start(
                nextFrame.mode.estimatedDurationSeconds,
                nextFrame.mode.activityText,
                () => this.onAnimationComplete()
            );
        } else {
            // No animation - process next immediately
            this.processNext();
        }
    },

    /**
     * Called when progress animation completes
     */
    onAnimationComplete() {
        console.log(`[FrameQueue] Animation complete, queueLen=${this.queue.length}`);
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
