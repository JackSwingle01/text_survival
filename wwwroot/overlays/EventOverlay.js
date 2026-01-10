// overlays/EventOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear, show, hide } from '../lib/helpers.js';
import { ActionButton } from '../lib/components/ActionButton.js';
import { OutcomeItem } from '../lib/components/StatRow.js';
import { Animator } from '../core/Animator.js';

/**
 * EventOverlay - Event display with choices and animated outcomes
 */
export class EventOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('eventOverlay', inputHandler);

        // Bind continue button (static element)
        const continueBtn = this.$('#eventOutcomeContinueBtn');
        if (continueBtn) {
            continueBtn.onclick = () => this.respond('continue');
        }
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Event name
        const nameEl = this.$('#eventName');
        if (nameEl) nameEl.textContent = data.name;

        if (data.outcome) {
            this.renderOutcome(data);
        } else {
            this.renderChoices(data);
        }
    }

    renderChoices(data) {
        const outcomeActionsEl = this.$('#eventOutcomeActions');
        const descEl = this.$('#eventDescription');
        const choicesEl = this.$('#eventChoices');

        hide(outcomeActionsEl);
        if (descEl) descEl.textContent = data.description;

        if (choicesEl) {
            clear(choicesEl);
            data.choices?.forEach(choice => {
                choicesEl.appendChild(
                    ActionButton(choice, () => this.respond(choice.id))
                );
            });
        }
    }

    renderOutcome(data) {
        const outcome = data.outcome;
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

            progressText.textContent = `${data.description} (+${outcome.timeAddedMinutes} min)`;

            const durationMs = Math.max(500, outcome.timeAddedMinutes / 5 * 1000);

            Animator.progressBar(progressBar, durationMs, () => {
                setTimeout(() => {
                    hide(progressEl);
                    this.showOutcomeContent(data, outcome);
                }, 150);
            });
        } else {
            hide(progressEl);
            this.showOutcomeContent(data, outcome);
        }
    }

    showOutcomeContent(data, outcome) {
        const descEl = this.$('#eventDescription');
        const choicesEl = this.$('#eventChoices');
        const outcomeActionsEl = this.$('#eventOutcomeActions');

        show(descEl);
        show(choicesEl);
        show(outcomeActionsEl);
        clear(descEl);
        clear(choicesEl);

        // Context + message
        descEl.appendChild(
            div({ className: 'event-choice-context' }, data.description)
        );
        descEl.appendChild(
            div({ className: 'event-outcome-message' }, outcome.message)
        );

        // Build summary
        const summaryItems = [];

        // Time
        if (outcome.timeAddedMinutes > 0) {
            summaryItems.push(OutcomeItem('schedule', `+${outcome.timeAddedMinutes} minutes`, 'time'));
        }

        // Damage
        outcome.damageTaken?.forEach(dmg => {
            summaryItems.push(OutcomeItem('personal_injury', dmg, 'damage'));
        });

        // Effects
        outcome.effectsApplied?.forEach(effect => {
            summaryItems.push(OutcomeItem('warning', effect, 'effect'));
        });

        // Items gained/lost
        outcome.itemsGained?.forEach(item => {
            summaryItems.push(OutcomeItem('add', item, 'gain'));
        });
        outcome.itemsLost?.forEach(item => {
            summaryItems.push(OutcomeItem('remove', item, 'loss'));
        });

        // Tensions
        outcome.tensionsChanged?.forEach(tension => {
            const isPositive = tension.startsWith('-');
            summaryItems.push(OutcomeItem(
                isPositive ? 'trending_down' : 'trending_up',
                tension,
                isPositive ? 'tension-down' : 'tension-up'
            ));
        });

        // Stats delta
        if (outcome.statsDelta) {
            const d = outcome.statsDelta;
            const statsItems = [];

            if (Math.abs(d.energyDelta) >= 1) {
                const val = Math.round(d.energyDelta);
                statsItems.push(OutcomeItem('bolt', `${val > 0 ? '+' : ''}${val} energy`, 'stat'));
            }
            if (Math.abs(d.calorieDelta) >= 1) {
                const val = Math.round(d.calorieDelta);
                statsItems.push(OutcomeItem('restaurant', `${val > 0 ? '+' : ''}${val} kcal`, 'stat'));
            }
            if (Math.abs(d.hydrationDelta) >= 10) {
                const val = Math.round(d.hydrationDelta);
                statsItems.push(OutcomeItem('water_drop', `${val > 0 ? '+' : ''}${val} mL`, 'stat'));
            }
            if (Math.abs(d.temperatureDelta) >= 0.1) {
                const val = d.temperatureDelta.toFixed(1);
                statsItems.push(OutcomeItem('thermostat', `${d.temperatureDelta > 0 ? '+' : ''}${val}°F`, 'stat'));
            }

            if (statsItems.length > 0) {
                summaryItems.push(
                    div({ className: 'outcome-stats-group' }, ...statsItems)
                );
            }
        }

        // Add summary to choices area
        if (summaryItems.length > 0) {
            choicesEl.appendChild(
                div({ className: 'event-outcome-summary' }, ...summaryItems)
            );
        }
    }

    hide() {
        super.hide();
        const progressEl = this.$('#eventProgress');
        const progressBar = this.$('#eventProgressBar');
        if (progressEl) hide(progressEl);
        if (progressBar) progressBar.style.width = '0%';
    }
}
