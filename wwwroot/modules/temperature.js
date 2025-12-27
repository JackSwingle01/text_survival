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

        // Air temperature display
        const airTempEl = document.getElementById('airTempDisplay');
        if (state.fireHeat > 0) {
            airTempEl.textContent = `${state.airTemp.toFixed(0)}°F (+${state.fireHeat.toFixed(0)}°)`;
        } else {
            airTempEl.textContent = `${state.airTemp.toFixed(0)}°F`;
        }
    }
};
