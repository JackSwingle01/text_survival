import { Utils } from './utils.js';

export const SurvivalDisplay = {
    render(state) {
        this.updateStatSegments('health', state.healthPercent, this.getHealthStatus);
        this.updateStatSegments('food', state.foodPercent, this.getFoodStatus);
        this.updateStatSegments('water', state.waterPercent, this.getWaterStatus);
        this.updateStatSegments('energy', state.energyPercent, this.getEnergyStatus);
    },

    updateStatSegments(stat, percent, statusFn) {
        const pctEl = document.getElementById(stat + 'Pct');
        const statusEl = document.getElementById(stat + 'Status');

        pctEl.textContent = percent + '%';
        statusEl.textContent = statusFn(percent);

        pctEl.className = 'stat-value';
        if (percent < 20) {
            pctEl.classList.add('critical');
        } else if (percent < 40) {
            pctEl.classList.add('low');
        }

        // Render segmented bar
        this.renderSegmentBar(stat + 'SegmentBar', percent);
    },

    renderSegmentBar(containerId, percent, state = 'normal') {
        const container = document.getElementById(containerId);
        Utils.clearElement(container);

        // All bars now use the simple fill style
        const fill = document.createElement('div');
        fill.className = 'bar-fill';
        fill.style.width = percent + '%';
        container.appendChild(fill);
    },

    getHealthStatus(pct) {
        if (pct >= 90) return 'Healthy';
        if (pct >= 70) return 'Fine';
        if (pct >= 50) return 'Hurt';
        if (pct >= 25) return 'Wounded';
        return 'Critical';
    },

    getFoodStatus(pct) {
        if (pct >= 80) return 'Well Fed';
        if (pct >= 60) return 'Satisfied';
        if (pct >= 40) return 'Peckish';
        if (pct >= 20) return 'Hungry';
        return 'Starving';
    },

    getWaterStatus(pct) {
        if (pct >= 80) return 'Hydrated';
        if (pct >= 60) return 'Fine';
        if (pct >= 40) return 'Thirsty';
        if (pct >= 20) return 'Parched';
        return 'Dehydrated';
    },

    getEnergyStatus(pct) {
        if (pct >= 90) return 'Energized';
        if (pct >= 80) return 'Alert';
        if (pct >= 40) return 'Normal';
        if (pct >= 30) return 'Tired';
        if (pct >= 20) return 'Very Tired';
        return 'Exhausted';
    }
};
