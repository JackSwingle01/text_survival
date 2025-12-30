import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';

/**
 * EncounterOverlay - Predator encounter interface with distance/boldness tracking
 */
export class EncounterOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('encounterOverlay', inputHandler);

        this.targetEl = document.getElementById('encounterTarget');
        this.distanceMask = document.getElementById('encounterDistanceMask');
        this.distanceValue = document.getElementById('encounterDistanceValue');
        this.boldnessDescriptor = document.getElementById('encounterBoldnessDescriptor');
        this.boldnessFill = document.getElementById('encounterBoldnessFill');
        this.factorsEl = document.getElementById('encounterFactors');
        this.messageEl = document.getElementById('encounterMessage');
        this.choicesEl = document.getElementById('encounterChoices');
        this.outcomeEl = document.getElementById('encounterOutcome');
        this.outcomeMessageEl = document.getElementById('encounterOutcomeMessage');
    }

    render(encounterData, inputId) {
        this.show(inputId);

        // Predator name
        this.targetEl.textContent = encounterData.predatorName;

        // Distance bar with animation
        this.updateDistanceBar(encounterData);

        // Boldness gauge
        this.updateBoldness(encounterData);

        // Threat factors
        this.updateFactors(encounterData.threatFactors || []);

        // Status message
        if (encounterData.statusMessage) {
            this.messageEl.textContent = encounterData.statusMessage;
            show(this.messageEl);
        } else {
            hide(this.messageEl);
        }

        // Check if outcome phase
        if (encounterData.outcome) {
            this.showOutcome(encounterData);
        } else {
            this.showChoices(encounterData);
        }
    }

    /**
     * Update encounter distance bar with animation
     * Reversed from hunt - red is close (danger), green is far
     */
    updateDistanceBar(encounterData) {
        const maxDistance = 100;

        if (encounterData.isAnimatingDistance && encounterData.previousDistanceMeters != null) {
            Animator.distanceMask(this.distanceMask, encounterData.previousDistanceMeters, encounterData.currentDistanceMeters, maxDistance);
            Animator.distance(this.distanceValue, encounterData.previousDistanceMeters, encounterData.currentDistanceMeters);
        } else {
            const targetPct = Math.max(0, Math.min(100, encounterData.currentDistanceMeters / maxDistance * 100));
            this.distanceMask.style.transition = 'none';
            this.distanceMask.style.width = targetPct + '%';
            this.distanceValue.textContent = `${Math.round(encounterData.currentDistanceMeters)}m`;
        }
    }

    /**
     * Update encounter boldness gauge
     */
    updateBoldness(encounterData) {
        const boldness = encounterData.boldnessLevel || 0;
        const descriptor = encounterData.boldnessDescriptor || 'wary';

        // Update descriptor text and class
        this.boldnessDescriptor.textContent = descriptor;
        this.boldnessDescriptor.className = 'encounter-boldness-descriptor ' + descriptor;

        // Update fill bar
        this.boldnessFill.style.width = (boldness * 100) + '%';
        this.boldnessFill.className = 'encounter-boldness-fill ' + descriptor;
    }

    /**
     * Update encounter threat factors display
     */
    updateFactors(factors) {
        this.clear(this.factorsEl);

        if (!factors || factors.length === 0) {
            hide(this.factorsEl);
            return;
        }

        show(this.factorsEl);
        factors.forEach(factor => {
            const factorEl = document.createElement('div');
            factorEl.className = 'encounter-factor';

            const icon = document.createElement('span');
            icon.className = ICON_CLASS;
            icon.textContent = factor.icon || 'warning';
            factorEl.appendChild(icon);

            const text = document.createElement('span');
            text.textContent = factor.description;
            factorEl.appendChild(text);

            this.factorsEl.appendChild(factorEl);
        });
    }

    /**
     * Show encounter choices (during encounter)
     */
    showChoices(encounterData) {
        hide(this.outcomeEl);
        show(this.choicesEl);

        this.clear(this.choicesEl);

        encounterData.choices.forEach(choice => {
            const btn = document.createElement('button');
            btn.className = 'option-btn';
            btn.disabled = !choice.isAvailable;

            const label = document.createElement('span');
            label.className = 'option-btn__label';
            label.textContent = choice.label;
            btn.appendChild(label);

            // Show disabled reason if unavailable, otherwise show description
            if (!choice.isAvailable && choice.disabledReason) {
                const reason = document.createElement('span');
                reason.className = 'choice-disabled-reason';
                reason.textContent = choice.disabledReason;
                btn.appendChild(reason);
            } else if (choice.description) {
                const desc = document.createElement('span');
                desc.className = 'option-btn__desc';
                desc.textContent = choice.description;
                btn.appendChild(desc);
            }

            btn.onclick = () => this.respond(choice.id);
            this.choicesEl.appendChild(btn);
        });
    }

    /**
     * Show encounter outcome
     */
    showOutcome(encounterData) {
        hide(this.choicesEl);
        show(this.outcomeEl);

        const outcome = encounterData.outcome;
        this.outcomeEl.className = 'encounter-outcome ' + (outcome.result || '').toLowerCase();
        this.outcomeMessageEl.textContent = outcome.message;

        // Add continue button
        this.clear(this.outcomeEl);
        this.outcomeEl.appendChild(this.outcomeMessageEl);

        const continueBtn = document.createElement('button');
        continueBtn.className = 'event-continue-btn';
        continueBtn.textContent = outcome.result === 'fight' ? 'Fight!' : 'Continue';
        continueBtn.onclick = () => this.respond('continue');
        this.outcomeEl.appendChild(continueBtn);
    }

    cleanup() {
        this.clear(this.factorsEl);
        this.clear(this.choicesEl);
        this.clear(this.outcomeEl);
    }
}
