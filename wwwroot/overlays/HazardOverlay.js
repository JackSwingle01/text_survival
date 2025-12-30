import { OverlayManager } from '../core/OverlayManager.js';
import { Utils } from '../modules/utils.js';

/**
 * HazardOverlay - Hazardous terrain warning with quick/careful choices
 */
export class HazardOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('hazardOverlay', inputHandler);
        this.descEl = document.getElementById('hazardDescription');
        this.choicesEl = document.getElementById('hazardChoices');
    }

    render(hazardPrompt, inputId) {
        this.show(inputId);

        // Set hazard description
        this.descEl.textContent = hazardPrompt.hazardDescription;

        // Clear previous choices
        this.clear(this.choicesEl);

        // Quick option
        const quickBtn = this.createHazardButton(
            'Quick',
            `${hazardPrompt.quickTimeMinutes} min • ${(hazardPrompt.injuryRisk * 100).toFixed(0)}% injury risk`,
            true
        );
        this.choicesEl.appendChild(quickBtn);

        // Careful option
        const carefulBtn = this.createHazardButton(
            'Careful',
            `${hazardPrompt.carefulTimeMinutes} min • Safe passage`,
            false
        );
        this.choicesEl.appendChild(carefulBtn);
    }

    createHazardButton(label, description, isQuick) {
        const btn = document.createElement('button');
        btn.className = 'option-btn';

        const labelSpan = document.createElement('span');
        labelSpan.className = 'option-btn__label';
        labelSpan.textContent = label;
        btn.appendChild(labelSpan);

        const descSpan = document.createElement('span');
        descSpan.className = 'option-btn__desc';
        descSpan.textContent = description;
        btn.appendChild(descSpan);

        btn.onclick = () => {
            this.inputHandler.sendAction(
                'hazard_choice',
                {
                    quickTravel: isQuick,
                    choiceId: isQuick ? 'quick' : 'careful'
                },
                this.inputId
            );
        };

        return btn;
    }

    cleanup() {
        this.clear(this.choicesEl);
    }
}
