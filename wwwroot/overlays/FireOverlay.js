import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';

/**
 * FireOverlay - Complex dual-mode fire management interface
 * Modes: starting (select tool/tinder) and tending (add fuel)
 */
export class FireOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('fireOverlay', inputHandler);

        this.titleEl = document.getElementById('fireTitle');
        this.leftPane = document.getElementById('fireLeftPane');
        this.statusPane = document.getElementById('fireStatusPane');
        this.progressEl = document.getElementById('fireProgress');
        this.progressBar = document.getElementById('fireProgressBar');
        this.progressText = document.getElementById('fireProgressText');
        this.progressResult = document.getElementById('fireProgressResult');
        this.startBtn = document.getElementById('fireStartBtn');
        this.doneBtn = document.getElementById('fireDoneBtn');

        this.pendingFireStart = false;
    }

    render(fireData, inputId, input) {
        this.show(inputId);

        // Check if we're receiving result from a fire start attempt
        if (this.pendingFireStart) {
            this.pendingFireStart = false;
            // Show result based on whether fire is now active
            if (fireData.fire.isActive) {
                this.progressResult.textContent = 'Fire started!';
                this.progressResult.className = 'fire-progress-result success';
            } else {
                this.progressResult.textContent = 'Failed to catch...';
                this.progressResult.className = 'fire-progress-result failure';
            }
            show(this.progressResult);
            // Hide result after delay and reset progress bar
            setTimeout(() => {
                hide(this.progressEl);
                hide(this.progressResult);
                this.progressBar.style.width = '0%';
            }, 1500);
        } else {
            // Show progress bar (empty) in starting mode to prevent layout jump, hide in tending mode
            if (fireData.mode === 'starting') {
                show(this.progressEl);
                this.progressBar.style.width = '0%';
                this.progressText.textContent = '';
                hide(this.progressResult);
            } else {
                hide(this.progressEl);
            }
        }

        // Set title based on mode
        this.titleEl.textContent = fireData.mode === 'starting' ? 'START FIRE' : 'TEND FIRE';

        // Render left pane based on mode
        if (fireData.mode === 'starting') {
            this.renderStartingMode(fireData.tools, fireData.tinders, fireData.fire);
            show(this.startBtn);
            const hasTinder = fireData.tinders && fireData.tinders.length > 0;
            this.startBtn.disabled = !fireData.fire.hasKindling || !fireData.tools ||
                                     fireData.tools.length === 0 || !hasTinder;
            this.startBtn.onclick = () => this.startFireWithProgress();
        } else {
            this.renderTendingMode(fireData.fuels, fireData.fire);
            hide(this.startBtn);
        }

        // Render right pane (fire status)
        this.renderStatus(fireData.fire, fireData.mode);

        // Done button
        this.doneBtn.onclick = () => this.respond('done');
    }

    startFireWithProgress() {
        this.pendingFireStart = true;

        // Show progress bar
        show(this.progressEl);
        hide(this.progressResult);
        this.progressText.textContent = 'Starting fire...';

        // Disable buttons during animation
        this.startBtn.disabled = true;
        this.doneBtn.disabled = true;

        // Animate progress bar (~1.5 seconds)
        Animator.progressBar(this.progressBar, 1500, () => {
            // Animation complete - send response to backend
            this.respond('start_fire');
        });
    }

    renderStartingMode(tools, tinders, fire) {
        this.clear(this.leftPane);

        // Header
        const header = document.createElement('div');
        header.className = 'fire-pane-header';
        const h3 = document.createElement('h3');
        const headerIcon = document.createElement('span');
        headerIcon.className = ICON_CLASS;
        headerIcon.textContent = 'construction';
        h3.appendChild(headerIcon);
        h3.appendChild(document.createTextNode('Materials'));
        header.appendChild(h3);
        this.leftPane.appendChild(header);

        const items = document.createElement('div');
        items.className = 'fire-items';

        // Tools section
        if (tools && tools.length > 0) {
            const toolHeader = document.createElement('div');
            toolHeader.className = 'fire-section-header';
            const toolIcon = document.createElement('span');
            toolIcon.className = ICON_CLASS;
            toolIcon.textContent = 'handyman';
            toolHeader.appendChild(toolIcon);
            toolHeader.appendChild(document.createTextNode('Tools'));
            items.appendChild(toolHeader);

            for (const tool of tools) {
                const row = this.createToolRow(tool);
                items.appendChild(row);
            }
        } else {
            const noTools = document.createElement('div');
            noTools.className = 'fire-empty';
            noTools.textContent = 'No fire-making tools';
            items.appendChild(noTools);
        }

        // Tinder section
        if (tinders && tinders.length > 0) {
            const tinderHeader = document.createElement('div');
            tinderHeader.className = 'fire-section-header';
            const tinderIcon = document.createElement('span');
            tinderIcon.className = ICON_CLASS;
            tinderIcon.textContent = 'grass';
            tinderHeader.appendChild(tinderIcon);
            tinderHeader.appendChild(document.createTextNode('Tinder'));
            items.appendChild(tinderHeader);

            for (const tinder of tinders) {
                const row = this.createTinderRow(tinder);
                items.appendChild(row);
            }
        }

        // Kindling check
        const kindlingHeader = document.createElement('div');
        kindlingHeader.className = 'fire-section-header';
        const kindlingIcon = document.createElement('span');
        kindlingIcon.className = ICON_CLASS;
        kindlingIcon.textContent = 'park';
        kindlingHeader.appendChild(kindlingIcon);
        kindlingHeader.appendChild(document.createTextNode('Kindling'));
        items.appendChild(kindlingHeader);

        const kindling = document.createElement('div');
        kindling.className = 'fire-kindling-status ' + (fire.hasKindling ? 'has-kindling' : 'no-kindling');
        const kindlingStatusIcon = document.createElement('span');
        kindlingStatusIcon.className = ICON_CLASS;
        kindlingStatusIcon.textContent = fire.hasKindling ? 'check_circle' : 'cancel';
        kindling.appendChild(kindlingStatusIcon);
        kindling.appendChild(document.createTextNode(fire.hasKindling ? 'Sticks (required)' : 'No sticks (required)'));
        items.appendChild(kindling);

        this.leftPane.appendChild(items);
    }

    createToolRow(tool) {
        const row = document.createElement('div');
        row.className = 'fire-tool-item' + (tool.isSelected ? ' selected' : '');
        row.onclick = () => this.sendToolSelect(tool.id);

        const indicator = document.createElement('span');
        indicator.className = `fire-selection-indicator ${ICON_CLASS}`;
        indicator.textContent = tool.isSelected ? 'radio_button_checked' : 'radio_button_unchecked';
        row.appendChild(indicator);

        const name = document.createElement('span');
        name.className = 'fire-tool-name';
        name.textContent = tool.displayName;
        row.appendChild(name);

        const chance = document.createElement('span');
        chance.className = 'fire-tool-chance';
        chance.textContent = tool.successPercent + '%';
        row.appendChild(chance);

        return row;
    }

    createTinderRow(tinder) {
        const row = document.createElement('div');
        row.className = 'fire-tinder-item' + (tinder.isSelected ? ' selected' : '');
        row.onclick = () => this.sendTinderSelect(tinder.id);

        const indicator = document.createElement('span');
        indicator.className = `fire-selection-indicator ${ICON_CLASS}`;
        indicator.textContent = tinder.isSelected ? 'radio_button_checked' : 'radio_button_unchecked';
        row.appendChild(indicator);

        const name = document.createElement('span');
        name.className = 'fire-tinder-name';
        name.textContent = tinder.displayName;
        row.appendChild(name);

        const count = document.createElement('span');
        count.className = 'fire-tinder-count';
        count.textContent = `(${tinder.count})`;
        row.appendChild(count);

        const bonus = document.createElement('span');
        bonus.className = 'fire-tinder-bonus';
        bonus.textContent = '+' + tinder.bonusPercent + '%';
        row.appendChild(bonus);

        return row;
    }

    renderTendingMode(fuels, fire) {
        this.clear(this.leftPane);

        // Header
        const header = document.createElement('div');
        header.className = 'fire-pane-header';
        const h3 = document.createElement('h3');
        const headerIcon = document.createElement('span');
        headerIcon.className = ICON_CLASS;
        headerIcon.textContent = 'local_fire_department';
        h3.appendChild(headerIcon);
        h3.appendChild(document.createTextNode('Fuel'));
        header.appendChild(h3);
        this.leftPane.appendChild(header);

        const items = document.createElement('div');
        items.className = 'fire-items';

        if (fuels && fuels.length > 0) {
            for (const fuel of fuels) {
                const row = this.createFuelRow(fuel);
                items.appendChild(row);
            }
        } else {
            const empty = document.createElement('div');
            empty.className = 'fire-empty';
            empty.textContent = 'No fuel in inventory';
            items.appendChild(empty);
        }

        this.leftPane.appendChild(items);
    }

    createFuelRow(fuel) {
        const row = document.createElement('div');
        row.className = 'fire-fuel-item' + (fuel.canAdd ? '' : ' disabled');
        if (fuel.canAdd) {
            row.onclick = () => this.sendAddFuel(fuel.id, 1);
        }

        const icon = document.createElement('span');
        icon.className = `${ICON_CLASS} fire-item-icon`;
        icon.textContent = fuel.icon || 'local_fire_department';
        row.appendChild(icon);

        const name = document.createElement('span');
        name.className = 'fire-item-name';
        name.textContent = fuel.displayName;
        row.appendChild(name);

        const count = document.createElement('span');
        count.className = 'fire-item-count';
        count.textContent = `x${fuel.count}`;
        row.appendChild(count);

        const weight = document.createElement('span');
        weight.className = 'fire-item-weight';
        weight.textContent = fuel.weightKg >= 1
            ? `${fuel.weightKg.toFixed(1)}kg`
            : `${fuel.weightKg.toFixed(2)}kg`;
        row.appendChild(weight);

        // Burn time hint
        const burnTime = document.createElement('span');
        burnTime.className = 'fire-item-burn-time';
        if (fuel.burnTimeMinutes >= 60) {
            const hours = Math.floor(fuel.burnTimeMinutes / 60);
            const mins = fuel.burnTimeMinutes % 60;
            burnTime.textContent = mins > 0 ? `+${hours}h${mins}m` : `+${hours}h`;
        } else {
            burnTime.textContent = `+${fuel.burnTimeMinutes}m`;
        }
        row.appendChild(burnTime);

        if (fuel.canAdd) {
            const arrow = document.createElement('span');
            arrow.className = `fire-item-arrow ${ICON_CLASS}`;
            arrow.textContent = 'arrow_forward';
            row.appendChild(arrow);
        } else if (fuel.disabledReason) {
            const reason = document.createElement('span');
            reason.className = 'fire-item-reason';
            reason.textContent = fuel.disabledReason;
            reason.style.display = 'block';
            row.appendChild(reason);
        }

        return row;
    }

    renderStatus(fire, mode) {
        this.clear(this.statusPane);

        // Phase display
        const phaseDisplay = document.createElement('div');
        phaseDisplay.className = 'fire-phase-display';

        const phaseIconClass = fire.phase.toLowerCase().replace(' ', '');
        const phaseIcon = document.createElement('div');
        phaseIcon.className = `fire-phase-icon ${ICON_CLASS} ${phaseIconClass}`;
        phaseIcon.textContent = fire.phaseIcon || 'local_fire_department';
        phaseDisplay.appendChild(phaseIcon);

        const phaseName = document.createElement('div');
        phaseName.className = 'fire-phase-name ' + phaseIconClass;
        phaseName.textContent = fire.phase;
        phaseDisplay.appendChild(phaseName);

        this.statusPane.appendChild(phaseDisplay);

        // Fire stats
        if (mode === 'tending') {
            // Temperature
            this.addStatRow('thermostat', 'Temperature', `${Math.round(fire.temperatureF)}°F`, this.getTempClass(fire.temperatureF));

            // Heat output
            this.addStatRow('wb_sunny', 'Heat Output', `+${Math.round(fire.heatOutputF)}°F`);

            // Fuel gauge
            this.renderFuelGauge(fire);

            // Time remaining
            const timeClass = fire.minutesRemaining < 30 ? 'danger' : fire.minutesRemaining < 60 ? 'warning' : '';
            this.addStatRow('timer', 'Time Remaining', this.formatTime(fire.minutesRemaining), timeClass);

            // Burn rate
            this.addStatRow('speed', 'Burn Rate', `${fire.burnRateKgPerHour.toFixed(1)} kg/hr`);

            // Pit info
            this.addStatRow('circle', 'Pit Type', fire.pitType);

            // Charcoal
            if (fire.charcoalKg > 0) {
                this.addStatRow('whatshot', 'Charcoal', `${fire.charcoalKg.toFixed(2)} kg`);
            }
        } else {
            // Starting mode - show pit info and success chance
            this.addStatRow('circle', 'Pit Type', fire.pitType);
            this.addStatRow('air', 'Wind Protection', `${Math.round(fire.windProtection * 100)}%`);
            this.addStatRow('eco', 'Fuel Efficiency', `+${Math.round((fire.fuelEfficiency - 1) * 100)}%`);

            // Success chance section
            const successSection = document.createElement('div');
            successSection.className = 'fire-success-section';

            const successLabel = document.createElement('div');
            successLabel.className = 'fire-success-label';
            successLabel.textContent = 'Success Chance';
            successSection.appendChild(successLabel);

            const successValue = document.createElement('div');
            successValue.className = 'fire-success-value';
            successValue.textContent = fire.finalSuccessPercent + '%';
            successSection.appendChild(successValue);

            this.statusPane.appendChild(successSection);
        }
    }

    renderFuelGauge(fire) {
        const gauge = document.createElement('div');
        gauge.className = 'fire-fuel-gauge';

        const gaugeLabel = document.createElement('div');
        gaugeLabel.className = 'fire-gauge-label';
        const gaugeLabelLeft = document.createElement('span');
        gaugeLabelLeft.textContent = 'Fuel';
        const gaugeLabelRight = document.createElement('span');
        gaugeLabelRight.textContent = `${fire.totalKg.toFixed(1)} / ${fire.maxCapacityKg.toFixed(0)} kg`;
        gaugeLabel.appendChild(gaugeLabelLeft);
        gaugeLabel.appendChild(gaugeLabelRight);
        gauge.appendChild(gaugeLabel);

        const gaugeBar = document.createElement('div');
        gaugeBar.className = 'fire-gauge-bar';

        const fillPct = (fire.totalKg / fire.maxCapacityKg) * 100;
        const burningPct = (fire.burningKg / fire.maxCapacityKg) * 100;

        const unburnedFill = document.createElement('div');
        unburnedFill.className = 'fire-gauge-fill unburned';
        unburnedFill.style.width = fillPct + '%';
        gaugeBar.appendChild(unburnedFill);

        const burningFill = document.createElement('div');
        burningFill.className = 'fire-gauge-fill burning';
        burningFill.style.width = burningPct + '%';
        gaugeBar.appendChild(burningFill);

        gauge.appendChild(gaugeBar);
        this.statusPane.appendChild(gauge);
    }

    addStatRow(icon, label, value, valueClass = '') {
        const row = document.createElement('div');
        row.className = 'fire-stat-row';

        const labelEl = document.createElement('span');
        labelEl.className = 'fire-stat-label';
        const iconEl = document.createElement('span');
        iconEl.className = ICON_CLASS;
        iconEl.textContent = icon;
        labelEl.appendChild(iconEl);
        labelEl.appendChild(document.createTextNode(label));
        row.appendChild(labelEl);

        const valueEl = document.createElement('span');
        valueEl.className = 'fire-stat-value' + (valueClass ? ' ' + valueClass : '');
        valueEl.textContent = value;
        row.appendChild(valueEl);

        this.statusPane.appendChild(row);
    }

    getTempClass(temp) {
        if (temp < 200) return 'danger';
        if (temp < 400) return 'warning';
        return 'good';
    }

    formatTime(minutes) {
        if (minutes < 60) return `${minutes} min`;
        const hours = Math.floor(minutes / 60);
        const mins = minutes % 60;
        return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
    }

    sendToolSelect(toolId) {
        this.inputHandler.sendAction('fire', { fireToolId: toolId }, this.inputId);
    }

    sendTinderSelect(tinderId) {
        this.inputHandler.sendAction('fire', { tinderId: tinderId }, this.inputId);
    }

    sendAddFuel(fuelId, count) {
        this.inputHandler.sendAction('fire', { fuelItemId: fuelId, fuelCount: count }, this.inputId);
    }

    cleanup() {
        this.pendingFireStart = false;
        this.clear(this.leftPane);
        this.clear(this.statusPane);
        hide(this.progressEl);
        hide(this.progressResult);
    }
}
