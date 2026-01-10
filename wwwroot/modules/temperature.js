export const TemperatureDisplay = {
    render(state) {
        const bodyTemp = state.bodyTemp;

        // Body temperature display
        const bodyTempEl = document.getElementById('bodyTempDisplay');
        bodyTempEl.textContent = `${bodyTemp.toFixed(1)}°F`;

        // Temperature bar - use pre-computed percentage from server
        const tempBar = document.getElementById('tempBar');
        if (tempBar) {
            tempBar.style.width = state.bodyTempBarPct + '%';
        }

        // Temperature trend in tooltip
        const trendEl = document.getElementById('bodyTempTrend');
        if (trendEl && state.trendPerHour !== undefined) {
            const sign = state.trendPerHour > 0 ? '+' : '';
            const trendText = Math.abs(state.trendPerHour) > 0.1
                ? `(${sign}${state.trendPerHour.toFixed(1)}°F/hr)`
                : '(stable)';
            trendEl.textContent = trendText;
        }

        // Temperature trend arrow - use pre-computed from server
        let trendArrow = document.getElementById('tempTrendArrow');
        if (!trendArrow) {
            trendArrow = document.createElement('span');
            trendArrow.id = 'tempTrendArrow';
            trendArrow.className = 'temp-trend-arrow';
            const tempStat = document.querySelector('[data-stat="body-temp"]');
            if (tempStat) tempStat.appendChild(trendArrow);
        }

        trendArrow.textContent = state.tempTrend.arrow;
        trendArrow.style.color = state.tempTrend.color;

        // Air temperature display
        const airTempEl = document.getElementById('airTempDisplay');
        if (state.fireHeat > 0) {
            airTempEl.textContent = `${state.airTemp.toFixed(0)}°F (+${state.fireHeat.toFixed(0)}°)`;
        } else {
            airTempEl.textContent = `${state.airTemp.toFixed(0)}°F`;
        }

        // Clothing warmth display
        const clothingWarmthBar = document.getElementById('clothingWarmthBar');
        const clothingWarmthDisplay = document.getElementById('clothingWarmthDisplay');
        if (clothingWarmthBar && clothingWarmthDisplay) {
            const warmthPct = state.clothingWarmthPercent || 0;
            const capacityF = state.clothingWarmthCapacityF || 0;
            clothingWarmthBar.style.width = warmthPct + '%';

            // Tooltip shows current heat stored / max capacity
            const currentHeatF = (warmthPct / 100) * capacityF;
            clothingWarmthDisplay.textContent = `${currentHeatF.toFixed(1)}°F / ${capacityF.toFixed(1)}°F`;
        }

        // Temperature crisis - make section red and show warning
        const tempSection = document.querySelector('.panel:has([data-stat="body-temp"])');
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