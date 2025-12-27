import { Utils } from './utils.js';

export const EffectsDisplay = {
    render(effects) {
        const container = document.getElementById('effectsList');
        Utils.clearElement(container);

        if (!effects || effects.length === 0) {
            const none = document.createElement('div');
            none.className = 'effect-item';
            none.textContent = 'None';
            container.appendChild(none);
            return;
        }

        effects.forEach(e => {
            const div = document.createElement('div');
            div.className = `effect-item ${e.trend}`;

            const nameSpan = document.createElement('span');
            nameSpan.textContent = e.name;
            div.appendChild(nameSpan);

            const rightSpan = document.createElement('span');
            const sevSpan = document.createElement('span');
            sevSpan.className = 'effect-severity';
            sevSpan.textContent = `${e.severityPercent}%`;
            rightSpan.appendChild(sevSpan);

            const trend = e.trend === 'worsening' ? '↑' : e.trend === 'improving' ? '↓' : '';
            if (trend) {
                rightSpan.appendChild(document.createTextNode(trend));
            }
            div.appendChild(rightSpan);

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
        Utils.clearElement(container);

        const hasBloodLoss = bloodPercent && bloodPercent < 95;
        const hasInjuries = injuries && injuries.length > 0;

        if (!hasBloodLoss && !hasInjuries) {
            const none = document.createElement('div');
            none.className = 'injury-item';
            none.textContent = 'None';
            container.appendChild(none);
            return;
        }

        if (hasBloodLoss) {
            const div = document.createElement('div');
            div.className = `injury-item ${this.getInjurySeverityClass(bloodPercent)} has-tooltip`;
            div.textContent = 'Blood loss ';
            const pctSpan = document.createElement('span');
            pctSpan.className = 'injury-pct';
            pctSpan.textContent = `(${bloodPercent}%)`;
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
                pctSpan.textContent = `(${i.conditionPercent}%)`;
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
