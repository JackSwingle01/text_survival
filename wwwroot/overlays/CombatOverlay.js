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

        // Intro phase elements
        this.introSectionEl = document.getElementById('combatIntroSection');
        this.introActionsEl = document.getElementById('combatIntroActions');
        this.introMessageEl = document.getElementById('combatIntroMessage');
        this.introConfirmBtn = document.getElementById('combatIntroConfirm');
        this.combatContentEl = document.getElementById('combatContent');

        // Combat phase elements
        this.animalNameEl = document.getElementById('combatAnimalName');
        this.behaviorStateEl = document.getElementById('combatBehaviorState');
        this.behaviorDescEl = document.getElementById('combatBehaviorDesc');
        this.animalHealthEl = document.getElementById('combatAnimalHealth');
        this.playerBracedEl = document.getElementById('combatPlayerBraced');
        this.threatFactorsEl = document.getElementById('combatThreatFactors');
        this.threatSectionEl = document.getElementById('combatThreatSection');
        this.actionMessageEl = document.getElementById('combatActionMessage');
        this.actionsEl = document.getElementById('combatActions');
        this.outcomeEl = document.getElementById('combatOutcome');
        this.outcomeMessageEl = document.getElementById('combatOutcomeMessage');
        this.rewardsEl = document.getElementById('combatRewards');
        this.outcomeActionsEl = document.getElementById('combatOutcomeActions');
        this.outcomeContinueBtn = document.getElementById('combatOutcomeContinueBtn');

        // Distance track elements
        this.distanceValueEl = document.getElementById('combatDistanceValue');
        this.playerMarkerEl = document.getElementById('combatPlayerMarker');

        // Aggression gauge elements
        this.aggressionDescriptorEl = document.getElementById('combatAggressionDescriptor');
        this.aggressionFillEl = document.getElementById('combatAggressionFill');

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

        // Set up button handlers
        this.introConfirmBtn.onclick = () => this.respond('confirm');
        this.outcomeContinueBtn.onclick = () => this.respond('continue');
    }

    render(combatData, inputId) {
        this.show(inputId);

        // Phase detection: Distinguish between encounter (choices) and combat (actions + phase)
        // Encounter has: choices, predatorName
        // Combat has: actions, phase, animalName
        const isApproachPhase = combatData.choices !== undefined;
        const isCombatPhase = combatData.phase !== undefined || combatData.actions !== undefined;
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
     * Uses phase field to determine what to display
     */
    renderCombatPhase(combatData, inputId) {
        // Hide encounter-specific elements
        hide(document.getElementById('encounterOverlay'));

        // Always update animal status header
        this.animalNameEl.textContent = combatData.animalName || 'Enemy';

        // Phase-specific rendering
        const phase = combatData.phase?.toLowerCase() || 'playerChoice';

        switch (phase) {
            case 'intro':
                this.renderIntroPhase(combatData);
                break;

            case 'playerchoice':
                this.renderChoicePhase(combatData);
                break;

            case 'playeraction':
            case 'animalaction':
            case 'behaviorchange':
                this.renderNarrativePhase(combatData, 'Continue');
                break;

            case 'outcome':
                this.showCombatOutcome(combatData);
                break;

            default:
                this.renderChoicePhase(combatData);
        }
    }

    /**
     * Render intro phase (first frame with confirmation)
     */
    renderIntroPhase(combatData) {
        // Show intro, hide combat content
        show(this.introSectionEl);
        show(this.introActionsEl);
        hide(this.combatContentEl);

        // Set intro message
        this.introMessageEl.textContent = combatData.narrativeMessage || `A ${combatData.animalName?.toLowerCase()} lunges at you!`;
    }

    /**
     * Render player choice phase - show actions and full combat state
     */
    renderChoicePhase(combatData) {
        // Hide intro, show content
        hide(this.introSectionEl);
        hide(this.introActionsEl);
        hide(this.outcomeActionsEl);
        show(this.combatContentEl);

        // Show elements that narrative phase hides
        show(this.behaviorDescEl);
        show(this.animalHealthEl);

        // Update combat state display
        this.updateDistanceBar(combatData);
        this.updateAggression(combatData);
        this.updateAnimalStatus(combatData);
        this.updateThreatFactors(combatData);

        // Braced indicator
        if (combatData.playerBraced) {
            show(this.playerBracedEl);
        } else {
            hide(this.playerBracedEl);
        }

        // Hide narrative message in choice phase
        hide(this.actionMessageEl);
        hide(this.outcomeEl);

        // Combat actions
        this.showActions(combatData);
    }

    /**
     * Render narrative phase - show message with continue button
     * Used for player action results, animal attacks, and behavior changes
     * Only shows the narrative - hides behavior description to avoid info dump
     */
    renderNarrativePhase(combatData, buttonText) {
        // Hide intro, show content
        hide(this.introSectionEl);
        show(this.combatContentEl);

        // Update bars (keep visual context)
        this.updateDistanceBar(combatData);
        this.updateAggression(combatData);

        // Update animal name and behavior state
        this.animalNameEl.textContent = combatData.animalName || 'Enemy';
        this.behaviorStateEl.textContent = combatData.behaviorState || '';
        this.behaviorStateEl.className = 'behavior-state ' + (combatData.behaviorState || '').toLowerCase();

        // Hide the behavior description - the narrative message is the focus
        hide(this.behaviorDescEl);
        hide(this.animalHealthEl);
        hide(this.threatSectionEl);
        hide(this.playerBracedEl);

        // Show narrative message prominently
        if (combatData.narrativeMessage) {
            this.actionMessageEl.textContent = combatData.narrativeMessage;
            show(this.actionMessageEl);
        } else {
            hide(this.actionMessageEl);
        }

        // Hide outcome
        hide(this.outcomeEl);

        // Show continue button instead of actions
        this.clear(this.actionsEl);
        const continueBtn = document.createElement('button');
        continueBtn.className = 'event-continue-btn';
        continueBtn.textContent = buttonText;
        continueBtn.onclick = () => this.respond('continue');
        this.actionsEl.appendChild(continueBtn);
        show(this.actionsEl);
    }

    /**
     * Update combat distance track with player marker
     * Player marker slides along track toward target (fixed on right)
     * 25m = left edge (0%), 0m = right edge (100%)
     */
    updateDistanceBar(combatData) {
        const maxDistance = 25; // Combat max distance
        const currentDistance = combatData.distanceMeters || 0;
        const prevDistance = combatData.previousDistanceMeters;

        // Update text value
        if (this.distanceValueEl) {
            this.distanceValueEl.textContent = `${Math.round(currentDistance)}m`;
        }

        // Position player marker along track
        // 25m = 0% (left), 0m = 100% (right, at target)
        if (this.playerMarkerEl) {
            const positionPct = 100 - (currentDistance / maxDistance * 100);
            const clampedPct = Math.max(0, Math.min(100, positionPct));

            // Check if we should animate (previous distance differs significantly)
            const shouldAnimate = prevDistance != null && Math.abs(prevDistance - currentDistance) > 0.5;

            if (shouldAnimate) {
                // CSS transition handles animation
                this.playerMarkerEl.style.transition = 'left 0.5s ease-out';
            } else {
                // Instant positioning
                this.playerMarkerEl.style.transition = 'none';
            }

            this.playerMarkerEl.style.left = clampedPct + '%';
        }
    }

    /**
     * Update aggression gauge
     */
    updateAggression(combatData) {
        const boldness = combatData.boldnessLevel || 0;
        const descriptor = combatData.boldnessDescriptor || 'wary';

        // Update descriptor text and class
        if (this.aggressionDescriptorEl) {
            this.aggressionDescriptorEl.textContent = descriptor;
            this.aggressionDescriptorEl.className = 'gauge-bar__descriptor gauge-bar__descriptor--' + descriptor;
        }

        // Update fill bar
        if (this.aggressionFillEl) {
            this.aggressionFillEl.style.width = (boldness * 100) + '%';
            this.aggressionFillEl.className = 'gauge-bar__fill gauge-bar__fill--' + descriptor;
        }
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

        // Hide entire panel if no factors
        if (factors.length === 0) {
            hide(this.threatSectionEl);
            return;
        }

        show(this.threatSectionEl);

        factors.forEach(factor => {
            const isDanger = ['meat', 'weakness', 'blood', 'bleeding'].includes(factor.id);
            // Use .badge class instead of .threat-factor
            const className = 'badge threat-factor ' + (isDanger ? 'danger' : 'advantage');
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
        // Delegate to appropriate outcome handler based on data structure
        if (combatData.choices !== undefined) {
            this.showApproachOutcome(combatData);
        } else {
            this.showCombatOutcome(combatData);
        }
    }

    showCombatOutcome(combatData) {
        hide(this.actionsEl);
        hide(this.actionMessageEl);
        hide(this.introActionsEl);
        hide(document.getElementById('encounterOverlay'));
        show(this.outcomeEl);
        show(this.outcomeActionsEl);

        const outcome = combatData.outcome;
        if (!outcome) return;

        this.outcomeEl.className = 'combat-outcome ' + (outcome.result || '').toLowerCase();

        // Outcome message
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

        // Update button text
        this.outcomeContinueBtn.textContent = 'Continue';
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

        // Guard against missing choices array
        if (!encounterData.choices || !Array.isArray(encounterData.choices)) {
            console.warn('No choices available in approach phase data');
            return;
        }

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
