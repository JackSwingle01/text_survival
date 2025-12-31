import { OverlayManager } from '../core/OverlayManager.js';
import { Utils } from '../modules/utils.js';

/**
 * ConfirmOverlay - Simple confirmation dialog with yes/no choices
 */
export class ConfirmOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('confirmOverlay', inputHandler);
        this.promptEl = document.getElementById('confirmPrompt');
        this.choicesEl = document.getElementById('confirmChoices');
    }

    render(prompt, inputId, input) {
        this.show(inputId);

        // Set prompt text
        this.promptEl.textContent = prompt;

        // Create Yes/No buttons from input choices
        if (input?.choices) {
            this.setChoices(input.choices, '#confirmChoices');
        }
    }

    cleanup() {
        // Clear any state
        this.clear(this.choicesEl);
    }
}
