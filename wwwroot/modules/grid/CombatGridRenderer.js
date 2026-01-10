import { TERRAIN_COLORS, renderTerrainTexture } from './TerrainRenderer.js';

/**
 * CombatGridRenderer - Renders combat grid on the main canvas
 * Replaces the world map when in combat mode
 */
export class CombatGridRenderer {
    constructor() {
        this.canvas = null;
        this.ctx = null;
        this.combatState = null;
        this.onUnitClick = null;

        // Grid settings - combat uses meters, not tiles
        this.GRID_SIZE = 25;  // 25x25 meter grid
        this.CELL_SIZE = 20;  // pixels per meter (calculated dynamically)
        this.PADDING = 40;    // padding around grid

        // Bound resize handler
        this._boundResizeHandler = () => this.resizeCanvas();

        // State
        this.selectedUnit = null;
        this.animationId = null;

        // Animation state for units
        this.unitAnimations = new Map();  // unitId -> animation data

        // Colors
        this.COLORS = {
            background: 'hsl(215, 30%, 8%)',
            gridLine: 'rgba(255, 255, 255, 0.05)',
            player: { fill: 'hsl(210, 60%, 45%)', stroke: 'hsl(210, 70%, 60%)' },
            ally: { fill: 'hsl(120, 40%, 35%)', stroke: 'hsl(120, 50%, 50%)' },
            enemy: { fill: 'hsl(0, 60%, 40%)', stroke: 'hsl(0, 70%, 55%)' },
            selection: 'hsl(45, 100%, 60%)',
            zoneClose: 'rgba(255, 80, 80, 0.15)',
            zoneNear: 'rgba(255, 160, 80, 0.10)',
            zoneMid: 'rgba(255, 220, 80, 0.05)'
        };

        // Zone radii in meters
        this.ZONES = [
            { radius: 3, color: this.COLORS.zoneClose, label: 'CLOSE' },
            { radius: 8, color: this.COLORS.zoneNear, label: 'NEAR' },
            { radius: 15, color: this.COLORS.zoneMid, label: 'MID' }
        ];

        // Boldness colors
        this.BOLDNESS_COLORS = {
            aggressive: '#ff4444',
            bold: '#ff8844',
            wary: '#ffcc44',
            cautious: '#44cc44'
        };

        // Bound handlers
        this._boundHandleClick = (e) => this.handleClick(e);

        // Activation state
        this.isActive = false;
    }

    /**
     * Initialize the combat renderer on the given canvas
     * Note: Does NOT activate - call activate() separately to start rendering
     */
    init(canvas, onUnitClick) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.onUnitClick = onUnitClick;

        // Calculate initial size based on viewport
        this.resizeCanvas();

