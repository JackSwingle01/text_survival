// overlays/FireOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear, show, hide } from '../lib/helpers.js';
import { Icon, ICON_CLASS } from '../lib/components/Icon.js';
import { FuelGauge } from '../lib/components/Gauge.js';
import { ItemList } from '../components/ItemList.js';
import { FireRowBuilders } from '../components/rowBuilders.js';
import { Animator } from '../core/Animator.js';

/**
 * FireOverlay - Complex dual-mode fire management interface
 * Modes: starting (select tool/tinder) and tending (add fuel)
 */
export class FireOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('fireOverlay', inputHandler);
        this.pendingFireStart = false;
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        const progressEl = this.$('#fireProgress');
        const progressBar = this.$('#fireProgressBar');
        const progressResult = this.$('#fireProgressResult');
        const startBtn = this.$('#fireStartBtn');
        const doneBtn = this.$('#fireDoneBtn');

        // Handle fire start result
        if (this.pendingFireStart) {
            this.pendingFireStart = false;
            if (data.fire.isActive) {
                progressResult.textContent = 'Fire started!';
                progressResult.className = 'fire-progress-result success';
            } else {
                progressResult.textContent = 'Failed to catch...';
                progressResult.className = 'fire-progress-result failure';
            }
            show(progressResult);
            setTimeout(() => {
                hide(progressEl);
                hide(progressResult);
                progressBar.style.width = '0%';
            }, 1500);
        } else {
            if (data.mode === 'starting') {
                show(progressEl);
                progressBar.style.width = '0%';
                this.$('#fireProgressText').textContent = '';
                hide(progressResult);
            } else {
                hide(progressEl);
            }
        }

        // Title
        const titleEl = this.$('#fireTitle');
        if (titleEl) {
            titleEl.textContent = data.mode === 'starting' ? 'START FIRE' : 'TEND FIRE';
        }

        // Left pane content based on mode
        if (data.mode === 'starting') {
            this.renderStartingMode(data, progressEl, progressBar, startBtn, doneBtn);
        } else {
            this.renderTendingMode(data);
            hide(startBtn);
        }

        // Right pane (status)
        this.renderStatus(data.fire, data.mode);

        // Done button
        doneBtn.onclick = () => this.respond('done');
    }

    renderStartingMode(data, progressEl, progressBar, startBtn, doneBtn) {
        const leftPane = this.$('#fireLeftPane');
        clear(leftPane);

        // Header
        leftPane.appendChild(this.buildPaneHeader('Materials', 'construction'));

        const items = div({ className: 'fire-items' });

        // Tools section
        const toolList = new ItemList({
            container: items,
            onItemClick: (item) => this.sendAction('fire', { fireToolId: item.id }),
            rowBuilder: FireRowBuilders.tool
        });

        toolList.render([{
            header: data.tools?.length > 0 ? { text: 'Tools', icon: 'handyman' } : null,
            items: data.tools || [],
            emptyMessage: data.tools?.length === 0 ? 'No fire-making tools' : null
        }]);

        // Tinders section
        const tinderList = new ItemList({
            container: items,
            onItemClick: (item) => this.sendAction('fire', { tinderId: item.id }),
            rowBuilder: FireRowBuilders.tinder
        });

        tinderList.render([{
            header: data.tinders?.length > 0 ? { text: 'Tinder', icon: 'grass' } : null,
            items: data.tinders || []
        }]);

        // Kindling section
        items.appendChild(this.buildSectionHeader('Kindling', 'park'));
        items.appendChild(this.buildKindlingStatus(data.fire.hasKindling));

        leftPane.appendChild(items);

        // Start button
        show(startBtn);
        const hasTinder = data.tinders?.length > 0;
        startBtn.disabled = !data.fire.hasKindling || !data.tools?.length || !hasTinder;
        startBtn.onclick = () => this.startFireWithProgress(progressEl, progressBar, startBtn, doneBtn);
    }

    renderTendingMode(data) {
        const leftPane = this.$('#fireLeftPane');
        clear(leftPane);

        // Header
        leftPane.appendChild(this.buildPaneHeader('Fuel', 'local_fire_department'));

        const items = div({ className: 'fire-items' });

        // Fuels section
        const fuelList = new ItemList({
            container: items,
            onItemClick: (fuel) => this.sendAction('fire', { fuelItemId: fuel.id, fuelCount: 1 }),
            rowBuilder: FireRowBuilders.fuel
        });

        fuelList.render([{
            items: data.fuels || [],
            emptyMessage: !data.fuels?.length ? 'No fuel in inventory' : null
        }]);

        // Ember carriers section
        if (data.emberCarriers?.length > 0) {
            items.appendChild(this.buildPaneHeader('Ember Carriers', 'fireplace'));

            const carrierList = new ItemList({
                container: items,
                onItemClick: (carrier) => {
                    if (!carrier.isLit) {
                        this.sendAction('fire', { emberCarrierId: carrier.id });
                    }
                },
                rowBuilder: FireRowBuilders.emberCarrier
            });

            carrierList.render([{ items: data.emberCarriers }]);
        }

        leftPane.appendChild(items);
    }

    renderStatus(fire, mode) {
        const statusPane = this.$('#fireStatusPane');
        clear(statusPane);

        // Phase display
        const phaseClass = fire.phase.toLowerCase().replace(' ', '');
        statusPane.appendChild(
            div({ className: 'fire-phase-display' },
                div({ className: `fire-phase-icon ${ICON_CLASS} ${phaseClass}` }, fire.phaseIcon || 'local_fire_department'),
                div({ className: `fire-phase-name ${phaseClass}` }, fire.phase)
            )
        );

        if (mode === 'tending') {
            // Temperature
            this.addStatRow(statusPane, 'thermostat', 'Temperature', `${Math.round(fire.temperatureF)}°F`, this.getTempClass(fire.temperatureF));

            // Heat output
            this.addStatRow(statusPane, 'wb_sunny', 'Heat Output', `+${Math.round(fire.heatOutputF)}°F`);

            // Fuel gauge
            statusPane.appendChild(FuelGauge({
                totalKg: fire.totalKg,
                burningKg: fire.burningKg,
                maxCapacityKg: fire.maxCapacityKg
            }));

            // Time remaining
            const timeClass = fire.minutesRemaining < 30 ? 'danger' : fire.minutesRemaining < 60 ? 'warning' : '';
            this.addStatRow(statusPane, 'timer', 'Time Remaining', this.formatTime(fire.minutesRemaining), timeClass);

            // Burn rate
            this.addStatRow(statusPane, 'speed', 'Burn Rate', `${fire.burnRateKgPerHour.toFixed(1)} kg/hr`);

            // Pit info
            this.addStatRow(statusPane, 'circle', 'Pit Type', fire.pitType);

            // Charcoal
            if (fire.charcoalKg > 0) {
                this.addStatRow(statusPane, 'whatshot', 'Charcoal', `${fire.charcoalKg.toFixed(2)} kg`);
            }
        } else {
            // Starting mode
            this.addStatRow(statusPane, 'circle', 'Pit Type', fire.pitType);
            this.addStatRow(statusPane, 'air', 'Wind Protection', `${Math.round(fire.windProtection * 100)}%`);
            this.addStatRow(statusPane, 'eco', 'Fuel Efficiency', `+${Math.round((fire.fuelEfficiency - 1) * 100)}%`);

            // Success chance
            statusPane.appendChild(this.buildSuccessSection(fire));
        }
    }

    buildPaneHeader(title, iconName) {
        return div({ className: 'pane-header' },
            Icon(iconName, 'pane-header__icon'),
            span({ className: 'pane-header__title' }, title)
        );
    }

    buildSectionHeader(text, iconName) {
        return div({ className: 'section-header' },
            Icon(iconName),
            text
        );
    }

    buildKindlingStatus(hasKindling) {
        return div({ className: hasKindling ? 'fire-kindling-status--has' : 'fire-kindling-status--missing' },
            Icon(hasKindling ? 'check_circle' : 'cancel'),
            hasKindling ? 'Sticks (required)' : 'No sticks (required)'
        );
    }

    buildSuccessSection(fire) {
        const section = div({ className: 'fire-success-section' },
            div({ className: 'fire-success-label' }, 'Success Chance'),
            div({ className: 'fire-success-value' }, `${fire.finalSuccessPercent}%`)
        );

        if (fire.modifiers?.length > 0) {
            const modifiersEl = div({ className: 'fire-modifiers' });
            fire.modifiers.forEach(mod => {
                modifiersEl.appendChild(
                    div({ className: `fire-modifier-row ${mod.percentDelta >= 0 ? 'bonus' : 'penalty'}` },
                        Icon(mod.icon),
                        span({ className: 'fire-modifier-name' }, mod.name),
                        span({ className: 'fire-modifier-value' }, `${mod.percentDelta >= 0 ? '+' : ''}${mod.percentDelta}%`)
                    )
                );
            });
            section.appendChild(modifiersEl);
        }

        return section;
    }

    addStatRow(container, iconName, label, value, valueClass = '') {
        container.appendChild(
            div({ className: 'fire-stat-row' },
                span({ className: 'fire-stat-label' },
                    Icon(iconName),
                    label
                ),
                span({ className: `fire-stat-value ${valueClass}`.trim() }, value)
            )
        );
    }

    startFireWithProgress(progressEl, progressBar, startBtn, doneBtn) {
        this.pendingFireStart = true;

        show(progressEl);
        hide(this.$('#fireProgressResult'));
        this.$('#fireProgressText').textContent = 'Starting fire...';

        startBtn.disabled = true;
        doneBtn.disabled = true;

        Animator.progressBar(progressBar, 1500, () => {
            this.respond('start_fire');
        });
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

    hide() {
        super.hide();
        const progressEl = this.$('#fireProgress');
        const progressResult = this.$('#fireProgressResult');
        if (progressEl) hide(progressEl);
        if (progressResult) hide(progressResult);
    }
}
