import { show, hide, clear } from '../lib/helpers.js';

export const EffectsDisplay = {
    render(effects) {
        const container = document.getElementById('effectsList');
        const section = container?.closest('.panel');

        // Hide entire section if no effects
        if (!effects || effects.length === 0) {
            hide(section);
            return;
        }

        // Show section
        show(section);
        clear(container);

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

            // Add tooltip using pre-computed lines from server
            if (e.tooltipLines && e.tooltipLines.length > 0) {
                const tooltip = document.createElement('div');
                tooltip.className = 'effect-tooltip';
                e.tooltipLines.forEach((line, i) => {
                    if (i > 0) tooltip.appendChild(document.createElement('br'));
                    tooltip.appendChild(document.createTextNode(line));
                });
                div.appendChild(tooltip);
                div.classList.add('has-tooltip');
            }

            container.appendChild(div);
        });
    },

    renderInjuries(injuries, bloodPercent) {
        const container = document.getElementById('injuriesList');
        const section = container?.closest('.panel');

        const hasBloodLoss = bloodPercent && bloodPercent < 95;
        const hasInjuries = injuries && injuries.length > 0;

        // Hide entire section if no injuries
        if (!hasBloodLoss && !hasInjuries) {
            hide(section);
            return;
        }

        // Show section
        show(section);
        clear(container);

        if (hasBloodLoss) {
            const bloodLossPercent = 100 - bloodPercent;
            const div = document.createElement('div');
            div.className = `injury-item ${this.getInjurySeverityClass(bloodPercent)} has-tooltip`;

            // Name
            const nameSpan = document.createElement('span');
            nameSpan.className = 'injury-name';
            nameSpan.textContent = 'Blood loss';
            div.appendChild(nameSpan);

            // Bar
            const barContainer = document.createElement('div');
            barContainer.className = 'injury-bar-container';
            const bar = document.createElement('div');
            bar.className = 'injury-bar';
            bar.style.width = `${bloodLossPercent}%`;
            barContainer.appendChild(bar);
            div.appendChild(barContainer);

            // Tooltip - blood loss affects consciousness, moving, manipulation
            const tooltip = document.createElement('div');
            tooltip.className = 'effect-tooltip';
            tooltip.textContent = 'Consciousness, Moving, Manipulation';
            div.appendChild(tooltip);

            container.appendChild(div);
        }

        if (hasInjuries) {
            injuries.forEach(i => {
                const div = document.createElement('div');
                // Use pre-computed severity class from server
                div.className = `injury-item ${i.severityClass}`;

                // Name
                const nameSpan = document.createElement('span');
                nameSpan.className = 'injury-name';
                nameSpan.textContent = i.isOrgan ? `${i.partName} (organ)` : i.partName;
                div.appendChild(nameSpan);

                // Bar (using damagePercent)
                const barContainer = document.createElement('div');
                barContainer.className = 'injury-bar-container';
                const bar = document.createElement('div');
                bar.className = 'injury-bar';
                bar.style.width = `${i.damagePercent}%`;
                barContainer.appendChild(bar);
                div.appendChild(barContainer);

                // Tooltip with capacity impacts
                const tooltip = this.createInjuryTooltip(i);
                if (tooltip) {
                    div.appendChild(tooltip);
                    div.classList.add('has-tooltip');
                }

                container.appendChild(div);
            });
        }
    },

    createInjuryTooltip(injury) {
        const lines = [];

        // Capacity impacts (same format as effects)
        if (injury.capacityImpacts) {
            for (const [cap, impact] of Object.entries(injury.capacityImpacts)) {
                if (impact !== 0) {
                    const sign = impact > 0 ? '+' : '';
                    lines.push(`${cap}: ${sign}${impact}%`);
                }
            }
        }

        if (lines.length === 0) return null;

        const tooltip = document.createElement('div');
        tooltip.className = 'effect-tooltip';
        lines.forEach((line, i) => {
            if (i > 0) tooltip.appendChild(document.createElement('br'));
            tooltip.appendChild(document.createTextNode(line));
        });
        return tooltip;
    },

    // Still needed for blood loss (not sent from server) and capacities
    getInjurySeverityClass(percent) {
        if (percent <= 20) return 'critical';
        if (percent <= 50) return 'severe';
        if (percent <= 70) return 'moderate';
        return 'minor';
    },

    renderCapacities(capacities) {
        const container = document.getElementById('capacitiesList');
        const section = container?.closest('.panel');

        // Filter to only impaired capacities (< 100%)
        const impaired = Object.entries(capacities || {})
            .filter(([_, pct]) => pct < 100)
            .sort((a, b) => a[1] - b[1]); // Worst first

        // Hide section if all capacities are at 100%
        if (impaired.length === 0) {
            hide(section);
            return;
        }

        show(section);
        clear(container);

        impaired.forEach(([name, pct]) => {
            const div = document.createElement('div');
            // Reuse injury classes - severity based on capacity level
            div.className = `injury-item ${this.getInjurySeverityClass(pct)}`;

            const nameSpan = document.createElement('span');
            nameSpan.className = 'injury-name';
            nameSpan.textContent = name;
            div.appendChild(nameSpan);

            const barContainer = document.createElement('div');
            barContainer.className = 'injury-bar-container';
            const bar = document.createElement('div');
            bar.className = 'injury-bar';
            bar.style.width = `${pct}%`; // Show capacity level
            barContainer.appendChild(bar);
            div.appendChild(barContainer);

            const pctSpan = document.createElement('span');
            pctSpan.className = 'injury-pct';
            pctSpan.textContent = `${pct}%`;
            div.appendChild(pctSpan);

            container.appendChild(div);
        });
    }
};