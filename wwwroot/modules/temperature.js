import { Utils } from './utils.js';

export const TemperatureDisplay = {
    render(state) {
        const bodyTemp = state.bodyTemp;
        const feelsLike = state.airTemp + (state.fireHeat || 0);

        // Temperature badge (feels like temp - prominent display)
        const tempBadge = document.getElementById('tempBadge');
        const tempBadgeValue = document.getElementById('tempBadgeValue');
        tempBadgeValue.textContent = `${feelsLike.toFixed(0)}°F`;

        // Set badge color class based on feels like temp
        tempBadge.className = 'temp-badge';
        if (feelsLike < 20) tempBadge.classList.add('danger');
        else if (feelsLike < 40) tempBadge.classList.add('cold');
        else if (feelsLike < 60) tempBadge.classList.add('cool');
        else if (feelsLike < 80) tempBadge.classList.add('normal');
        else tempBadge.classList.add('hot');

        // Temperature segmented bar (87-102 range)
        const tempPct = Math.max(0, Math.min(100, (bodyTemp - 87) / (102 - 87) * 100));
        let tempState = 'normal';
        if (bodyTemp < 95) tempState = 'cold';
        else if (bodyTemp < 97) tempState = 'cool';
        else if (bodyTemp > 100) tempState = 'hot';
        this.renderSegmentBar('tempSegmentBar', tempPct, tempState);

        document.getElementById('bodyTempDisplay').textContent = `${bodyTemp.toFixed(1)}°F`;

        const statusEl = document.getElementById('tempStatus');
        statusEl.textContent = state.tempStatus;
        statusEl.className = 'temp-status ' + state.tempStatus.toLowerCase();

        // Air breakdown
        document.getElementById('airTempDisplay').textContent = `${state.airTemp.toFixed(0)}°F`;

        const fireContrib = document.getElementById('fireContrib');
        if (state.fireHeat > 0) {
            fireContrib.textContent = ` + Fire +${state.fireHeat.toFixed(0)}°F`;
        } else {
            fireContrib.textContent = '';
        }

        // Trend
        const trendEl = document.getElementById('tempTrend');
        const rate = state.trendPerHour;
        if (Math.abs(rate) < 0.05) {
            trendEl.textContent = '→ Stable';
            trendEl.className = 'temp-trend stable';
        } else if (rate < 0) {
            trendEl.textContent = `↓ Cooling (${rate.toFixed(1)}°/hr)`;
            trendEl.className = 'temp-trend cooling';
        } else {
            trendEl.textContent = `↑ Warming (+${rate.toFixed(1)}°/hr)`;
            trendEl.className = 'temp-trend warming';
        }
    },

    renderSegmentBar(containerId, percent, state = 'normal') {
        const container = document.getElementById(containerId);
        Utils.clearElement(container);

        // All bars now use the simple fill style
        const fill = document.createElement('div');
        fill.className = 'bar-fill';
        fill.style.width = percent + '%';
        container.appendChild(fill);
    }
};
