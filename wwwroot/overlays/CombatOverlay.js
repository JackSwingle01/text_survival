import { OverlayManager } from '../core/OverlayManager.js';
import { Utils, show, hide } from '../modules/utils.js';

/**
 * CombatOverlay - Combat interface with distance zones and action groups
 */
export class CombatOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('combatOverlay', inputHandler);

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
    }

    render(combatData, inputId) {
        this.show(inputId);

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

        // Check if outcome phase
        if (combatData.outcome) {
            this.showOutcome(combatData);
        } else {
            this.showActions(combatData);
        }
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
            const factorEl = document.createElement('div');
            const isDanger = ['meat', 'weakness', 'blood', 'bleeding'].includes(factor.id);
            factorEl.className = 'threat-factor ' + (isDanger ? 'danger' : 'advantage');

            const iconEl = document.createElement('span');
            iconEl.className = 'material-symbols-outlined';
            iconEl.textContent = factor.icon || 'info';
            factorEl.appendChild(iconEl);

            const labelEl = document.createElement('span');
            labelEl.textContent = factor.description;
            factorEl.appendChild(labelEl);

            this.threatFactorsEl.appendChild(factorEl);
        });
    }

    showActions(combatData) {
        hide(this.outcomeEl);
        show(this.actionsEl);

        this.clear(this.actionsEl);

        const actions = combatData.actions || [];

        // Group actions by category
        const categories = ['offensive', 'defensive', 'movement', 'special'];
        const groupedActions = {};
        categories.forEach(cat => groupedActions[cat] = []);

        actions.forEach(action => {
            const cat = action.category || 'special';
            if (groupedActions[cat]) {
                groupedActions[cat].push(action);
            } else {
                groupedActions['special'].push(action);
            }
        });

        // Render each category group
        categories.forEach(category => {
            const catActions = groupedActions[category];
            if (catActions.length === 0) return;

            const groupEl = document.createElement('div');
            groupEl.className = 'combat-action-group';

            catActions.forEach(action => {
                const btn = document.createElement('button');
                btn.className = `combat-action ${action.category || 'special'}`;
                btn.disabled = !action.isAvailable;

                const nameEl = document.createElement('div');
                nameEl.className = 'combat-action-name';
                nameEl.textContent = action.label;
                btn.appendChild(nameEl);

                if (!action.isAvailable && action.disabledReason) {
                    const reasonEl = document.createElement('div');
                    reasonEl.className = 'combat-action-disabled';
                    reasonEl.textContent = action.disabledReason;
                    btn.appendChild(reasonEl);
                } else {
                    if (action.description) {
                        const hintEl = document.createElement('div');
                        hintEl.className = 'combat-action-hint';
                        hintEl.textContent = action.description;
                        btn.appendChild(hintEl);
                    }

                    if (action.hitChance) {
                        const statEl = document.createElement('div');
                        statEl.className = 'combat-action-stat';
                        statEl.textContent = action.hitChance;
                        btn.appendChild(statEl);
                    }
                }

                btn.onclick = () => this.respond(action.id);
                groupEl.appendChild(btn);
            });

            this.actionsEl.appendChild(groupEl);
        });
    }

    showOutcome(combatData) {
        hide(this.actionsEl);
        show(this.outcomeEl);

        const outcome = combatData.outcome;
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

    cleanup() {
        this.clear(this.actionsEl);
        this.clear(this.rewardsEl);
        this.clear(this.threatFactorsEl);
    }
}
