import { show, hide } from './utils.js';
import { getFirePhaseLabel } from './icons.js';

export const FireDisplay = {
    render(fire) {
        const statusEl = document.getElementById('fireStatus');
        const section = statusEl?.closest('.panel');

        // Hide entire section if no fire pit
        if (!fire) {
            hide(section);
            return;
        }

        // Show section
        show(section);

        const phaseText = document.getElementById('firePhaseText');
        const fuelEl = document.getElementById('fireFuel');
        const heatEl = document.getElementById('fireHeat');

        if (fire.phase === 'Cold') {
            phaseText.textContent = getFirePhaseLabel('Cold');
            statusEl.className = 'fire-status cold';

            if (fire.totalKg > 0) {
                fuelEl.textContent = `${fire.totalKg.toFixed(1)}kg ready`;
            } else {
                fuelEl.textContent = 'None';
            }
            heatEl.textContent = '--';
            return;
        }

        // Active fire - show phase and time (using centralized labels)
        const phaseLabel = getFirePhaseLabel(fire.phase);

        // Use pre-computed time display from server
        phaseText.textContent = `${phaseLabel} — ${fire.timeDisplay}`;

        // Apply urgency class for styling
        const urgencyClass = fire.urgency?.toLowerCase() || 'safe';
        statusEl.className = `fire-status fire-${urgencyClass}`;

        // Fuel display
        if (fire.unlitKg > 0.1) {
            fuelEl.textContent = `${fire.burningKg.toFixed(1)}kg (+${fire.unlitKg.toFixed(1)}kg)`;
        } else {
            fuelEl.textContent = `${fire.totalKg.toFixed(1)}kg`;
        }

        // Heat output
        if (fire.heatOutput > 0) {
            heatEl.textContent = `+${fire.heatOutput.toFixed(0)}°F`;
            heatEl.className = 'stat-row__value stat-row__value--good';
        } else {
            heatEl.textContent = '--';
            heatEl.className = 'stat-row__value';
        }
    }
};