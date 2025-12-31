import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
import { Utils, show, hide } from '../modules/utils.js';

/**
 * HuntOverlay - Hunting interface with distance tracking and state display
 */
export class HuntOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('huntOverlay', inputHandler);

        this.animalNameEl = document.getElementById('huntAnimalName');
        this.animalDescEl = document.getElementById('huntAnimalDesc');
        this.distanceMask = document.getElementById('huntDistanceMask');
        this.distanceValue = document.getElementById('huntDistanceValue');
        this.activityEl = document.getElementById('huntActivity');
        this.timeEl = document.getElementById('huntTime');
        this.stateEl = document.getElementById('huntState');
        this.messageEl = document.getElementById('huntMessage');
        this.choicesEl = document.getElementById('huntChoices');
        this.outcomeEl = document.getElementById('huntOutcome');
        this.outcomeMessageEl = document.getElementById('huntOutcomeMessage');
        this.outcomeSummaryEl = document.getElementById('huntOutcomeSummary');
        this.outcomeActionsEl = document.getElementById('huntOutcomeActions');
        this.outcomeContinueBtn = document.getElementById('huntOutcomeContinueBtn');

        // Set up button
        this.outcomeContinueBtn.onclick = () => this.respond('continue');

        this.lastHuntTime = 0;
    }

    render(huntData, inputId) {
        this.show(inputId);

        // Animal info
        this.animalNameEl.textContent = huntData.animalName;
        this.animalDescEl.textContent = huntData.animalDescription || '';

        // Distance bar with animation
        this.updateDistanceBar(huntData);

        // Status
        this.activityEl.textContent = huntData.animalActivity || '';

        // Time with animation
        const newTime = huntData.minutesSpent;
        if (huntData.isAnimatingDistance && this.lastHuntTime < newTime) {
            Animator.time(this.timeEl, this.lastHuntTime, newTime);
        } else {
            this.timeEl.textContent = newTime + ' min';
        }
        this.lastHuntTime = newTime;

        this.updateStateDisplay(huntData.animalState);

        // Message
        if (huntData.statusMessage) {
            this.messageEl.textContent = huntData.statusMessage;
            show(this.messageEl);
        } else {
            hide(this.messageEl);
        }

        // Check if outcome phase
        if (huntData.outcome) {
            this.showOutcome(huntData);
        } else {
            this.showChoices(huntData);
        }
    }

    updateDistanceBar(huntData) {
        const maxDistance = 100;

        if (huntData.isAnimatingDistance && huntData.previousDistanceMeters != null) {
            Animator.distanceMask(this.distanceMask, huntData.previousDistanceMeters, huntData.currentDistanceMeters, maxDistance);
            Animator.distance(this.distanceValue, huntData.previousDistanceMeters, huntData.currentDistanceMeters);
        } else {
            const targetPct = Math.max(0, Math.min(100, huntData.currentDistanceMeters / maxDistance * 100));
            this.distanceMask.style.transition = 'none';
            this.distanceMask.style.width = targetPct + '%';
            this.distanceValue.textContent = `${Math.round(huntData.currentDistanceMeters)}m`;
        }
    }

    updateStateDisplay(state) {
        if (!this.stateEl) return;

        this.stateEl.className = 'hunt-state ' + (state || 'idle').toLowerCase();

        const stateText = {
            'idle': 'unaware',
            'alert': 'alert!',
            'detected': 'spotted you!'
        };

        this.stateEl.textContent = stateText[(state || 'idle').toLowerCase()] || state;
    }

    showChoices(huntData) {
        hide(this.outcomeEl);
        hide(this.outcomeActionsEl);
        show(this.choicesEl);

        this.setChoices(huntData.choices, '#huntChoices');
    }

    showOutcome(huntData) {
        hide(this.choicesEl);
        show(this.outcomeEl);
        show(this.outcomeActionsEl);

        const outcome = huntData.outcome;
        this.outcomeMessageEl.textContent = outcome.message;

        this.clear(this.outcomeSummaryEl);

        // Time spent
        if (outcome.totalMinutesSpent > 0) {
            this.addOutcomeItem('schedule', `${outcome.totalMinutesSpent} minutes`, 'time');
        }

        // Items gained
        if (outcome.itemsGained && outcome.itemsGained.length > 0) {
            outcome.itemsGained.forEach(item => {
                this.addOutcomeItem('add', item, 'gain');
            });
        }

        // Effects
        if (outcome.effectsApplied && outcome.effectsApplied.length > 0) {
            outcome.effectsApplied.forEach(effect => {
                this.addOutcomeItem('warning', effect, 'effect');
            });
        }

        // Update button text
        this.outcomeContinueBtn.textContent = outcome.transitionToCombat ? 'Face It!' : 'Continue';
    }

    addOutcomeItem(icon, text, styleClass) {
        const item = this.createIconText(icon, text, 'outcome-item ' + styleClass);
        this.outcomeSummaryEl.appendChild(item);
    }

    cleanup() {
        this.lastHuntTime = 0;
        this.clear(this.choicesEl);
        this.clear(this.outcomeSummaryEl);
    }
}
