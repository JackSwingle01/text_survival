import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
import { DOMBuilder, paneHeader } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';
import { ItemList } from '../components/ItemList.js';
import { FireRowBuilders } from '../components/rowBuilders.js';

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
            this.renderTendingMode(fireData.fuels, fireData.emberCarriers, fireData.fire);
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

        // Header using paneHeader helper
        const header = paneHeader({
            title: 'Materials',
            icon: 'construction'
        });
        this.leftPane.appendChild(header.build());

        const items = document.createElement('div');
        items.className = 'fire-items';

        // Use ItemList for tools and tinders
        const itemList = new ItemList({
            container: items,
            onItemClick: (item) => {
                // Determine if it's a tool or tinder based on which property exists
                if (item.successPercent !== undefined) {
                    this.sendToolSelect(item.id);
                } else if (item.bonusPercent !== undefined) {
                    this.sendTinderSelect(item.id);
                }
            },
            rowBuilder: null  // Will be set per section
        });

        // Tools section
        itemList.rowBuilder = FireRowBuilders.tool;
        itemList.render([{
            header: tools && tools.length > 0 ? { text: 'Tools', icon: 'handyman' } : null,
            items: tools || [],
            emptyMessage: tools && tools.length === 0 ? 'No fire-making tools' : null
        }]);

        // Tinders section
        itemList.rowBuilder = FireRowBuilders.tinder;
        itemList.render([{
            header: tinders && tinders.length > 0 ? { text: 'Tinder', icon: 'grass' } : null,
            items: tinders || []
        }]);

        // Kindling check (unique to fire overlay, not using ItemList)
        const kindlingHeader = document.createElement('div');
        kindlingHeader.className = 'section-header';
        const kindlingIcon = document.createElement('span');
        kindlingIcon.className = ICON_CLASS;
        kindlingIcon.textContent = 'park';
        kindlingHeader.appendChild(kindlingIcon);
        kindlingHeader.appendChild(document.createTextNode('Kindling'));
        items.appendChild(kindlingHeader);

        const kindling = document.createElement('div');
        kindling.className = fire.hasKindling ? 'fire-kindling-status--has' : 'fire-kindling-status--missing';
        const kindlingStatusIcon = document.createElement('span');
        kindlingStatusIcon.className = ICON_CLASS;
        kindlingStatusIcon.textContent = fire.hasKindling ? 'check_circle' : 'cancel';
        kindling.appendChild(kindlingStatusIcon);
        kindling.appendChild(document.createTextNode(fire.hasKindling ? 'Sticks (required)' : 'No sticks (required)'));
        items.appendChild(kindling);

        this.leftPane.appendChild(items);
    }

    renderTendingMode(fuels, emberCarriers, fire) {
        this.clear(this.leftPane);

        // Header using paneHeader helper
        const header = paneHeader({
            title: 'Fuel',
            icon: 'local_fire_department'
        });
        this.leftPane.appendChild(header.build());

        const items = document.createElement('div');
        items.className = 'fire-items';

        // Use ItemList for fuels
        const fuelList = new ItemList({
            container: items,
            onItemClick: (fuel) => this.sendAddFuel(fuel.id, 1),
            rowBuilder: FireRowBuilders.fuel
        });

        fuelList.render([{
            items: fuels || [],
            emptyMessage: !fuels || fuels.length === 0 ? 'No fuel in inventory' : null
        }]);

        // Add ember carriers section if any exist
        if (emberCarriers && emberCarriers.length > 0) {
            const carrierHeader = paneHeader({
                title: 'Ember Carriers',
                icon: 'fireplace'
            });
            items.appendChild(carrierHeader.build());

            const carrierList = new ItemList({
                container: items,
                onItemClick: (carrier) => {
                    if (!carrier.isLit) {
                        this.sendLightEmberCarrier(carrier.id);
                    }
                },
                rowBuilder: FireRowBuilders.emberCarrier
            });

            carrierList.render([{
                items: emberCarriers,
                emptyMessage: null
            }]);
        }

        this.leftPane.appendChild(items);
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
        const row = this.createStatRow(label, value, {
            icon: icon,
            valueClass: valueClass
        });
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
        this.sendAction('fire', { fireToolId: toolId });
    }

    sendTinderSelect(tinderId) {
        this.sendAction('fire', { tinderId: tinderId });
    }

    sendAddFuel(fuelId, count) {
        this.sendAction('fire', { fuelItemId: fuelId, fuelCount: count });
    }

    sendLightEmberCarrier(carrierId) {
        this.sendAction('fire', { emberCarrierId: carrierId });
    }

    cleanup() {
        // Don't reset pendingFireStart here - it needs to survive hide/show cycle
        // Flag is reset in render() after showing the result (line 34)
        this.clear(this.leftPane);
        this.clear(this.statusPane);
        hide(this.progressEl);
        hide(this.progressResult);
    }
}
