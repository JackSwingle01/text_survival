// overlays/DeathOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { clear } from '../lib/helpers.js';
import { SimpleStatRow } from '../lib/components/StatRow.js';
import { ActionButton } from '../lib/components/ActionButton.js';

/**
 * DeathOverlay - Game over screen showing cause of death and final stats
 */
export class DeathOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('deathOverlay', inputHandler);
    }

    render(data, inputId, input) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Set cause of death
        const causeEl = this.$('#deathCause');
        if (causeEl) causeEl.textContent = data.causeOfDeath;

        // Build stats
        const statsEl = this.$('#deathStats');
        if (statsEl) {
            clear(statsEl);
            statsEl.appendChild(
                SimpleStatRow('schedule', 'Time Survived', data.timeSurvived)
            );
            statsEl.appendChild(
                SimpleStatRow('favorite', 'Final Vitality', `${data.finalVitality.toFixed(0)}%`)
            );
            statsEl.appendChild(
                SimpleStatRow('restaurant', 'Final Calories', `${data.finalCalories.toFixed(0)} kcal`)
            );
            statsEl.appendChild(
                SimpleStatRow('water_drop', 'Final Hydration', `${data.finalHydration.toFixed(0)}%`)
            );
            statsEl.appendChild(
                SimpleStatRow('device_thermostat', 'Body Temperature', `${data.finalTemperature.toFixed(1)}°F`)
            );
        }

        // Build choices (restart button)
        const choicesEl = this.$('#deathChoices');
        if (choicesEl) {
            clear(choicesEl);
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
}
