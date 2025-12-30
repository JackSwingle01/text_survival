// modules/core/InputHandler.js
export class InputHandler {
    constructor(getSocket, getCurrentInputId) {
        this.getSocket = getSocket;
        this.getCurrentInputId = getCurrentInputId;
        this.awaitingResponse = false;
        this.resumeBlockUntil = 0;
    }

    /**
     * Check if input is currently blocked
     */
    isBlocked() {
        if (this.awaitingResponse) return true;
        if (Date.now() < this.resumeBlockUntil) return true;
        return false;
    }

    /**
     * Validate an inputId against current state
     */
    isValidInputId(inputId) {
        if (!inputId || inputId <= 0) return false;
        if (inputId !== this.getCurrentInputId()) return false;
        return true;
    }

    /**
     * Block input briefly (e.g., after page resume)
     */
    blockBriefly(durationMs = 200) {
        this.resumeBlockUntil = Date.now() + durationMs;
    }

    /**
     * Reset state (e.g., after receiving response)
     */
    reset() {
        this.awaitingResponse = false;
    }

    /**
     * Send a validated response
     * Returns true if sent, false if blocked/invalid
     */
    send(message, inputId) {
        if (this.isBlocked()) return false;
        if (!this.isValidInputId(inputId)) {
            console.log(`[InputHandler] Rejecting stale input: ${inputId} vs ${this.getCurrentInputId()}`);
            return false;
        }

        const socket = this.getSocket();
        if (!socket || socket.readyState !== WebSocket.OPEN) {
            console.warn('[InputHandler] Socket not ready');
            return false;
        }

        this.awaitingResponse = true;
        this.disableAllButtons();

        socket.send(JSON.stringify({ ...message, inputId }));
        return true;
    }

    /**
     * Send a choice response
     */
    respond(choiceId, inputId) {
        // Validate choiceId - FAIL LOUDLY if invalid
        if (!choiceId || choiceId === '') {
            console.error('[InputHandler] INVALID choiceId rejected:',
                { choiceId, type: typeof choiceId, inputId },
                '\nStack:', new Error().stack);
            // Do NOT send anything - this is a bug that needs fixing
            return false;
        }
        return this.send({ choiceId }, inputId);
    }

    /**
     * Send a typed action
     */
    sendAction(type, data, inputId) {
        return this.send({ type, ...data }, inputId);
    }

    /**
     * Disable all buttons to prevent double-clicks
     */
    disableAllButtons() {
        document.querySelectorAll('.btn, .option-btn, .combat-action').forEach(btn => {
            btn.disabled = true;
        });
    }

    /**
     * Re-enable all buttons
     */
    enableAllButtons() {
        document.querySelectorAll('.btn, .option-btn, .combat-action').forEach(btn => {
            btn.disabled = false;
        });
    }
}