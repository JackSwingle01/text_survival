export const SurvivalDisplay = {
    render(state) {
        this.updateStat('health', state.healthPercent);
        this.updateStat('food', state.foodPercent);
        this.updateStat('water', state.waterPercent);
        this.updateStat('energy', state.energyPercent);
    },

    updateStat(stat, percent) {
        const pctEl = document.getElementById(stat + 'Pct');
        const barEl = document.getElementById(stat + 'Bar');

        if (!pctEl || !barEl) return;

        pctEl.textContent = percent + '%';
        barEl.style.width = percent + '%';

        // Update value color class
        pctEl.className = 'stat-value';
        if (percent >= 60) {
            pctEl.classList.add('good');
        } else if (percent >= 30) {
            pctEl.classList.add('warning');
        } else {
            pctEl.classList.add('danger');
        }
    }
};
