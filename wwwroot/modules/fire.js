import { Utils } from './utils.js';

export const FireDisplay = {
    render(fire) {
        const phaseEl = document.getElementById('firePhase');
        const phaseText = phaseEl.querySelector('.fire-phase-text');
        const timeEl = document.getElementById('fireTime');
        const fuelEl = document.getElementById('fireFuel');
        const heatEl = document.getElementById('fireHeat');

        if (!fire) {
            phaseText.textContent = 'No fire pit';
            phaseEl.className = 'fire-phase cold';
            timeEl.textContent = '';
            fuelEl.textContent = '';
            heatEl.textContent = '';
            return;
        }

        if (fire.phase === 'Cold') {
            phaseText.textContent = 'Cold';
            phaseEl.className = 'fire-phase cold';
            timeEl.textContent = '';
            // Show fuel if any is loaded
            Utils.clearElement(fuelEl);
            const icon = document.createElement('span');
            icon.className = 'material-symbols-outlined';
            icon.textContent = 'local_fire_department';
            fuelEl.appendChild(icon);

            if (fire.totalKg > 0) {
                const litPercent = fire.totalKg > 0 ? Math.round(fire.burningKg / fire.totalKg * 100) : 0;
                const text = document.createTextNode(`${fire.totalKg.toFixed(1)}kg fuel (${litPercent}% lit)`);
                fuelEl.appendChild(text);
            } else {
                const text = document.createTextNode('No fuel');
                fuelEl.appendChild(text);
            }
            heatEl.textContent = '';
            return;
        }

        // Active fire
        phaseText.textContent = fire.phase;
        phaseEl.className = 'fire-phase ' + fire.phase.toLowerCase();

        // Time remaining with burn rate
        timeEl.textContent = `${fire.minutesRemaining} min (${fire.burnRateKgPerHour.toFixed(1)} kg/hr)`;

        // Fuel breakdown: burning vs unlit, or total/max
        Utils.clearElement(fuelEl);
        const fuelIcon = document.createElement('span');
        fuelIcon.className = 'material-symbols-outlined';
        fuelIcon.textContent = 'local_fire_department';
        fuelEl.appendChild(fuelIcon);

        if (fire.unlitKg > 0.1) {
            const burningSpan = document.createElement('span');
            burningSpan.className = 'fire-burning';
            burningSpan.textContent = `${fire.burningKg.toFixed(1)}kg burning`;
            const unlitSpan = document.createElement('span');
            unlitSpan.className = 'fire-unlit';
            unlitSpan.textContent = ` (+${fire.unlitKg.toFixed(1)}kg unlit)`;
            fuelEl.appendChild(burningSpan);
            fuelEl.appendChild(unlitSpan);
        } else {
            const fuelText = document.createTextNode(`${fire.totalKg.toFixed(1)}/${fire.maxCapacityKg.toFixed(0)} kg fuel`);
            fuelEl.appendChild(fuelText);
        }

        // Heat output
        if (fire.heatOutput > 0) {
            heatEl.textContent = `+${fire.heatOutput.toFixed(0)}°F heat`;
        } else {
            heatEl.textContent = '';
        }
    }
};