        // Listen for window resize
        window.addEventListener('resize', this._boundResizeHandler);
    }

    /**
     * Calculate optimal cell size based on viewport height (like CanvasGridRenderer)
     */
    calculateOptimalCellSize() {
        const viewportHeight = window.innerHeight;
        const verticalOverhead = 60;  // Account for UI elements
        const availableHeight = viewportHeight - verticalOverhead;
        const cellSize = Math.floor((availableHeight - this.PADDING * 2) / this.GRID_SIZE);
        // Clamp to reasonable range (16-32px per meter)
        return Math.max(16, Math.min(32, cellSize));
    }

    /**
     * Resize canvas based on viewport
     */
    resizeCanvas() {
        if (!this.canvas) return;

        this.CELL_SIZE = this.calculateOptimalCellSize();
        const canvasSize = this.GRID_SIZE * this.CELL_SIZE + this.PADDING * 2;

        // Always update canvas size (needed on init when switching from world grid)
        if (this.canvas.width !== canvasSize || this.canvas.height !== canvasSize) {
            this.canvas.width = canvasSize;
            this.canvas.height = canvasSize;
        }
    }

    /**
     * Update combat state
     */
    update(combatState) {
        // Track previous positions for animation
        if (this.combatState && combatState?.grid?.units) {
            for (const unit of combatState.grid.units) {
                const prev = this.combatState.grid?.units?.find(u => u.id === unit.id);
                if (prev && (prev.position.x !== unit.position.x || prev.position.y !== unit.position.y)) {
                    // Unit moved - start animation
                    this.unitAnimations.set(unit.id, {
                        fromX: prev.position.x,
                        fromY: prev.position.y,
                        toX: unit.position.x,
                        toY: unit.position.y,
                        startTime: performance.now(),
                        duration: 300
                    });
                }
            }
        }

        this.combatState = combatState;
    }

    /**
     * Get interpolated unit position (for animations)
     */
    getUnitPosition(unit) {
        const anim = this.unitAnimations.get(unit.id);
        if (!anim) {
            return { x: unit.position.x, y: unit.position.y };
        }

        const elapsed = performance.now() - anim.startTime;
        const progress = Math.min(1, elapsed / anim.duration);
        const eased = 1 - Math.pow(1 - progress, 3);  // easeOutCubic

        if (progress >= 1) {
            this.unitAnimations.delete(unit.id);
            return { x: unit.position.x, y: unit.position.y };
        }

        return {
            x: anim.fromX + (anim.toX - anim.fromX) * eased,
            y: anim.fromY + (anim.toY - anim.fromY) * eased
        };
    }

    /**
     * Convert grid position to canvas coordinates
     */
    gridToCanvas(gridX, gridY) {
        return {
            x: this.PADDING + gridX * this.CELL_SIZE,
            y: this.PADDING + gridY * this.CELL_SIZE
        };
    }

    /**
     * Convert canvas coordinates to grid position
     */
    canvasToGrid(canvasX, canvasY) {
        return {
            x: Math.floor((canvasX - this.PADDING) / this.CELL_SIZE),
            y: Math.floor((canvasY - this.PADDING) / this.CELL_SIZE)
        };
    }

    /**
     * Main render function
     */
    render() {
        if (!this.ctx || !this.canvas) return;

        const ctx = this.ctx;
        const canvasSize = this.canvas.width;
        const grid = this.combatState?.grid;

        // Render terrain background if available, otherwise solid background
        if (grid?.terrain && grid.locationX != null && grid.locationY != null) {
            const baseColor = TERRAIN_COLORS[grid.terrain] || TERRAIN_COLORS.Plain;
            ctx.fillStyle = baseColor;
            ctx.fillRect(0, 0, canvasSize, canvasSize);

            // Tile terrain texture across combat grid at normal resolution
            const tileSize = this.CELL_SIZE * 5; // 5 meters per texture tile
            const gridPixels = this.GRID_SIZE * this.CELL_SIZE;
            const tilesNeeded = Math.ceil(gridPixels / tileSize);

            for (let ty = 0; ty < tilesNeeded; ty++) {
                for (let tx = 0; tx < tilesNeeded; tx++) {
                    const px = this.PADDING + tx * tileSize;
                    const py = this.PADDING + ty * tileSize;
                    renderTerrainTexture(
                        ctx,
                        grid.terrain,
                        px,
                        py,
                        tileSize,
                        grid.locationX + tx,
                        grid.locationY + ty
                    );
                }
            }
        } else {
            // Fallback to solid background if no terrain data
            ctx.fillStyle = this.COLORS.background;
            ctx.fillRect(0, 0, canvasSize, canvasSize);
        }

        if (!grid) {
            this.renderLoading();
            return;
        }

        // Find player unit for zone rendering
        const playerUnit = grid.units?.find(u => u.team === 'player');

        // Render layers
        this.renderGrid();
        if (playerUnit) {
            this.renderZones(playerUnit);
        }
        this.renderUnits();
        this.renderDistanceInfo();
    }

    /**
     * Render loading state
     */
    renderLoading() {
        const ctx = this.ctx;
        ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
        ctx.font = "14px 'JetBrains Mono', monospace";
        ctx.textAlign = 'center';
        ctx.fillText('Combat starting...', this.canvas.width / 2, this.canvas.height / 2);
    }

    /**
     * Render grid lines
     */
    renderGrid() {
        const ctx = this.ctx;
        ctx.strokeStyle = this.COLORS.gridLine;
        ctx.lineWidth = 1;

        // Vertical lines
        for (let x = 0; x <= this.GRID_SIZE; x++) {
            const canvasX = this.PADDING + x * this.CELL_SIZE;
            ctx.beginPath();
            ctx.moveTo(canvasX, this.PADDING);
            ctx.lineTo(canvasX, this.PADDING + this.GRID_SIZE * this.CELL_SIZE);
            ctx.stroke();
        }

        // Horizontal lines
        for (let y = 0; y <= this.GRID_SIZE; y++) {
            const canvasY = this.PADDING + y * this.CELL_SIZE;
            ctx.beginPath();
            ctx.moveTo(this.PADDING, canvasY);
            ctx.lineTo(this.PADDING + this.GRID_SIZE * this.CELL_SIZE, canvasY);
            ctx.stroke();
        }
    }

    /**
     * Render distance zones around player
     */
    renderZones(playerUnit) {
        const ctx = this.ctx;
        const pos = this.getUnitPosition(playerUnit);
        const { x: centerX, y: centerY } = this.gridToCanvas(pos.x + 0.5, pos.y + 0.5);

        // Draw zones from largest to smallest
        for (let i = this.ZONES.length - 1; i >= 0; i--) {
            const zone = this.ZONES[i];
            const radiusPx = zone.radius * this.CELL_SIZE;

            // Filled zone
            ctx.beginPath();
            ctx.arc(centerX, centerY, radiusPx, 0, Math.PI * 2);
            ctx.fillStyle = zone.color;
            ctx.fill();

            // Dashed ring outline
            ctx.setLineDash([4, 4]);
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
            ctx.lineWidth = 1;
            ctx.stroke();
            ctx.setLineDash([]);

            // Zone label
            ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
            ctx.font = "10px 'JetBrains Mono', monospace";
            ctx.textAlign = 'center';
            ctx.fillText(zone.label, centerX, centerY - radiusPx + 12);
        }
    }

    /**
     * Render all units
     */
    renderUnits() {
        if (!this.combatState?.grid?.units) return;

        for (const unit of this.combatState.grid.units) {
            this.renderUnit(unit);
        }
    }

    /**
     * Render a single unit with 3D shadow, vitality bar, and status badge
     */
    renderUnit(unit) {
        const ctx = this.ctx;
        const pos = this.getUnitPosition(unit);
        const { x, y } = this.gridToCanvas(pos.x + 0.5, pos.y + 0.5);
        const radius = this.CELL_SIZE * 0.7;

        const isSelected = this.selectedUnit?.id === unit.id;
        const colors = this.COLORS[unit.team] || this.COLORS.enemy;

        // 3D elliptical shadow beneath unit (wider than tall)
        const shadowWidth = radius * 1.3;
        ctx.save();
        ctx.translate(x, y + radius * 0.6);
        ctx.scale(1, 0.4);
        ctx.beginPath();
        ctx.arc(0, 0, shadowWidth, 0, Math.PI * 2);
        const shadowGradient = ctx.createRadialGradient(0, 0, 0, 0, 0, shadowWidth);
        shadowGradient.addColorStop(0, 'rgba(0, 0, 0, 0.5)');
        shadowGradient.addColorStop(0.6, 'rgba(0, 0, 0, 0.25)');
        shadowGradient.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = shadowGradient;
        ctx.fill();
        ctx.restore();

        // Selection ring
        if (isSelected) {
            ctx.beginPath();
            ctx.arc(x, y, radius + 4, 0, Math.PI * 2);
            ctx.strokeStyle = this.COLORS.selection;
            ctx.lineWidth = 3;
            ctx.stroke();
        }

        // Unit icon (emoji)
        ctx.font = `${this.CELL_SIZE * 0.8}px sans-serif`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(unit.icon || '?', x, y);

        // Vitality bar above unit
        if (unit.vitality < 1) {
            const barWidth = radius * 1.6;
            const barHeight = 5;
            const barX = x - barWidth / 2;
            const barY = y - radius - 12;

            // Background bar
            ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
            ctx.fillRect(barX, barY, barWidth, barHeight);

            // Health fill
            const fillWidth = barWidth * unit.vitality;
            const healthColor = this.getHealthColor(unit.vitality);
            ctx.fillStyle = healthColor;
            ctx.fillRect(barX, barY, fillWidth, barHeight);

            // Border
            ctx.strokeStyle = 'rgba(0, 0, 0, 0.8)';
            ctx.lineWidth = 1;
            ctx.strokeRect(barX, barY, barWidth, barHeight);
        }

        // Status badge (for enemies with boldness descriptor)
        if (unit.team === 'enemy' && unit.boldnessDescriptor) {
            const badgeText = unit.boldnessDescriptor.toUpperCase();
            const boldnessColor = this.BOLDNESS_COLORS[unit.boldnessDescriptor] || '#888888';

            // Measure text for badge sizing
            ctx.font = "bold 9px 'JetBrains Mono', monospace";
            const textMetrics = ctx.measureText(badgeText);
            const badgeWidth = textMetrics.width + 12;
            const badgeHeight = 14;
            const badgeX = x - badgeWidth / 2;
            const badgeY = y - radius - 28;

            // Badge background
            ctx.fillStyle = boldnessColor + 'e6';
            ctx.fillRect(badgeX, badgeY, badgeWidth, badgeHeight);

            // Badge border
            ctx.strokeStyle = boldnessColor;
            ctx.lineWidth = 1;
            ctx.strokeRect(badgeX, badgeY, badgeWidth, badgeHeight);

            // Badge text
            ctx.fillStyle = '#d4cfc4';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(badgeText, x, badgeY + badgeHeight / 2);
        }
    }

    /**
     * Get health bar color
     */
    getHealthColor(vitality) {
        if (vitality > 0.7) return '#44cc44';
        if (vitality > 0.4) return '#cccc44';
        return '#cc4444';
    }

    /**
     * Render distance and zone info
     */
    renderDistanceInfo() {
        if (!this.combatState) return;

        const ctx = this.ctx;
        const distance = this.combatState.distanceMeters;
        const zone = this.combatState.distanceZone;

        // Distance display in top-left
        ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
        ctx.fillRect(10, 10, 100, 40);

        ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
        ctx.font = "bold 16px 'JetBrains Mono', monospace";
        ctx.textAlign = 'left';
        ctx.fillText(`${Math.round(distance)}m`, 20, 30);

        ctx.fillStyle = 'rgba(255, 255, 255, 0.6)';
        ctx.font = "12px 'JetBrains Mono', monospace";
        ctx.fillText(zone?.toUpperCase() || '', 20, 45);
    }

    /**
     * Handle click events
     */
    handleClick(e) {
        if (!this.combatState?.grid?.units) return;

        const rect = this.canvas.getBoundingClientRect();
        const scaleX = this.canvas.width / rect.width;
        const scaleY = this.canvas.height / rect.height;
        const canvasX = (e.clientX - rect.left) * scaleX;
        const canvasY = (e.clientY - rect.top) * scaleY;

        // Check if clicked on a unit
        for (const unit of this.combatState.grid.units) {
            const pos = this.getUnitPosition(unit);
            const { x, y } = this.gridToCanvas(pos.x + 0.5, pos.y + 0.5);
            const radius = this.CELL_SIZE * 0.7;

            const dx = canvasX - x;
            const dy = canvasY - y;
            if (Math.sqrt(dx * dx + dy * dy) <= radius) {
                this.selectedUnit = unit;
                if (this.onUnitClick) {
                    this.onUnitClick(unit);
                }
                return;
            }
        }

        // Clicked elsewhere - deselect
        this.selectedUnit = null;
        if (this.onUnitClick) {
            this.onUnitClick(null);
        }
    }

    /**
     * Start animation loop
     */
    startAnimation() {
        const animate = () => {
            this.render();
            this.animationId = requestAnimationFrame(animate);
        };
        animate();
    }

    /**
     * Stop animation loop
     */
    stopAnimation() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
    }

    /**
     * Activate the renderer - add event handlers and start animation
     */
    activate() {
        if (this.isActive) return;

        this.canvas.addEventListener('click', this._boundHandleClick);
        this.startAnimation();
        this.isActive = true;
    }

    /**
     * Deactivate the renderer - remove event handlers, stop animation, clear state
     */
    deactivate() {
        if (!this.isActive) return;

        this.canvas.removeEventListener('click', this._boundHandleClick);
        this.stopAnimation();
        this.combatState = null;
        this.selectedUnit = null;
        this.unitAnimations.clear();
        this.isActive = false;
    }

    /**
     * Clean up
     */
    destroy() {
        this.stopAnimation();
        window.removeEventListener('resize', this._boundResizeHandler);
        if (this.canvas) {
            this.canvas.removeEventListener('click', this._boundHandleClick);
        }
        this.combatState = null;
        this.selectedUnit = null;
        this.unitAnimations.clear();
    }
}

// Singleton instance
let combatRendererInstance = null;

export function getCombatGridRenderer() {
    if (!combatRendererInstance) {
        combatRendererInstance = new CombatGridRenderer();
    }
    return combatRendererInstance;
}
