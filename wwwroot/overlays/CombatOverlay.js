import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
import { Utils, show, hide } from '../modules/utils.js';

/**
 * CombatOverlay - Multi-phase combat interface
 * Handles both predator approach (encounter) and active combat phases
 */
export class CombatOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('combatOverlay', inputHandler);

        // Combat phase elements
        this.animalNameEl = document.getElementById('combatAnimalName');
        this.distanceMetersEl = document.getElementById('combatDistanceMeters');
        this.behaviorStateEl = document.getElementById('combatBehaviorState');
        this.behaviorDescEl = document.getElementById('combatBehaviorDesc');
        this.animalHealthEl = document.getElementById('combatAnimalHealth');
        this.threatFactorsEl = document.getElementById('combatThreatFactors');
        this.actionMessageEl = document.getElementById('combatActionMessage');
        this.playerBracedEl = document.getElementById('combatPlayerBraced');
        this.actionsEl = document.getElementById('combatActions');
        this.outcomeEl = document.getElementById('combatOutcome');
        this.outcomeMessageEl = document.getElementById('combatOutcomeMessage');
        this.rewardsEl = document.getElementById('combatRewards');

        // Approach phase elements (from EncounterOverlay)
        this.encounterTargetEl = document.getElementById('encounterTarget');
        this.encounterDistanceMask = document.getElementById('encounterDistanceMask');
        this.encounterDistanceValue = document.getElementById('encounterDistanceValue');
        this.encounterBoldnessDescriptor = document.getElementById('encounterBoldnessDescriptor');
        this.encounterBoldnessFill = document.getElementById('encounterBoldnessFill');
        this.encounterFactorsEl = document.getElementById('encounterFactors');
        this.encounterMessageEl = document.getElementById('encounterMessage');
        this.encounterChoicesEl = document.getElementById('encounterChoices');
        this.encounterOutcomeEl = document.getElementById('encounterOutcome');
        this.encounterOutcomeMessageEl = document.getElementById('encounterOutcomeMessage');
    }

    render(combatData, inputId) {
        this.show(inputId);

        // Phase detection: Check for approach phase properties
        const isApproachPhase = combatData.boldnessLevel !== undefined || combatData.choices !== undefined;
        const isOutcomePhase = combatData.outcome != null;  // Check for both null and undefined

        if (isApproachPhase && !isOutcomePhase) {
            this.renderApproachPhase(combatData, inputId);
        } else if (isOutcomePhase) {
            // Outcome phase - check which type
            if (isApproachPhase) {
                this.showApproachOutcome(combatData);
            } else {
                this.showCombatOutcome(combatData);
            }
        } else {
            this.renderCombatPhase(combatData, inputId);
        }
    }

    /**
     * Render approach phase (predator encounter before combat)
     */
    renderApproachPhase(encounterData, inputId) {
        // Hide combat-specific elements
        hide(document.querySelector('.combat-zones'));
        hide(document.querySelector('.combat-behavior'));
        hide(this.actionMessageEl);
        hide(this.playerBracedEl);
        hide(this.outcomeEl);
        hide(this.actionsEl);

        // Show encounter-specific elements
        show(document.getElementById('encounterOverlay'));

        // Predator name
        if (this.encounterTargetEl) {
            this.encounterTargetEl.textContent = encounterData.predatorName || encounterData.animalName;
        }

        // Distance bar with animation
        this.updateApproachDistanceBar(encounterData);

        // Boldness gauge
        this.updateBoldness(encounterData);

        // Threat factors (using shared implementation)
        this.updateApproachThreatFactors(encounterData.threatFactors || []);

        // Status message
        if (encounterData.statusMessage) {
            this.encounterMessageEl.textContent = encounterData.statusMessage;
            show(this.encounterMessageEl);
        } else {
            hide(this.encounterMessageEl);
        }

        // Choices (approach phase actions)
        this.showApproachChoices(encounterData);
    }

    /**
     * Render combat phase (active fighting)
     */
    renderCombatPhase(combatData, inputId) {
        // Hide encounter-specific elements
        hide(document.getElementById('encounterOverlay'));

        // Show combat-specific elements
        show(document.querySelector('.combat-zones'));
        show(document.querySelector('.combat-behavior'));

        // Update distance zone indicator
        this.updateDistance(combatData);

        // Update animal status
        this.updateAnimalStatus(combatData);

        // Update threat factors
        this.updateThreatFactors(combatData);

        // Last action message
        if (combatData.lastActionMessage) {
            this.actionMessageEl.textContent = combatData.lastActionMessage;
            show(this.actionMessageEl);
        } else {
            this.actionMessageEl.textContent = '';
        }

        // Player status (braced indicator)
        if (combatData.playerBraced) {
            show(this.playerBracedEl);
        } else {
            hide(this.playerBracedEl);
        }

        // Combat actions
        this.showActions(combatData);
    }

    updateDistance(combatData) {
        // Update zone highlight
        const zones = document.querySelectorAll('.combat-zone');
        const currentZone = (combatData.distanceZone || '').toLowerCase();

        zones.forEach(zone => {
            zone.classList.remove('active');
            if (zone.dataset.zone === currentZone) {
                zone.classList.add('active');
            }
        });

        // Update distance meters
        const newDistance = Math.round(combatData.distanceMeters || 0);
        this.distanceMetersEl.textContent = `${newDistance}m`;
    }

    updateAnimalStatus(combatData) {
        // Animal name
        this.animalNameEl.textContent = combatData.animalName || 'Enemy';

        // Behavior state and description
        const behaviorState = combatData.behaviorState || 'Unknown';
        this.behaviorStateEl.textContent = behaviorState;
        this.behaviorStateEl.className = 'behavior-state ' + behaviorState.toLowerCase();
        this.behaviorDescEl.textContent = combatData.behaviorDescription || '';

        // Health description
        this.animalHealthEl.textContent = combatData.animalConditionNarrative || 'The animal watches you.';
    }

    updateThreatFactors(combatData) {
        this.clear(this.threatFactorsEl);

        const factors = combatData.threatFactors || [];
        factors.forEach(factor => {
            const isDanger = ['meat', 'weakness', 'blood', 'bleeding'].includes(factor.id);
            const className = 'threat-factor ' + (isDanger ? 'danger' : 'advantage');
            const factorEl = this.createIconText(factor.icon || 'info', factor.description, className);
            this.threatFactorsEl.appendChild(factorEl);
        });
    }

    showActions(combatData) {
        hide(this.outcomeEl);
        show(this.actionsEl);

        this.setChoices(combatData.actions, '#combatActions');
    }

    showOutcome(combatData) {
        // Delegate to appropriate outcome handler
        if (combatData.boldnessLevel !== undefined || combatData.choices !== undefined) {
            this.showApproachOutcome(combatData);
        } else {
            this.showCombatOutcome(combatData);
        }
    }

    showCombatOutcome(combatData) {
        hide(this.actionsEl);
        hide(this.actionMessageEl);
        hide(document.getElementById('encounterOverlay'));
        show(this.outcomeEl);

        const outcome = combatData.outcome;
        if (!outcome) return;

        this.outcomeEl.className = 'combat-outcome ' + (outcome.result || '').toLowerCase();
        this.outcomeMessageEl.textContent = outcome.message;

        // Show rewards if any
        this.clear(this.rewardsEl);
        if (outcome.rewards && outcome.rewards.length > 0) {
            show(this.rewardsEl);
            outcome.rewards.forEach(reward => {
                const rewardEl = document.createElement('div');
                rewardEl.className = 'combat-reward-item';
                rewardEl.textContent = '+ ' + reward;
                this.rewardsEl.appendChild(rewardEl);
            });
        } else {
            hide(this.rewardsEl);
        }

        // Add continue button
        const continueBtn = document.createElement('button');
        continueBtn.className = 'event-continue-btn';
        continueBtn.textContent = 'Continue';
        continueBtn.onclick = () => this.respond('continue');
        this.outcomeEl.appendChild(continueBtn);
    }

    /**
     * Approach phase: Update distance bar with animation
     * Reversed from hunt - red is close (danger), green is far
     */
    updateApproachDistanceBar(encounterData) {
        const maxDistance = 100;

        if (encounterData.isAnimatingDistance && encounterData.previousDistanceMeters != null) {
            Animator.distanceMask(this.encounterDistanceMask, encounterData.previousDistanceMeters, encounterData.currentDistanceMeters, maxDistance);
            Animator.distance(this.encounterDistanceValue, encounterData.previousDistanceMeters, encounterData.currentDistanceMeters);
        } else {
            const targetPct = Math.max(0, Math.min(100, encounterData.currentDistanceMeters / maxDistance * 100));
            this.encounterDistanceMask.style.transition = 'none';
            this.encounterDistanceMask.style.width = targetPct + '%';
            this.encounterDistanceValue.textContent = `${Math.round(encounterData.currentDistanceMeters)}m`;
        }
    }

    /**
     * Approach phase: Update boldness gauge
     */
    updateBoldness(encounterData) {
        const boldness = encounterData.boldnessLevel || 0;
        const descriptor = encounterData.boldnessDescriptor || 'wary';

        // Update descriptor text and class
        this.encounterBoldnessDescriptor.textContent = descriptor;
        this.encounterBoldnessDescriptor.className = 'encounter-boldness-descriptor ' + descriptor;

        // Update fill bar
        this.encounterBoldnessFill.style.width = (boldness * 100) + '%';
        this.encounterBoldnessFill.className = 'encounter-boldness-fill ' + descriptor;
    }

    /**
     * Approach phase: Update threat factors
     */
    updateApproachThreatFactors(factors) {
        this.clear(this.encounterFactorsEl);

        if (!factors || factors.length === 0) {
            hide(this.encounterFactorsEl);
            return;
        }

        show(this.encounterFactorsEl);
        factors.forEach(factor => {
            const factorEl = this.createIconText(factor.icon || 'warning', factor.description, 'encounter-factor');
            this.encounterFactorsEl.appendChild(factorEl);
        });
    }

    /**
     * Approach phase: Show choices (stand/back/run/fight/drop)
     */
    showApproachChoices(encounterData) {
        hide(this.encounterOutcomeEl);
        show(this.encounterChoicesEl);

        this.clear(this.encounterChoicesEl);

        encounterData.choices.forEach(choice => {
            const btn = this.createOptionButton({
                label: choice.label,
                description: choice.description,
                disabled: !choice.isAvailable,
                disabledReason: choice.disabledReason,
                onClick: () => this.respond(choice.id)
            });
            this.encounterChoicesEl.appendChild(btn);
        });
    }

    /**
     * Approach phase: Show outcome
     */
    showApproachOutcome(encounterData) {
        hide(this.encounterChoicesEl);
        show(this.encounterOutcomeEl);

        const outcome = encounterData.outcome;
        if (!outcome) return;

        this.encounterOutcomeEl.className = 'encounter-outcome ' + (outcome.result || '').toLowerCase();
        this.encounterOutcomeMessageEl.textContent = outcome.message;

        // Add continue button
        this.clear(this.encounterOutcomeEl);
        this.encounterOutcomeEl.appendChild(this.encounterOutcomeMessageEl);

        const continueBtn = document.createElement('button');
        continueBtn.className = 'event-continue-btn';
        continueBtn.textContent = outcome.result === 'fight' ? 'Fight!' : 'Continue';
        continueBtn.onclick = () => this.respond('continue');
        this.encounterOutcomeEl.appendChild(continueBtn);
    }

    cleanup() {
        this.clear(this.actionsEl);
        this.clear(this.rewardsEl);
        this.clear(this.threatFactorsEl);
        this.clear(this.encounterChoicesEl);
        this.clear(this.encounterFactorsEl);
        this.clear(this.encounterOutcomeEl);
    }
}
