// overlays/CombatOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, button, clear, show, hide } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';
import { ActionButton, ContinueButton } from '../lib/components/ActionButton.js';
import { Animator } from '../core/Animator.js';
import { TERRAIN_COLORS, renderTerrainTexture } from '../modules/grid/TerrainRenderer.js';

/**
 * CombatOverlay - Multi-phase combat interface
 * Handles both predator approach (encounter) and active combat phases
 */
export class CombatOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('combatOverlay', inputHandler);

        // Grid settings
        this.GRID_SIZE = 25;
        this.CELL_SIZE = 20;
        this.GRID_PADDING = 2;

        // State for grid interaction
        this.currentUnits = [];
        this.selectedUnit = null;
        this.lastCombatData = null;
        this._autoAdvanceTimer = null;
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Set up grid click handler (once)
        this.setupGridClickHandler();

        // Set up button handlers
        const introConfirmBtn = this.$('#combatIntroConfirm');
        if (introConfirmBtn) introConfirmBtn.onclick = () => this.respond('confirm');

        const outcomeContinueBtn = this.$('#combatOutcomeContinueBtn');
        if (outcomeContinueBtn) outcomeContinueBtn.onclick = () => this.respond('continue');

        // Phase detection
        const isApproachPhase = data.choices !== undefined;
        const isCombatPhase = data.phase !== undefined || data.actions !== undefined;
        const isOutcomePhase = data.outcome != null;

        if (isApproachPhase && !isOutcomePhase) {
            this.renderApproachPhase(data);
        } else if (isOutcomePhase) {
            if (isApproachPhase) {
                this.showApproachOutcome(data);
            } else {
                this.showCombatOutcome(data);
            }
        } else {
            this.renderCombatPhase(data);
        }
    }

    setupGridClickHandler() {
        const gridCanvas = this.$('#combatGridCanvas');
        if (gridCanvas && !gridCanvas._clickHandlerAttached) {
            gridCanvas.addEventListener('click', (e) => this.handleGridClick(e));
            gridCanvas.style.cursor = 'pointer';
            gridCanvas._clickHandlerAttached = true;
        }
    }

    // ========== APPROACH PHASE ==========

    renderApproachPhase(data) {
        hide(this.$('#combatActionMessage'));
        hide(this.$('#combatPlayerBraced'));
        hide(this.$('#combatOutcome'));
        hide(this.$('#combatActions'));
        show(this.$('#encounterOverlay'));

        const targetEl = this.$('#encounterTarget');
        if (targetEl) targetEl.textContent = data.predatorName || data.animalName;

        this.updateApproachDistanceBar(data);
        this.updateBoldness(data);
        this.updateApproachThreatFactors(data.threatFactors || []);

        const messageEl = this.$('#encounterMessage');
        if (messageEl) {
            if (data.statusMessage) {
                messageEl.textContent = data.statusMessage;
                show(messageEl);
            } else {
                hide(messageEl);
            }
        }

        this.showApproachChoices(data);
    }

    updateApproachDistanceBar(data) {
        const maxDistance = 100;
        const distanceMask = this.$('#encounterDistanceMask');
        const distanceValue = this.$('#encounterDistanceValue');

        if (!distanceMask || !distanceValue) return;

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

    updateBoldness(data) {
        const boldness = data.boldnessLevel || 0;
        const descriptor = data.boldnessDescriptor || 'wary';

        const descriptorEl = this.$('#encounterBoldnessDescriptor');
        if (descriptorEl) {
            descriptorEl.textContent = descriptor;
            descriptorEl.className = 'encounter-boldness-descriptor ' + descriptor;
        }

        const fillEl = this.$('#encounterBoldnessFill');
        if (fillEl) {
            fillEl.style.width = (boldness * 100) + '%';
            fillEl.className = 'encounter-boldness-fill ' + descriptor;
        }
    }

    updateApproachThreatFactors(factors) {
        const factorsEl = this.$('#encounterFactors');
        if (!factorsEl) return;
        clear(factorsEl);

        if (!factors || factors.length === 0) {
            hide(factorsEl);
            return;
        }

        show(factorsEl);
        factors.forEach(factor => {
            factorsEl.appendChild(
                span({ className: 'encounter-factor' },
                    Icon(factor.icon || 'warning'),
                    factor.description
                )
            );
        });
    }

    showApproachChoices(data) {
        const choicesEl = this.$('#encounterChoices');
        const outcomeEl = this.$('#encounterOutcome');

        hide(outcomeEl);
        show(choicesEl);

        if (!choicesEl) return;
        clear(choicesEl);

        if (!data.choices || !Array.isArray(data.choices)) {
            console.warn('No choices available in approach phase data');
            return;
        }

        data.choices.forEach(choice => {
            choicesEl.appendChild(
                ActionButton({
                    label: choice.label,
                    description: choice.description,
                    disabled: !choice.isAvailable,
                    disabledReason: choice.disabledReason
                }, () => this.respond(choice.id))
            );
        });
    }

    showApproachOutcome(data) {
        const choicesEl = this.$('#encounterChoices');
        const outcomeEl = this.$('#encounterOutcome');
        const outcomeMessageEl = this.$('#encounterOutcomeMessage');

        hide(choicesEl);
        show(outcomeEl);

        const outcome = data.outcome;
        if (!outcome || !outcomeEl) return;

        outcomeEl.className = 'encounter-outcome ' + (outcome.result || '').toLowerCase();
        if (outcomeMessageEl) outcomeMessageEl.textContent = outcome.message;

        clear(outcomeEl);
        if (outcomeMessageEl) outcomeEl.appendChild(outcomeMessageEl);

        outcomeEl.appendChild(
            ContinueButton(
                () => this.respond('continue'),
                outcome.result === 'fight' ? 'Fight!' : 'Continue'
            )
        );
    }

    // ========== COMBAT PHASE ==========

    renderCombatPhase(data) {
        hide(this.$('#encounterOverlay'));

        const animalNameEl = this.$('#combatAnimalName');
        if (animalNameEl) animalNameEl.textContent = data.animalName || 'Enemy';

        const phase = data.phase?.toLowerCase() || 'playerChoice';

        switch (phase) {
            case 'intro':
                this.renderIntroPhase(data);
                break;
            case 'playerchoice':
                this.renderChoicePhase(data);
                break;
            case 'playeraction':
            case 'animalaction':
            case 'behaviorchange':
                this.renderNarrativePhase(data, 'Continue');
                break;
            case 'outcome':
                this.showCombatOutcome(data);
                break;
            default:
                this.renderChoicePhase(data);
        }
    }

    renderIntroPhase(data) {
        show(this.$('#combatIntroSection'));
        show(this.$('#combatIntroActions'));
        hide(this.$('#combatContent'));

        const enemyUnit = data.grid?.units?.find(u => u.team?.toLowerCase() === 'enemy');
        const enemyName = enemyUnit?.name?.toLowerCase() || 'enemy';

        const introMessageEl = this.$('#combatIntroMessage');
        if (introMessageEl) {
            introMessageEl.textContent = data.narrativeMessage || `A ${enemyName} lunges at you!`;
        }
    }

    renderChoicePhase(data) {
        hide(this.$('#combatIntroSection'));
        hide(this.$('#combatIntroActions'));
        hide(this.$('#combatOutcomeActions'));
        show(this.$('#combatContent'));

        // Hide legacy single-enemy header elements
        hide(this.$('#combatBehaviorDesc'));
        hide(this.$('#combatAnimalHealth'));
        const animalNameEl = this.$('#combatAnimalName');
        if (animalNameEl?.parentElement) hide(animalNameEl.parentElement);

        this.renderGrid(data);
        this.updateDistanceBar(data);
        this.updateThreatFactors(data);

        const bracedEl = this.$('#combatPlayerBraced');
        if (bracedEl) {
            data.playerBraced ? show(bracedEl) : hide(bracedEl);
        }

        hide(this.$('#combatActionMessage'));
        hide(this.$('#combatOutcome'));

        this.showActions(data);
    }

    renderNarrativePhase(data, buttonText) {
        hide(this.$('#combatIntroSection'));
        show(this.$('#combatContent'));

        this.renderGrid(data);
        this.updateDistanceBar(data);

        // Hide status elements
        const animalNameEl = this.$('#combatAnimalName');
        if (animalNameEl?.parentElement) hide(animalNameEl.parentElement);
        hide(this.$('#combatBehaviorDesc'));
        hide(this.$('#combatAnimalHealth'));
        hide(this.$('#combatThreatSection'));
        hide(this.$('#combatPlayerBraced'));

        const actionMessageEl = this.$('#combatActionMessage');
        if (actionMessageEl) {
            if (data.narrativeMessage) {
                actionMessageEl.textContent = data.narrativeMessage;
                show(actionMessageEl);
            } else {
                hide(actionMessageEl);
            }
        }

        hide(this.$('#combatOutcome'));

        // Show continue button
        const actionsEl = this.$('#combatActions');
        if (actionsEl) {
            clear(actionsEl);
            actionsEl.appendChild(ContinueButton(() => this.respond('continue'), buttonText));
            show(actionsEl);
        }

        // Auto-advance for AI turns
        if (data.autoAdvanceMs) {
            if (this._autoAdvanceTimer) clearTimeout(this._autoAdvanceTimer);
            this._autoAdvanceTimer = setTimeout(() => {
                this._autoAdvanceTimer = null;
                this.respond('continue');
            }, data.autoAdvanceMs);
        }
    }

    updateDistanceBar(data) {
        const maxDistance = 25;
        const currentDistance = data.distanceMeters || 0;
        const prevDistance = data.previousDistanceMeters;

        const distanceValueEl = this.$('#combatDistanceValue');
        if (distanceValueEl) distanceValueEl.textContent = `${Math.round(currentDistance)}m`;

        const playerMarkerEl = this.$('#combatPlayerMarker');
        if (playerMarkerEl) {
            const positionPct = 100 - (currentDistance / maxDistance * 100);
            const clampedPct = Math.max(0, Math.min(100, positionPct));

            const shouldAnimate = prevDistance != null && Math.abs(prevDistance - currentDistance) > 0.5;
            playerMarkerEl.style.transition = shouldAnimate ? 'left 0.5s ease-out' : 'none';
            playerMarkerEl.style.left = clampedPct + '%';
        }
    }

    updateThreatFactors(data) {
        const threatFactorsEl = this.$('#combatThreatFactors');
        const threatSectionEl = this.$('#combatThreatSection');

        if (!threatFactorsEl) return;
        clear(threatFactorsEl);

        const factors = data.threatFactors || [];

        if (factors.length === 0) {
            hide(threatSectionEl);
            return;
        }

        show(threatSectionEl);

        factors.forEach(factor => {
            const isDanger = ['meat', 'weakness', 'blood', 'bleeding'].includes(factor.id);
            threatFactorsEl.appendChild(
                span({ className: `badge threat-factor ${isDanger ? 'danger' : 'advantage'}` },
                    Icon(factor.icon || 'info'),
                    factor.description
                )
            );
        });
    }

    showActions(data) {
        hide(this.$('#combatOutcome'));
        const actionsEl = this.$('#combatActions');
        if (!actionsEl) return;
        show(actionsEl);
        clear(actionsEl);

        data.actions?.forEach(action => {
            actionsEl.appendChild(
                ActionButton(action, () => this.respond(action.id))
            );
        });
    }

    showCombatOutcome(data) {
        hide(this.$('#combatActions'));
        hide(this.$('#combatActionMessage'));
        hide(this.$('#combatIntroActions'));
        hide(this.$('#encounterOverlay'));
        show(this.$('#combatOutcome'));
        show(this.$('#combatOutcomeActions'));

        const outcome = data.outcome;
        if (!outcome) return;

        const outcomeEl = this.$('#combatOutcome');
        if (outcomeEl) outcomeEl.className = 'combat-outcome ' + (outcome.result || '').toLowerCase();

        const outcomeMessageEl = this.$('#combatOutcomeMessage');
        if (outcomeMessageEl) outcomeMessageEl.textContent = outcome.message;

        const rewardsEl = this.$('#combatRewards');
        if (rewardsEl) {
            clear(rewardsEl);
            if (outcome.rewards?.length > 0) {
                show(rewardsEl);
                outcome.rewards.forEach(reward => {
                    rewardsEl.appendChild(
                        div({ className: 'combat-reward-item' }, '+ ' + reward)
                    );
                });
            } else {
                hide(rewardsEl);
            }
        }

        const outcomeContinueBtn = this.$('#combatOutcomeContinueBtn');
        if (outcomeContinueBtn) outcomeContinueBtn.textContent = 'Continue';
    }

    // ========== GRID RENDERING ==========

    renderGrid(data) {
        const gridCanvas = this.$('#combatGridCanvas');
        const ctx = gridCanvas?.getContext('2d');

        if (!gridCanvas || !ctx || !data.grid) return;

        const grid = data.grid;
        const gridSize = grid.gridSize || this.GRID_SIZE;
        const canvasSize = gridSize * this.CELL_SIZE + this.GRID_PADDING * 2;

        this.currentUnits = grid.units || [];
        this.lastCombatData = data;

        gridCanvas.width = canvasSize;
        gridCanvas.height = canvasSize;

        // Draw terrain background
        if (grid.terrain && grid.locationX != null && grid.locationY != null) {
            const baseColor = TERRAIN_COLORS[grid.terrain] || TERRAIN_COLORS.Plain;
            ctx.fillStyle = baseColor;
            ctx.fillRect(0, 0, canvasSize, canvasSize);

            const tileSize = 120;
            const tilesNeeded = Math.ceil(canvasSize / tileSize);

            for (let ty = 0; ty < tilesNeeded; ty++) {
                for (let tx = 0; tx < tilesNeeded; tx++) {
                    const seedX = grid.locationX * 10 + tx;
                    const seedY = grid.locationY * 10 + ty;
                    renderTerrainTexture(ctx, grid.terrain, tx * tileSize, ty * tileSize, tileSize, seedX, seedY);
                }
            }
        } else {
            ctx.fillStyle = 'hsl(215, 25%, 12%)';
            ctx.fillRect(0, 0, canvasSize, canvasSize);
        }

        // Draw distance zone rings
        const cellSizeMeters = grid.cellSizeMeters || 1;
        const playerUnit = this.currentUnits.find(u => u.team?.toLowerCase() === 'player');

        if (playerUnit) {
            const playerX = this.GRID_PADDING + playerUnit.position.x * this.CELL_SIZE + this.CELL_SIZE / 2;
            const playerY = this.GRID_PADDING + playerUnit.position.y * this.CELL_SIZE + this.CELL_SIZE / 2;

            const zones = [
                { radius: 3, color: 'rgba(255, 80, 80, 0.15)' },
                { radius: 8, color: 'rgba(255, 160, 80, 0.1)' },
                { radius: 15, color: 'rgba(255, 220, 80, 0.05)' }
            ];

            zones.forEach(zone => {
                const radiusPx = (zone.radius / cellSizeMeters) * this.CELL_SIZE;
                ctx.beginPath();
                ctx.arc(playerX, playerY, radiusPx, 0, Math.PI * 2);
                ctx.fillStyle = zone.color;
                ctx.fill();
            });
        }

        // Draw units
        this.currentUnits.forEach(unit => {
            const isSelected = this.selectedUnit?.id === unit.id;
            this.drawUnit(ctx, unit, gridSize, isSelected);
        });
    }

    drawUnit(ctx, unit, gridSize, isSelected) {
        const x = this.GRID_PADDING + unit.position.x * this.CELL_SIZE + this.CELL_SIZE / 2;
        const y = this.GRID_PADDING + unit.position.y * this.CELL_SIZE + this.CELL_SIZE / 2;
        const radius = this.CELL_SIZE * 0.35;

        const icon = unit.icon || '🐾';
        let fillColor, strokeColor;

        switch (unit.team?.toLowerCase()) {
            case 'player':
                fillColor = 'hsl(210, 60%, 45%)';
                strokeColor = 'hsl(210, 70%, 60%)';
                break;
            case 'ally':
                fillColor = 'hsl(120, 40%, 35%)';
                strokeColor = 'hsl(120, 50%, 50%)';
                break;
            case 'enemy':
            default:
                fillColor = 'hsl(0, 60%, 40%)';
                strokeColor = 'hsl(0, 70%, 55%)';
                break;
        }

        // Selection highlight
        if (isSelected) {
            ctx.beginPath();
            ctx.arc(x, y, radius + 4, 0, Math.PI * 2);
            ctx.strokeStyle = 'hsl(45, 100%, 60%)';
            ctx.lineWidth = 3;
            ctx.stroke();
        }

        // Unit circle
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fillStyle = fillColor;
        ctx.fill();
        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = 2;
        ctx.stroke();

        // Icon
        ctx.font = `${this.CELL_SIZE * 0.5}px sans-serif`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(icon, x, y);

        // Boldness indicator for enemies
        if (unit.team?.toLowerCase() === 'enemy') {
            const indicatorColor = this.getBoldnessColor(unit.boldnessDescriptor);
            ctx.beginPath();
            ctx.arc(x + radius * 0.7, y - radius * 0.7, 4, 0, Math.PI * 2);
            ctx.fillStyle = indicatorColor;
            ctx.fill();
        }
    }

    getBoldnessColor(descriptor) {
        switch ((descriptor || '').toLowerCase()) {
            case 'aggressive': return '#ff4444';
            case 'bold': return '#ff8844';
            case 'wary': return '#ffcc44';
            case 'cautious': return '#44cc44';
            default: return '#888888';
        }
    }

    handleGridClick(event) {
        const gridCanvas = this.$('#combatGridCanvas');
        if (!gridCanvas) return;

        const rect = gridCanvas.getBoundingClientRect();
        const clickX = event.clientX - rect.left;
        const clickY = event.clientY - rect.top;

        const gridX = (clickX - this.GRID_PADDING) / this.CELL_SIZE;
        const gridY = (clickY - this.GRID_PADDING) / this.CELL_SIZE;

        const clickedUnit = this.currentUnits.find(unit => {
            const dx = unit.position.x + 0.5 - gridX;
            const dy = unit.position.y + 0.5 - gridY;
            return Math.sqrt(dx * dx + dy * dy) < 0.7;
        });

        if (clickedUnit) {
            this.selectedUnit = clickedUnit;
            this.showUnitDetail(clickedUnit);
            if (this.lastCombatData) this.renderGrid(this.lastCombatData);
        } else {
            this.selectedUnit = null;
            this.hideUnitDetail();
        }
    }

    showUnitDetail(unit) {
        const unitDetailEl = this.$('#combatUnitDetail');
        if (!unitDetailEl) return;
        clear(unitDetailEl);

        const isEnemy = unit.team?.toLowerCase() === 'enemy';

        // Header
        unitDetailEl.appendChild(
            div({ className: 'unit-detail-header' },
                span({ className: 'unit-detail-name' }, unit.name),
                span({ className: 'unit-detail-team ' + unit.team }, unit.team)
            )
        );

        // Health
        unitDetailEl.appendChild(
            div({ className: 'unit-detail-health' },
                div({ className: 'unit-detail-bar' },
                    div({
                        className: 'unit-detail-bar-fill',
                        style: { width: (unit.vitality * 100).toFixed(0) + '%' }
                    })
                ),
                span({ className: 'unit-detail-health-text' }, unit.healthDescription)
            )
        );

        // Boldness for enemies
        if (isEnemy) {
            unitDetailEl.appendChild(
                div({ className: 'unit-detail-boldness' },
                    span({ className: 'unit-detail-boldness-label' }, 'Behavior:'),
                    span({ className: 'unit-detail-boldness-value ' + unit.boldnessDescriptor }, unit.boldnessDescriptor)
                )
            );
        }

        unitDetailEl.classList.add('visible');
    }

    hideUnitDetail() {
        const unitDetailEl = this.$('#combatUnitDetail');
        if (unitDetailEl) unitDetailEl.classList.remove('visible');
    }

    // ========== CLEANUP ==========

    hide() {
        super.hide();

        if (this._autoAdvanceTimer) {
            clearTimeout(this._autoAdvanceTimer);
            this._autoAdvanceTimer = null;
        }

        // Clear various elements
        clear(this.$('#combatActions'));
        clear(this.$('#combatRewards'));
        clear(this.$('#combatThreatFactors'));
        clear(this.$('#encounterChoices'));
        clear(this.$('#encounterFactors'));
        clear(this.$('#encounterOutcome'));

        // Hide sections
        hide(this.$('#combatIntroSection'));
        hide(this.$('#combatIntroActions'));
        hide(this.$('#combatOutcome'));
        hide(this.$('#combatOutcomeActions'));
        hide(this.$('#combatActionMessage'));
        hide(this.$('#combatContent'));
        hide(this.$('#encounterOverlay'));

        // Reset marker
        const playerMarkerEl = this.$('#combatPlayerMarker');
        if (playerMarkerEl) playerMarkerEl.style.transition = 'none';

        // Reset grid state
        this.currentUnits = [];
        this.selectedUnit = null;
        this.lastCombatData = null;
        this.hideUnitDetail();
    }
}
