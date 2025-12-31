// modules/overlays/EventOverlay.js
import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder, icon } from '../core/DOMBuilder.js';
import { StatRow } from '../components/StatRow.js';
import { Animator } from '../core/Animator.js';
import { Utils, show, hide } from '../modules/utils.js';

export class EventOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('eventOverlay', inputHandler);
    }

    render(eventData, inputId) {
        this.show(inputId);

        this.$('#eventName').textContent = eventData.name;

        if (eventData.outcome) {
            this.renderOutcome(eventData);
        } else {
            this.renderEventChoices(eventData);
        }
    }

    renderEventChoices(eventData) {
        this.$('#eventDescription').textContent = eventData.description;
        this.setChoices(eventData.choices, '#eventChoices');
    }

    renderOutcome(eventData) {
        const outcome = eventData.outcome;
        const progressEl = this.$('#eventProgress');
        const progressBar = this.$('#eventProgressBar');
        const progressText = this.$('#eventProgressText');
        const descEl = this.$('#eventDescription');
        const choicesEl = this.$('#eventChoices');

        if (outcome.timeAddedMinutes > 0) {
            // Animate progress first
            hide(descEl);
            hide(choicesEl);
            show(progressEl);
            
            progressText.textContent = `${eventData.description} (+${outcome.timeAddedMinutes} min)`;
            
            const durationMs = Math.max(500, outcome.timeAddedMinutes / 5 * 1000);
            
            Animator.progressBar(progressBar, durationMs, () => {
                setTimeout(() => {
                    hide(progressEl);
                    this.showOutcomeContent(eventData, outcome);
                }, 150);
            });
        } else {
            hide(progressEl);
            this.showOutcomeContent(eventData, outcome);
        }
    }

    showOutcomeContent(eventData, outcome) {
        const descEl = this.$('#eventDescription');
        const choicesEl = this.$('#eventChoices');

        show(descEl);
        show(choicesEl);
        this.clear(descEl);
        this.clear(choicesEl);

        // Context + message
        descEl.appendChild(
            DOMBuilder.div('event-choice-context').text(eventData.description).build()
        );
        descEl.appendChild(
            DOMBuilder.div('event-outcome-message').text(outcome.message).build()
        );

        // Build summary
        const summary = DOMBuilder.div('event-outcome-summary');

        // Time
        if (outcome.timeAddedMinutes > 0) {
            summary.append(StatRow.outcome('schedule', `+${outcome.timeAddedMinutes} minutes`, 'time'));
        }

        // Damage
        outcome.damageTaken?.forEach(dmg => {
            summary.append(StatRow.outcome('personal_injury', dmg, 'damage'));
        });

        // Effects
        outcome.effectsApplied?.forEach(effect => {
            summary.append(StatRow.outcome('warning', effect, 'effect'));
        });

        // Items gained/lost
        outcome.itemsGained?.forEach(item => {
            summary.append(StatRow.outcome('add', item, 'gain'));
        });
        outcome.itemsLost?.forEach(item => {
            summary.append(StatRow.outcome('remove', item, 'loss'));
        });

        // Tensions
        outcome.tensionsChanged?.forEach(tension => {
            const isPositive = tension.startsWith('-');
            summary.append(StatRow.outcome(
                isPositive ? 'trending_down' : 'trending_up',
                tension,
                isPositive ? 'tension-down' : 'tension-up'
            ));
        });

        // Stats delta
        if (outcome.statsDelta) {
            const statsGroup = DOMBuilder.div('outcome-stats-group');
            const d = outcome.statsDelta;
            
            if (Math.abs(d.energyDelta) >= 1) {
                const val = Math.round(d.energyDelta);
                statsGroup.append(StatRow.outcome('bolt', `${val > 0 ? '+' : ''}${val} energy`, 'stat'));
            }
            if (Math.abs(d.calorieDelta) >= 1) {
                const val = Math.round(d.calorieDelta);
                statsGroup.append(StatRow.outcome('restaurant', `${val > 0 ? '+' : ''}${val} kcal`, 'stat'));
            }
            if (Math.abs(d.hydrationDelta) >= 10) {
                const val = Math.round(d.hydrationDelta);
                statsGroup.append(StatRow.outcome('water_drop', `${val > 0 ? '+' : ''}${val} mL`, 'stat'));
            }
            if (Math.abs(d.temperatureDelta) >= 0.1) {
                const val = d.temperatureDelta.toFixed(1);
                statsGroup.append(StatRow.outcome('thermostat', `${d.temperatureDelta > 0 ? '+' : ''}${val}°F`, 'stat'));
            }
            
            if (statsGroup.el.children.length > 0) {
                summary.append(statsGroup);
            }
        }

        if (summary.el.children.length > 0) {
            choicesEl.appendChild(summary.build());
        }

        // Continue button
        const continueBtn = document.createElement('button');
        continueBtn.className = 'event-continue-btn';
        continueBtn.textContent = 'Continue';
        continueBtn.onclick = () => this.respond('continue');
        choicesEl.appendChild(continueBtn);
    }

    cleanup() {
        const progressEl = this.$('#eventProgress');
        const progressBar = this.$('#eventProgressBar');
        if (progressEl) hide(progressEl);
        if (progressBar) progressBar.style.width = '0%';
    }
}