export const TemperatureDisplay = {
    render(state) {
        const bodyTemp = state.bodyTemp;

        // Body temperature display
        const bodyTempEl = document.getElementById('bodyTempDisplay');
        bodyTempEl.textContent = `${bodyTemp.toFixed(1)}°F`;

        // Temperature bar (87-102 range for body temp)
        const tempPct = Math.max(0, Math.min(100, (bodyTemp - 87) / (102 - 87) * 100));
        const tempBar = document.getElementById('tempBar');
        if (tempBar) {
            tempBar.style.width = tempPct + '%';
        }

        // Temperature trend arrow (to the right of the bar)
        let trendArrow = document.getElementById('tempTrendArrow');
        if (!trendArrow) {
            trendArrow = document.createElement('span');
            trendArrow.id = 'tempTrendArrow';
            trendArrow.className = 'temp-trend-arrow';
            const tempStat = document.querySelector('[data-stat="body-temp"]');
            if (tempStat) tempStat.appendChild(trendArrow);
        }

        const arrow = state.trendPerHour > 0.2 ? '↑' :
                     state.trendPerHour < -0.2 ? '↓' :
                     '→';
        const color = state.trendPerHour > 0.2 ? '#4caf50' :
                     state.trendPerHour < -0.2 ? '#f44336' :
                     '#888';
        trendArrow.textContent = arrow;
        trendArrow.style.color = color;

        // Air temperature display
        const airTempEl = document.getElementById('airTempDisplay');
        if (state.fireHeat > 0) {
            airTempEl.textContent = `${state.airTemp.toFixed(0)}°F (+${state.fireHeat.toFixed(0)}°)`;
        } else {
            airTempEl.textContent = `${state.airTemp.toFixed(0)}°F`;
        }

        // Temperature crisis - make section red and show warning
        const tempSection = document.querySelector('.status-section:has([data-stat="body-temp"])');
        let crisisWarning = document.getElementById('tempCrisisWarning');

        if (state.temperatureCrisis) {
            // Add red background to temperature section
            if (tempSection) tempSection.classList.add('temp-crisis');

            // Add/update warning text
            if (!crisisWarning) {
                crisisWarning = document.createElement('div');
                crisisWarning.id = 'tempCrisisWarning';
                crisisWarning.className = 'temp-crisis-warning';
                const tempStat = document.querySelector('[data-stat="body-temp"]');
                if (tempStat) tempStat.after(crisisWarning);
            }
            crisisWarning.textContent = state.temperatureCrisis.actionGuidance;
        } else {
            // Remove crisis styling
            if (tempSection) tempSection.classList.remove('temp-crisis');
            if (crisisWarning) crisisWarning.remove();
        }
    }
};
