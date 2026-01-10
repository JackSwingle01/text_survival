// overlays/HuntOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear, show, hide } from '../lib/helpers.js';
import { ActionButton, ContinueButton } from '../lib/components/ActionButton.js';
import { OutcomeItem } from '../lib/components/StatRow.js';
import { Animator } from '../core/Animator.js';

/**
 * HuntOverlay - Hunting interface with distance tracking and state display
 */
export class HuntOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('huntOverlay', inputHandler);
        this.lastHuntTime = 0;
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Animal info
        const nameEl = this.$('#huntAnimalName');
        const descEl = this.$('#huntAnimalDesc');
        if (nameEl) nameEl.textContent = data.animalName;
        if (descEl) descEl.textContent = data.animalDescription || '';

        // Distance bar with animation
        this.updateDistanceBar(data);

        // Activity status
        const activityEl = this.$('#huntActivity');
        if (activityEl) activityEl.textContent = data.animalActivity || '';

        // Time with animation
        const timeEl = this.$('#huntTime');
        if (timeEl) {
            const newTime = data.minutesSpent;
            if (data.isAnimatingDistance && this.lastHuntTime < newTime) {
                Animator.time(timeEl, this.lastHuntTime, newTime);
            } else {
                timeEl.textContent = newTime + ' min';
            }
            this.lastHuntTime = newTime;
        }

        // Animal state
        this.updateStateDisplay(data.animalState);

        // Message
        const messageEl = this.$('#huntMessage');
        if (messageEl) {
            if (data.statusMessage) {
                messageEl.textContent = data.statusMessage;
                show(messageEl);
            } else {
                hide(messageEl);
            }
        }

        // Check if outcome phase
        if (data.outcome) {
            this.renderOutcome(data);
        } else {
            this.renderChoices(data);
        }
    }

    updateDistanceBar(data) {
        const distanceMask = this.$('#huntDistanceMask');
        const distanceValue = this.$('#huntDistanceValue');
        if (!distanceMask || !distanceValue) return;

        const maxDistance = 100;

        if (data.isAnimatingDistance && data.previousDistanceMeters != null) {
            Animator.distanceMask(distanceMask, data.previousDistanceMeters, data.currentDistanceMeters, maxDistance);
            Animator.distance(distanceValue, data.previousDistanceMeters, data.currentDistanceMeters);
        } else {
            const targetPct = Math.max(0, Math.min(100, data.currentDistanceMeters / maxDistance * 100));
            distanceMask.style.transition = 'none';
            distanceMask.style.width = targetPct + '%';
            distanceValue.textContent = `${Math.round(data.currentDistanceMeters)}m`;
        }
    }

    updateStateDisplay(state) {
        const stateEl = this.$('#huntState');
        if (!stateEl) return;

        const normalizedState = (state || 'idle').toLowerCase();
        stateEl.className = 'hunt-state ' + normalizedState;

        const stateText = {
            'idle': 'unaware',
            'alert': 'alert!',
            'detected': 'spotted you!'
        };

        stateEl.textContent = stateText[normalizedState] || state;
    }

    renderChoices(data) {
        const choicesEl = this.$('#huntChoices');
        const outcomeEl = this.$('#huntOutcome');
        const outcomeActionsEl = this.$('#huntOutcomeActions');

        hide(outcomeEl);
        hide(outcomeActionsEl);
        show(choicesEl);

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
        const choicesEl = this.$('#huntChoices');
        const outcomeEl = this.$('#huntOutcome');
        const outcomeActionsEl = this.$('#huntOutcomeActions');
        const outcomeMessageEl = this.$('#huntOutcomeMessage');
        const outcomeSummaryEl = this.$('#huntOutcomeSummary');

        hide(choicesEl);
        show(outcomeEl);
        show(outcomeActionsEl);

        const outcome = data.outcome;

        if (outcomeMessageEl) {
            outcomeMessageEl.textContent = outcome.message;
        }

        if (outcomeSummaryEl) {
            clear(outcomeSummaryEl);

            // Time spent
            if (outcome.totalMinutesSpent > 0) {
                outcomeSummaryEl.appendChild(
                    OutcomeItem('schedule', `${outcome.totalMinutesSpent} minutes`, 'time')
                );
            }

            // Items gained
            outcome.itemsGained?.forEach(item => {
                outcomeSummaryEl.appendChild(
                    OutcomeItem('add', item, 'gain')
                );
            });

            // Effects
            outcome.effectsApplied?.forEach(effect => {
                outcomeSummaryEl.appendChild(
                    OutcomeItem('warning', effect, 'effect')
                );
            });
        }

        // Update continue button
        if (outcomeActionsEl) {
            clear(outcomeActionsEl);
            const buttonLabel = outcome.transitionToCombat ? 'Face It!' : 'Continue';
            outcomeActionsEl.appendChild(
                ContinueButton(() => this.respond('continue'), buttonLabel)
            );
        }
    }

    hide() {
        super.hide();
        this.lastHuntTime = 0;
    }
}
