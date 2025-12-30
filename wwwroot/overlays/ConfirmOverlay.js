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

        // Clear previous choices
        this.clear(this.choicesEl);

        // Create Yes/No buttons from input choices
        if (input?.choices) {
            input.choices.forEach(choice => {
                const btn = document.createElement('button');
                btn.className = 'option-btn';

                const label = document.createElement('span');
                label.className = 'option-btn__label';
                label.textContent = choice.label;
                btn.appendChild(label);

                btn.onclick = this.makeClickHandler(choice.id);
                this.choicesEl.appendChild(btn);
            });
        }
    }

    cleanup() {
        // Clear any state
        this.clear(this.choicesEl);
    }
}
