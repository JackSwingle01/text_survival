import { Utils, show, hide } from './utils.js';

export const EffectsDisplay = {
    render(effects) {
        const container = document.getElementById('effectsList');
        const section = container?.closest('.status-section');

        // Hide entire section if no effects
        if (!effects || effects.length === 0) {
            hide(section);
            return;
        }

        // Show section
        show(section);
        Utils.clearElement(container);

        effects.forEach(e => {
            const div = document.createElement('div');
            div.className = `effect-item ${e.trend}`;

            const nameSpan = document.createElement('span');
            nameSpan.className = 'effect-name';
            nameSpan.textContent = e.name;
            div.appendChild(nameSpan);

            // Severity bar
            const barContainer = document.createElement('div');
            barContainer.className = 'effect-bar-container';
            const bar = document.createElement('div');
            bar.className = 'effect-bar';
            bar.style.width = `${e.severityPercent}%`;
            barContainer.appendChild(bar);
            div.appendChild(barContainer);

            // Horizontal trend arrow: ← improving, → worsening, - stable
            const trendSpan = document.createElement('span');
            trendSpan.className = 'effect-trend';
            const trend = e.trend === 'worsening' ? '→' : e.trend === 'improving' ? '←' : '-';
            trendSpan.textContent = trend;
            div.appendChild(trendSpan);

            // Add tooltip
            const tooltip = this.createEffectTooltip(e);
            if (tooltip) {
                div.appendChild(tooltip);
                div.classList.add('has-tooltip');
            }

            container.appendChild(div);
        });
    },

    createEffectTooltip(effect) {
        const lines = [];

        // Capacity impacts
        if (effect.capacityImpacts) {
            for (const [cap, impact] of Object.entries(effect.capacityImpacts)) {
                const sign = impact > 0 ? '+' : '';
                lines.push(`${cap}: ${sign}${impact}%`);
            }
        }

        // Stat impacts
        if (effect.statsImpact) {
            const s = effect.statsImpact;
            if (s.temperaturePerHour) {
                const sign = s.temperaturePerHour > 0 ? '+' : '';
                lines.push(`Temp: ${sign}${s.temperaturePerHour.toFixed(1)}\u00B0F/hr`);
            }
            if (s.hydrationPerHour) {
                const sign = s.hydrationPerHour > 0 ? '+' : '';
                lines.push(`Hydration: ${sign}${s.hydrationPerHour.toFixed(0)}ml/hr`);
            }
            if (s.caloriesPerHour) {
                const sign = s.caloriesPerHour > 0 ? '+' : '';
                lines.push(`Calories: ${sign}${s.caloriesPerHour.toFixed(0)}/hr`);
            }
            if (s.energyPerHour) {
                const sign = s.energyPerHour > 0 ? '+' : '';
                lines.push(`Energy: ${sign}${s.energyPerHour.toFixed(0)}/hr`);
            }
            if (s.damagePerHour) {
                lines.push(`${s.damageType || 'Damage'}: ${s.damagePerHour.toFixed(1)}/hr`);
            }
        }

        // Treatment status
        if (effect.requiresTreatment) {
            lines.push('Requires treatment');
        }

        if (lines.length === 0) return null;

        const tooltip = document.createElement('div');
        tooltip.className = 'effect-tooltip';
        // Use safe DOM methods instead of innerHTML
        lines.forEach((line, i) => {
            if (i > 0) tooltip.appendChild(document.createElement('br'));
            tooltip.appendChild(document.createTextNode(line));
        });
        return tooltip;
    },

    renderInjuries(injuries, bloodPercent) {
        const container = document.getElementById('injuriesList');
        const section = container?.closest('.status-section');

        const hasBloodLoss = bloodPercent && bloodPercent < 95;
        const hasInjuries = injuries && injuries.length > 0;

        // Hide entire section if no injuries
        if (!hasBloodLoss && !hasInjuries) {
            hide(section);
            return;
        }

        // Show section
        show(section);
        Utils.clearElement(container);

        if (hasBloodLoss) {
            const div = document.createElement('div');
            div.className = `injury-item ${this.getInjurySeverityClass(bloodPercent)} has-tooltip`;
            div.textContent = 'Blood loss ';
            const pctSpan = document.createElement('span');
            pctSpan.className = 'injury-pct';
            pctSpan.textContent = `(${100 - bloodPercent}%)`;
            div.appendChild(pctSpan);

            // Blood loss tooltip
            const tooltip = document.createElement('div');
            tooltip.className = 'effect-tooltip';
            tooltip.textContent = 'Affects: Consciousness, Moving, Manipulation';
            div.appendChild(tooltip);

            container.appendChild(div);
        }

        if (hasInjuries) {
            injuries.forEach(i => {
                const div = document.createElement('div');
                div.className = `injury-item ${this.getInjurySeverityClass(i.conditionPercent)}`;
                const label = i.isOrgan ? `${i.partName} (organ) ` : `${i.partName} `;
                div.textContent = label;
                const pctSpan = document.createElement('span');
                pctSpan.className = 'injury-pct';
                pctSpan.textContent = `(${100 - i.conditionPercent}%)`;
                div.appendChild(pctSpan);

                // Add tooltip for affected capacities
                if (i.affectedCapacities && i.affectedCapacities.length > 0) {
                    const tooltip = document.createElement('div');
                    tooltip.className = 'effect-tooltip';
                    tooltip.textContent = `Affects: ${i.affectedCapacities.join(', ')}`;
                    div.appendChild(tooltip);
                    div.classList.add('has-tooltip');
                }

                container.appendChild(div);
            });
        }
    },

    getInjurySeverityClass(percent) {
        if (percent <= 20) return 'critical';
        if (percent <= 50) return 'severe';
        if (percent <= 70) return 'moderate';
        return 'minor';
    }
};
