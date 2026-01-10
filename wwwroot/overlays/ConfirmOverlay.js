// overlays/ConfirmOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div } from '../lib/helpers.js';
import { ActionButton } from '../lib/components/ActionButton.js';

/**
 * ConfirmOverlay - Simple confirmation dialog with yes/no choices
 */
export class ConfirmOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('confirmOverlay', inputHandler);
    }

    render(prompt, inputId, input) {
        if (!prompt) {
            this.hide();
            return;
        }

        this.show();

        // Set prompt text
        const promptEl = this.$('#confirmPrompt');
        if (promptEl) promptEl.textContent = prompt;

        // Build choices
        const choicesEl = this.$('#confirmChoices');
        if (choicesEl) {
            choicesEl.replaceChildren();
            if (input?.choices) {
                input.choices.forEach(choice => {
                    if (!choice.id) return;
                    choicesEl.appendChild(
                        ActionButton(choice, () => this.respond(choice.id))
                    );
                });
            }
        }
    }

    // safeRender is inherited from OverlayBase
}
