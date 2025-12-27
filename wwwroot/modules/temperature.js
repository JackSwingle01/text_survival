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

        // Temperature crisis warning panel
        this.renderCrisisPanel(state.temperatureCrisis);
    },

    renderCrisisPanel(crisis) {
        let panel = document.getElementById('temperatureCrisisPanel');

        if (!crisis) {
            if (panel) panel.classList.add('hidden');
            return;
        }

        if (!panel) {
            panel = document.createElement('div');
            panel.id = 'temperatureCrisisPanel';
            panel.className = 'temperature-crisis-panel';
            const sidebar = document.querySelector('.left-sidebar');
            if (sidebar) sidebar.prepend(panel);
        }

        panel.classList.remove('hidden');
        panel.replaceChildren();

        const header = document.createElement('div');
        header.className = 'crisis-header';
        header.textContent = 'Temperature Crisis';
        panel.appendChild(header);

        const stats = document.createElement('div');
        stats.className = 'crisis-stats';

        const current = document.createElement('div');
        current.textContent = `Current: ${crisis.currentTemp.toFixed(1)}°F`;
        stats.appendChild(current);

        const danger = document.createElement('div');
        danger.textContent = `Danger: <${crisis.dangerThreshold.toFixed(0)}°F`;
        stats.appendChild(danger);

        const trend = document.createElement('div');
        trend.textContent = `Trend: ${crisis.trendPerHour > 0 ? '+' : ''}${crisis.trendPerHour.toFixed(1)}°F/hr`;
        stats.appendChild(trend);

        if (crisis.minutesUntilDamage) {
            const damage = document.createElement('div');
            damage.textContent = `Damage in: ${crisis.minutesUntilDamage} min`;
            stats.appendChild(damage);
        }

        panel.appendChild(stats);

        const guidance = document.createElement('div');
        guidance.className = 'crisis-guidance';
        guidance.textContent = crisis.actionGuidance;
        panel.appendChild(guidance);
    }
};
