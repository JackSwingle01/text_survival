export const FireDisplay = {
    render(fire) {
        const statusEl = document.getElementById('fireStatus');
        const phaseText = document.getElementById('firePhaseText');
        const fuelEl = document.getElementById('fireFuel');
        const heatEl = document.getElementById('fireHeat');

        if (!fire) {
            phaseText.textContent = 'No fire pit';
            statusEl.className = 'fire-status cold';
            fuelEl.textContent = '--';
            heatEl.textContent = '--';
            return;
        }

        if (fire.phase === 'Cold') {
            phaseText.textContent = 'No fire';
            statusEl.className = 'fire-status cold';

            if (fire.totalKg > 0) {
                fuelEl.textContent = `${fire.totalKg.toFixed(1)}kg ready`;
            } else {
                fuelEl.textContent = 'None';
            }
            heatEl.textContent = '--';
            return;
        }

        // Active fire - show phase and time
        const phaseLabel = fire.phase === 'Roaring' ? 'Roaring' :
                          fire.phase === 'Steady' ? 'Steady' :
                          fire.phase === 'Dying' ? 'Dying' :
                          fire.phase === 'Embers' ? 'Embers' :
                          fire.phase === 'Building' ? 'Building' :
                          fire.phase === 'Igniting' ? 'Igniting' : fire.phase;

        phaseText.textContent = `${phaseLabel} — ${fire.minutesRemaining} min`;
        statusEl.className = 'fire-status';

        // Fuel display
        if (fire.unlitKg > 0.1) {
            fuelEl.textContent = `${fire.burningKg.toFixed(1)}kg (+${fire.unlitKg.toFixed(1)}kg)`;
        } else {
            fuelEl.textContent = `${fire.totalKg.toFixed(1)}kg`;
        }

        // Heat output
        if (fire.heatOutput > 0) {
            heatEl.textContent = `+${fire.heatOutput.toFixed(0)}°F`;
            heatEl.className = 'status-value good';
        } else {
            heatEl.textContent = '--';
            heatEl.className = 'status-value';
        }
    }
};
