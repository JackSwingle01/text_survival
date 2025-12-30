import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils } from '../modules/utils.js';

/**
 * DeathOverlay - Game over screen showing cause of death and final stats
 */
export class DeathOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('deathOverlay', inputHandler);
        this.causeEl = document.getElementById('deathCause');
        this.statsEl = document.getElementById('deathStats');
        this.choicesEl = document.getElementById('deathChoices');
    }

    render(data, inputId, input) {
        this.show(inputId);

        // Set cause of death
        this.causeEl.textContent = data.causeOfDeath;

        // Build stats
        this.clear(this.statsEl);
        const statLines = [
            `Time Survived: ${data.timeSurvived}`,
            `Final Vitality: ${data.finalVitality.toFixed(0)}%`,
            `Final Calories: ${data.finalCalories.toFixed(0)} kcal`,
            `Final Hydration: ${data.finalHydration.toFixed(0)}%`,
            `Body Temperature: ${data.finalTemperature.toFixed(1)}°F`
        ];

        statLines.forEach(line => {
            this.statsEl.appendChild(
                DOMBuilder.div().text(line).build()
            );
        });

        // Add restart/choice buttons
        this.clear(this.choicesEl);

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
        this.clear(this.statsEl);
        this.clear(this.choicesEl);
    }
}
