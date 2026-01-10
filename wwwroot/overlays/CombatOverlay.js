import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
import { Utils, show, hide } from '../modules/utils.js';
import { TERRAIN_COLORS, renderTerrainTexture } from '../modules/grid/TerrainRenderer.js';

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

        // Combat grid elements (left pane)
        this.gridPaneEl = document.getElementById('combatGridPane');
        this.gridCanvasEl = document.getElementById('combatGridCanvas');
        this.gridCtx = this.gridCanvasEl?.getContext('2d');

        // Grid settings
        this.GRID_SIZE = 25;          // 25x25 grid
        this.CELL_SIZE = 20;          // pixels per cell
        this.GRID_PADDING = 2;        // padding around grid

        // Store current units for click detection
        this.currentUnits = [];
        this.selectedUnit = null;

        // Unit detail panel
        this.unitDetailEl = document.getElementById('combatUnitDetail');

        // Set up grid click handler
        if (this.gridCanvasEl) {
            this.gridCanvasEl.addEventListener('click', (e) => this.handleGridClick(e));
            this.gridCanvasEl.style.cursor = 'pointer';
        }

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

        // Get enemy name from grid units if needed for fallback
        const enemyUnit = combatData.grid?.units?.find(u => u.team?.toLowerCase() === 'enemy');
        const enemyName = enemyUnit?.name?.toLowerCase() || 'enemy';

        // Set intro message
        this.introMessageEl.textContent = combatData.narrativeMessage || `A ${enemyName} lunges at you!`;
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

        // Hide legacy single-enemy header elements - info now via grid click
        hide(this.behaviorDescEl);
        hide(this.animalHealthEl);
        hide(this.animalNameEl?.parentElement); // Hide the whole animal status row

        // Render 2D combat grid
        this.renderGrid(combatData);

        // Update combat state display
        this.updateDistanceBar(combatData);
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

        // Render 2D combat grid (keep visual context)
        this.renderGrid(combatData);

        // Update bars (keep visual context)
        this.updateDistanceBar(combatData);

        // Hide legacy header elements
        hide(this.animalNameEl?.parentElement);
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

        // Auto-advance after delay if specified (for AI turns)
        if (combatData.autoAdvanceMs) {
            // Clear any existing auto-advance timer
            if (this._autoAdvanceTimer) {
                clearTimeout(this._autoAdvanceTimer);
            }
            this._autoAdvanceTimer = setTimeout(() => {
                this._autoAdvanceTimer = null;
                this.respond('continue');
            }, combatData.autoAdvanceMs);
        }
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

    /**
     * Render the 2D combat grid showing unit positions
     */
    renderGrid(combatData) {
        if (!this.gridCanvasEl || !this.gridCtx || !combatData.grid) {
            return;
        }

        const grid = combatData.grid;
        const gridSize = grid.gridSize || this.GRID_SIZE;
        const canvasSize = gridSize * this.CELL_SIZE + this.GRID_PADDING * 2;

        // Store data for click detection
        this.currentUnits = grid.units || [];
        this.lastCombatData = combatData;

        // Set canvas size
        this.gridCanvasEl.width = canvasSize;
        this.gridCanvasEl.height = canvasSize;

        const ctx = this.gridCtx;

        // Draw terrain background if available
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

        // Draw distance zone rings from player position
        const cellSizeMeters = grid.cellSizeMeters || 1;
        const playerUnit = this.currentUnits.find(u => u.team?.toLowerCase() === 'player');
        if (playerUnit) {
            const playerX = this.GRID_PADDING + playerUnit.position.x * this.CELL_SIZE + this.CELL_SIZE / 2;
            const playerY = this.GRID_PADDING + playerUnit.position.y * this.CELL_SIZE + this.CELL_SIZE / 2;

            const zones = [
                { radius: 3, color: 'rgba(255, 80, 80, 0.15)' },   // Close - red
                { radius: 8, color: 'rgba(255, 160, 80, 0.1)' },   // Near - orange
                { radius: 15, color: 'rgba(255, 220, 80, 0.05)' }  // Mid - yellow
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
            const isSelected = this.selectedUnit && this.selectedUnit.id === unit.id;
            this.drawUnit(ctx, unit, gridSize, isSelected);
        });
    }

    /**
     * Handle click on combat grid canvas
     */
    handleGridClick(event) {
        const rect = this.gridCanvasEl.getBoundingClientRect();
        const clickX = event.clientX - rect.left;
        const clickY = event.clientY - rect.top;

        // Convert to grid coordinates
        const gridX = (clickX - this.GRID_PADDING) / this.CELL_SIZE;
        const gridY = (clickY - this.GRID_PADDING) / this.CELL_SIZE;

        // Find unit at click position (within 1 cell tolerance)
        const clickedUnit = this.currentUnits.find(unit => {
            const dx = unit.position.x + 0.5 - gridX;
            const dy = unit.position.y + 0.5 - gridY;
            return Math.sqrt(dx * dx + dy * dy) < 0.7;
        });

        if (clickedUnit) {
            this.selectedUnit = clickedUnit;
            this.showUnitDetail(clickedUnit);
            // Redraw grid to show selection
            if (this.lastCombatData) {
                this.renderGrid(this.lastCombatData);
            }
        } else {
            this.selectedUnit = null;
            this.hideUnitDetail();
        }
    }

    /**
     * Show detail panel for clicked unit (using safe DOM methods)
     */
    showUnitDetail(unit) {
        if (!this.unitDetailEl) return;

        // Clear existing content
        this.clear(this.unitDetailEl);

        const isEnemy = unit.team?.toLowerCase() === 'enemy';

        // Header row
        const header = document.createElement('div');
        header.className = 'unit-detail-header';

        const nameSpan = document.createElement('span');
        nameSpan.className = 'unit-detail-name';
        nameSpan.textContent = unit.name;
        header.appendChild(nameSpan);

        const teamSpan = document.createElement('span');
        teamSpan.className = 'unit-detail-team ' + unit.team;
        teamSpan.textContent = unit.team;
        header.appendChild(teamSpan);

        this.unitDetailEl.appendChild(header);

        // Health section
        const healthDiv = document.createElement('div');
        healthDiv.className = 'unit-detail-health';

        const barDiv = document.createElement('div');
        barDiv.className = 'unit-detail-bar';

        const fillDiv = document.createElement('div');
        fillDiv.className = 'unit-detail-bar-fill';
        fillDiv.style.width = (unit.vitality * 100).toFixed(0) + '%';
        barDiv.appendChild(fillDiv);
        healthDiv.appendChild(barDiv);

        const healthText = document.createElement('span');
        healthText.className = 'unit-detail-health-text';
        healthText.textContent = unit.healthDescription;
        healthDiv.appendChild(healthText);

        this.unitDetailEl.appendChild(healthDiv);

        // Show boldness for enemies
        if (isEnemy) {
            const boldnessDiv = document.createElement('div');
            boldnessDiv.className = 'unit-detail-boldness';

            const boldnessLabel = document.createElement('span');
            boldnessLabel.className = 'unit-detail-boldness-label';
            boldnessLabel.textContent = 'Behavior:';
            boldnessDiv.appendChild(boldnessLabel);

            const boldnessValue = document.createElement('span');
            boldnessValue.className = 'unit-detail-boldness-value ' + unit.boldnessDescriptor;
            boldnessValue.textContent = unit.boldnessDescriptor;
            boldnessDiv.appendChild(boldnessValue);

            this.unitDetailEl.appendChild(boldnessDiv);
        }

        this.unitDetailEl.classList.add('visible');
    }

    /**
     * Hide unit detail panel
     */
    hideUnitDetail() {
        if (this.unitDetailEl) {
            this.unitDetailEl.classList.remove('visible');
        }
    }

    /**
     * Draw a single unit on the combat grid
     */
    drawUnit(ctx, unit, gridSize, isSelected) {
        const x = this.GRID_PADDING + unit.position.x * this.CELL_SIZE + this.CELL_SIZE / 2;
        const y = this.GRID_PADDING + unit.position.y * this.CELL_SIZE + this.CELL_SIZE / 2;
        const radius = this.CELL_SIZE * 0.35;

        // Color based on team, icon from DTO (matches world map)
        let fillColor, strokeColor;
        const icon = unit.icon || '🐾';  // Use icon from backend, fallback to paw
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

        // Draw unit circle
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fillStyle = fillColor;
        ctx.fill();
        ctx.strokeStyle = strokeColor;
        ctx.lineWidth = 2;
        ctx.stroke();

        // Draw icon/emoji in center
        ctx.font = `${this.CELL_SIZE * 0.5}px sans-serif`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(icon, x, y);

        // Draw boldness indicator for enemies (color dot based on aggression)
        if (unit.team?.toLowerCase() === 'enemy') {
            const indicatorColor = this.getBoldnessColor(unit.boldnessDescriptor);
            ctx.beginPath();
            ctx.arc(x + radius * 0.7, y - radius * 0.7, 4, 0, Math.PI * 2);
            ctx.fillStyle = indicatorColor;
            ctx.fill();
        }
    }

    /**
     * Get color for boldness descriptor
     */
    getBoldnessColor(descriptor) {
        switch ((descriptor || '').toLowerCase()) {
            case 'aggressive':
                return '#ff4444';  // Red
            case 'bold':
                return '#ff8844';  // Orange
            case 'wary':
                return '#ffcc44';  // Yellow
            case 'cautious':
                return '#44cc44';  // Green
            default:
                return '#888888';  // Gray
        }
    }

    cleanup() {
        // Clear auto-advance timer if running
        if (this._autoAdvanceTimer) {
            clearTimeout(this._autoAdvanceTimer);
            this._autoAdvanceTimer = null;
        }

        // Clear DOM contents
        this.clear(this.actionsEl);
        this.clear(this.rewardsEl);
        this.clear(this.threatFactorsEl);
        this.clear(this.encounterChoicesEl);
        this.clear(this.encounterFactorsEl);
        this.clear(this.encounterOutcomeEl);

        // Hide all sub-sections that might be visible
        if (this.introSectionEl) hide(this.introSectionEl);
        if (this.introActionsEl) hide(this.introActionsEl);
        if (this.outcomeEl) hide(this.outcomeEl);
        if (this.outcomeActionsEl) hide(this.outcomeActionsEl);
        if (this.actionMessageEl) hide(this.actionMessageEl);
        if (this.combatContentEl) hide(this.combatContentEl);

        // Hide encounter overlay
        const encounterOverlay = document.getElementById('encounterOverlay');
        if (encounterOverlay) hide(encounterOverlay);

        // Reset distance marker animation state
        if (this.playerMarkerEl) {
            this.playerMarkerEl.style.transition = 'none';
        }

        // Reset grid click state
        this.currentUnits = [];
        this.selectedUnit = null;
        this.lastCombatData = null;
        this.hideUnitDetail();
    }
}
