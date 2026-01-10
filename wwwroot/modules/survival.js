export const SurvivalDisplay = {
    render(state) {
        // Use pre-computed display values from server
        this.updateStat('health', state.healthDisplay, true);
        this.updateStat('food', state.foodDisplay, false);
        this.updateStat('water', state.waterDisplay, false);
        this.updateStat('energy', state.energyDisplay, false);
    },

    updateStat(stat, display, showBarColor) {
        const pctEl = document.getElementById(stat + 'Pct');
        const barEl = document.getElementById(stat + 'Bar');

        if (!pctEl || !barEl) return;

        pctEl.textContent = display.value + '%';
        barEl.style.width = display.value + '%';

        // Use server-provided severity class
        pctEl.className = 'stat-value ' + display.severity;

        // Update bar color for health only
        if (showBarColor) {
            barEl.classList.remove('good', 'warning', 'danger');
            barEl.classList.add(display.severity);
        }
    }
};
