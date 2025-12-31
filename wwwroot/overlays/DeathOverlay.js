import { OverlayManager } from '../core/OverlayManager.js';
import { StatRow } from '../components/StatRow.js';
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

        // Build stats using StatRow component
        this.clear(this.statsEl);

        this.statsEl.appendChild(
            StatRow.simple('schedule', 'Time Survived', data.timeSurvived)
        );

        this.statsEl.appendChild(
            StatRow.simple('favorite', 'Final Vitality', `${data.finalVitality.toFixed(0)}%`)
        );

        this.statsEl.appendChild(
            StatRow.simple('restaurant', 'Final Calories', `${data.finalCalories.toFixed(0)} kcal`)
        );

        this.statsEl.appendChild(
            StatRow.simple('water_drop', 'Final Hydration', `${data.finalHydration.toFixed(0)}%`)
        );

        this.statsEl.appendChild(
            StatRow.simple('device_thermostat', 'Body Temperature', `${data.finalTemperature.toFixed(1)}°F`)
        );

        // Add restart/choice buttons
        if (input?.choices) {
            this.setChoices(input.choices, '#deathChoices');
        }
    }

    cleanup() {
        this.clear(this.statsEl);
        this.clear(this.choicesEl);
    }
}
