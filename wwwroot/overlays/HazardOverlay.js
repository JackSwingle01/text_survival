// overlays/HazardOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { clear } from '../lib/helpers.js';
import { ActionButton } from '../lib/components/ActionButton.js';

/**
 * HazardOverlay - Hazardous terrain warning with quick/careful choices
 */
export class HazardOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('hazardOverlay', inputHandler);
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Set hazard description
        const descEl = this.$('#hazardDescription');
        if (descEl) descEl.textContent = data.hazardDescription;

        // Build choices
        const choicesEl = this.$('#hazardChoices');
        if (choicesEl) {
            clear(choicesEl);

            // Quick option
            choicesEl.appendChild(
                ActionButton(
                    {
                        label: 'Quick',
                        description: `${data.quickTimeMinutes} min • ${(data.injuryRisk * 100).toFixed(0)}% injury risk`
                    },
                    () => this.sendAction('hazard_choice', { quickTravel: true, choiceId: 'quick' })
                )
            );

            // Careful option
            choicesEl.appendChild(
                ActionButton(
                    {
                        label: 'Careful',
                        description: `${data.carefulTimeMinutes} min • Safe passage`
                    },
                    () => this.sendAction('hazard_choice', { quickTravel: false, choiceId: 'careful' })
                )
            );
        }
    }
}
