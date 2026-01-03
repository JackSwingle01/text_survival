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

        // Determine severity class
        let severity = 'good';
        if (percent < 30) severity = 'danger';
        else if (percent < 60) severity = 'warning';

        // Update value color class
        pctEl.className = 'stat-value ' + severity;

        // Update bar color for health only
        if (stat === 'health') {
            barEl.classList.remove('good', 'warning', 'danger');
            barEl.classList.add(severity);
        }
    }
};
