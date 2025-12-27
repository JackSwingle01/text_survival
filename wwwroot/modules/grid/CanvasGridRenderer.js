/**
 * CanvasGridRenderer - Canvas-based tile grid rendering
 * Replaces Phaser-based GridScene with direct canvas rendering
 */
export class CanvasGridRenderer {
    constructor() {
        this.canvas = null;
        this.ctx = null;
        this.gridState = null;
        this.onMoveRequest = null;

        // Grid settings
        this.TILE_SIZE = 80;
        this.GAP = 3;
        this.VIEW_SIZE = 7;

        // State
        this.currentHover = null;
        this.playerHistory = [];
        this.snowParticles = [];
        this.animationId = null;

        // Colors
        this.COLORS = {
            midnight: 'hsl(215, 30%, 5%)',
            panel: 'hsl(215, 25%, 8%)',
            borderDim: 'rgba(255, 255, 255, 0.1)',
            textDim: 'rgba(255, 255, 255, 0.5)',
            fireOrange: '#e08830',
            techCyan: '#60a0b0',
            vitalRed: '#a05050'
        };

        this.TERRAIN_COLORS = {
            Forest: '#1a2530',
            Clearing: '#2a3a48',
            Plain: '#253040',
            Hills: '#1e2832',
            Water: '#1a3040',
            Marsh: '#1a3040',
            Rock: '#2a2a30',
            Mountain: '#151518',
            DeepWater: '#101828',
            unexplored: '#080a0c'
        };

        this.ICONS = {
            fire: 'local_fire_department',
            forage: 'eco',
            harvest: 'forest',
            animals: 'pets',
            water: 'water_drop',
            cache: 'inventory_2',
            shelter: 'camping',
            wood: 'carpenter',
            trap: 'trap',
            curing: 'dry_cleaning',
            project: 'construction',
            salvage: 'recycling'
        };
    }

    /**
     * Initialize the canvas renderer
     */
    init(canvasId, onMoveRequest) {
        console.log('[GridRenderer] Initializing with canvas:', canvasId);
        this.onMoveRequest = onMoveRequest;
        this.canvas = document.getElementById(canvasId);

        if (!this.canvas) {
            console.error('[GridRenderer] Canvas not found:', canvasId);
            return;
        }

        console.log('[GridRenderer] Canvas found, setting up context');
        this.ctx = this.canvas.getContext('2d');

        // Set canvas size
        this.canvas.width = this.VIEW_SIZE * this.TILE_SIZE + (this.VIEW_SIZE - 1) * this.GAP;
        this.canvas.height = this.VIEW_SIZE * this.TILE_SIZE + (this.VIEW_SIZE - 1) * this.GAP;

        // Initialize snow particles
        this.initSnowParticles();

        // Set up event handlers
        this.canvas.addEventListener('mousemove', (e) => this.handleMouseMove(e));
        this.canvas.addEventListener('mouseleave', () => this.handleMouseLeave());
        this.canvas.addEventListener('click', () => this.handleClick());

        // Start animation loop
        this.startAnimation();
    }

    /**
     * Initialize snow particle system
     */
    initSnowParticles() {
        this.snowParticles = [];
        for (let i = 0; i < 25; i++) {
            this.snowParticles.push({
                x: Math.random() * this.canvas.width,
                y: Math.random() * this.canvas.height,
                speed: 0.3 + Math.random() * 0.6,
                size: 1 + Math.random() * 1.2,
                drift: Math.random() * Math.PI * 2
            });
        }
    }

    /**
     * Update grid state from server
     */
    update(gridState) {
        console.log('[GridRenderer] Received grid state:', gridState ? `${gridState.tiles?.length} tiles` : 'null');
        this.gridState = gridState;
    }

    /**
     * Add position to movement history (for footprints)
     */
    addToHistory(x, y) {
        this.playerHistory.unshift({ x, y });
        if (this.playerHistory.length > 3) {
            this.playerHistory.pop();
        }
    }

    /**
     * Convert view coordinates to screen position
     */
    getTileScreenPos(vx, vy) {
        return {
            px: vx * (this.TILE_SIZE + this.GAP),
            py: vy * (this.TILE_SIZE + this.GAP)
        };
    }

    /**
     * Convert world coordinates to view coordinates
     */
    worldToView(worldX, worldY) {
        if (!this.gridState) return { vx: -1, vy: -1 };

        const viewCenterX = Math.floor(this.VIEW_SIZE / 2);
        const viewCenterY = Math.floor(this.VIEW_SIZE / 2);
        return {
            vx: worldX - this.gridState.playerX + viewCenterX,
            vy: worldY - this.gridState.playerY + viewCenterY
        };
    }

    /**
     * Convert view coordinates to world coordinates
     */
    viewToWorld(vx, vy) {
        if (!this.gridState) return { x: -1, y: -1 };

        const viewCenterX = Math.floor(this.VIEW_SIZE / 2);
        const viewCenterY = Math.floor(this.VIEW_SIZE / 2);
        return {
            x: vx - viewCenterX + this.gridState.playerX,
            y: vy - viewCenterY + this.gridState.playerY
        };
    }

    /**
     * Find tile data at world coordinates
     */
    findTile(worldX, worldY) {
        if (!this.gridState || !this.gridState.tiles) return null;
        return this.gridState.tiles.find(t => t.x === worldX && t.y === worldY);
    }

    /**
     * Draw a material icon at position
     */
    drawMaterialIcon(icon, x, y, size, color, alpha = 1) {
        this.ctx.save();
        this.ctx.font = `${size}px 'Material Symbols Outlined'`;
        this.ctx.textAlign = 'center';
        this.ctx.textBaseline = 'middle';
        this.ctx.globalAlpha = alpha;
        this.ctx.fillStyle = color;
        this.ctx.fillText(icon, x, y);
        this.ctx.restore();
    }

    /**
     * Main render function
     */
    render() {
        const ctx = this.ctx;

        // Clear canvas
        ctx.fillStyle = this.COLORS.midnight;
        ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);

        if (!this.gridState) {
            // Draw loading state
            ctx.fillStyle = this.COLORS.textDim;
            ctx.font = "14px 'JetBrains Mono', monospace";
            ctx.textAlign = 'center';
            ctx.fillText('Loading...', this.canvas.width / 2, this.canvas.height / 2);
            return;
        }

        // Render tiles
        for (let vy = 0; vy < this.VIEW_SIZE; vy++) {
            for (let vx = 0; vx < this.VIEW_SIZE; vx++) {
                const { x: worldX, y: worldY } = this.viewToWorld(vx, vy);
                const { px, py } = this.getTileScreenPos(vx, vy);

                const tile = this.findTile(worldX, worldY);

                if (!tile) {
                    // Out of bounds or no tile data
                    ctx.fillStyle = this.TERRAIN_COLORS.unexplored;
                    ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
                    continue;
                }

                this.renderTile(tile, px, py, worldX, worldY);
            }
        }

        // Render footprints
        this.renderFootprints();

        // Render snow particles
        this.renderSnow();

        // Render vignette
        this.renderVignette();
    }

    /**
     * Render a single tile
     */
    renderTile(tile, px, py, worldX, worldY) {
        const ctx = this.ctx;
        const isPlayer = worldX === this.gridState.playerX && worldY === this.gridState.playerY;
        const isAdjacent = tile.isAdjacent && tile.isPassable;
        const isVisible = tile.visibility === 'visible';
        const isExplored = tile.visibility === 'explored';
        const isHovered = this.currentHover &&
                          this.currentHover.x === worldX &&
                          this.currentHover.y === worldY;

        // Draw unexplored tiles
        if (tile.visibility === 'unexplored') {
            ctx.fillStyle = this.TERRAIN_COLORS.unexplored;
            ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.02)';
            ctx.lineWidth = 1;
            ctx.strokeRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
            return;
        }

        // Draw terrain base
        const terrainColor = this.TERRAIN_COLORS[tile.terrain] || this.TERRAIN_COLORS.Plain;
        ctx.fillStyle = terrainColor;
        ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);

        // Draw terrain textures for visible tiles
        if (isVisible) {
            this.renderTerrainTexture(tile.terrain, px, py);
        }

        // Apply fog of war for explored but not visible
        if (isExplored && !isVisible) {
            ctx.fillStyle = 'rgba(8, 10, 12, 0.65)';
            ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
        }

        // Draw tile border and highlights
        if (isPlayer) {
            ctx.strokeStyle = this.COLORS.fireOrange;
            ctx.lineWidth = 2;

            // Player tile glow
            const gradient = ctx.createRadialGradient(
                px + this.TILE_SIZE/2, py + this.TILE_SIZE/2, 0,
                px + this.TILE_SIZE/2, py + this.TILE_SIZE/2, this.TILE_SIZE * 0.6
            );
            gradient.addColorStop(0, 'rgba(224, 136, 48, 0.12)');
            gradient.addColorStop(1, 'rgba(224, 136, 48, 0)');
            ctx.fillStyle = gradient;
            ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
        } else if (isHovered && isAdjacent) {
            ctx.fillStyle = 'rgba(96, 160, 176, 0.12)';
            ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
            ctx.strokeStyle = this.COLORS.techCyan;
            ctx.lineWidth = 2;
        } else if (isAdjacent) {
            ctx.strokeStyle = 'rgba(96, 160, 176, 0.25)';
            ctx.lineWidth = 1;
        } else {
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
            ctx.lineWidth = 1;
        }

        ctx.strokeRect(px, py, this.TILE_SIZE, this.TILE_SIZE);

        // Draw hazard indicator
        if (tile.isHazardous && isVisible) {
            ctx.strokeStyle = 'rgba(184, 115, 51, 0.6)';
            ctx.lineWidth = 2;
            ctx.strokeRect(px + 2, py + 2, this.TILE_SIZE - 4, this.TILE_SIZE - 4);
        }

        // Corner marker
        ctx.fillStyle = 'rgba(255, 255, 255, 0.08)';
        ctx.fillRect(px + this.TILE_SIZE - 5, py + this.TILE_SIZE - 5, 3, 1);
        ctx.fillRect(px + this.TILE_SIZE - 3, py + this.TILE_SIZE - 5, 1, 3);

        // Draw location name
        if (isVisible && tile.locationName) {
            ctx.font = "500 8px 'Oswald', sans-serif";
            ctx.textAlign = 'left';
            ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
            ctx.fillText(tile.locationName.toUpperCase(), px + 4, py + 10);
        }

        // Draw feature icons
        if (isVisible && tile.featureIcons && tile.featureIcons.length > 0) {
            this.renderFeatureIcons(tile.featureIcons, px, py, tile.hasFire);
        }

        // Draw fire glow
        if (tile.hasFire && isVisible) {
            const gradient = ctx.createRadialGradient(
                px + this.TILE_SIZE * 0.28, py + this.TILE_SIZE * 0.38, 0,
                px + this.TILE_SIZE * 0.28, py + this.TILE_SIZE * 0.38, this.TILE_SIZE * 1.3
            );
            gradient.addColorStop(0, 'rgba(224, 136, 48, 0.15)');
            gradient.addColorStop(0.4, 'rgba(224, 136, 48, 0.05)');
            gradient.addColorStop(1, 'rgba(224, 136, 48, 0)');
            ctx.fillStyle = gradient;
            ctx.fillRect(px - this.TILE_SIZE, py - this.TILE_SIZE, this.TILE_SIZE * 3, this.TILE_SIZE * 3);
        }

        // Draw player icon
        if (isPlayer) {
            this.drawMaterialIcon('person_pin_circle',
                px + this.TILE_SIZE/2,
                py + this.TILE_SIZE/2 + 4,
                26, this.COLORS.fireOrange, 1);
        }
    }

    /**
     * Render terrain texture details
     */
    renderTerrainTexture(terrain, px, py) {
        const ctx = this.ctx;

        if (['Plain', 'Clearing', 'Hills'].includes(terrain)) {
            // Snow specks
            ctx.fillStyle = 'rgba(255, 255, 255, 0.06)';
            for (let i = 0; i < 5; i++) {
                const sx = px + 8 + (i * 15) % this.TILE_SIZE;
                const sy = py + 6 + (i * 19) % this.TILE_SIZE;
                ctx.fillRect(sx, sy, 2, 2);
            }
        }

        if (terrain === 'Water' || terrain === 'Marsh') {
            // Ice cracks
            ctx.strokeStyle = 'rgba(96, 160, 176, 0.2)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(px + 15, py + 12);
            ctx.lineTo(px + 40, py + 35);
            ctx.lineTo(px + 65, py + 30);
            ctx.stroke();
        }

        if (terrain === 'Forest') {
            // Tree silhouettes
            ctx.fillStyle = 'rgba(10, 15, 20, 0.4)';
            [[0.2, 0.3], [0.7, 0.4], [0.5, 0.75]].forEach(([ox, oy]) => {
                const tx = px + this.TILE_SIZE * ox;
                const ty = py + this.TILE_SIZE * oy;
                ctx.beginPath();
                ctx.moveTo(tx, ty);
                ctx.lineTo(tx - 3, ty + 8);
                ctx.lineTo(tx + 3, ty + 8);
                ctx.closePath();
                ctx.fill();
            });
        }
    }

    /**
     * Render feature icons on a tile
     */
    renderFeatureIcons(features, px, py, hasFire) {
        const iconPositions = [
            { ox: this.TILE_SIZE * 0.28, oy: this.TILE_SIZE * 0.38 },
            { ox: this.TILE_SIZE * 0.72, oy: this.TILE_SIZE * 0.38 },
            { ox: this.TILE_SIZE * 0.28, oy: this.TILE_SIZE * 0.73 },
            { ox: this.TILE_SIZE * 0.72, oy: this.TILE_SIZE * 0.73 }
        ];

        features.slice(0, 4).forEach((feature, i) => {
            const iconName = this.ICONS[feature];
            if (!iconName) return;

            const pos = iconPositions[i];
            const iconX = px + pos.ox;
            const iconY = py + pos.oy;

            let color = 'rgba(255, 255, 255, 0.45)';
            let glow = false;

            if (feature === 'fire') {
                color = this.COLORS.fireOrange;
                glow = true;
            } else if (feature === 'water') {
                color = this.COLORS.techCyan;
            }

            if (glow) {
                this.ctx.save();
                this.ctx.shadowColor = color;
                this.ctx.shadowBlur = 6;
            }

            this.drawMaterialIcon(iconName, iconX, iconY, 18, color, 0.8);

            if (glow) {
                this.ctx.restore();
            }
        });
    }

    /**
     * Render movement history footprints
     */
    renderFootprints() {
        if (!this.gridState) return;

        this.playerHistory.forEach((pos, i) => {
            const { vx, vy } = this.worldToView(pos.x, pos.y);
            if (vx >= 0 && vx < this.VIEW_SIZE && vy >= 0 && vy < this.VIEW_SIZE) {
                const { px, py } = this.getTileScreenPos(vx, vy);
                const alpha = 0.2 - (i * 0.06);
                if (alpha > 0) {
                    this.ctx.fillStyle = `rgba(96, 160, 176, ${alpha})`;
                    this.ctx.fillRect(px + this.TILE_SIZE/2 - 6, py + this.TILE_SIZE/2 - 2, 4, 6);
                    this.ctx.fillRect(px + this.TILE_SIZE/2 + 2, py + this.TILE_SIZE/2 - 2, 4, 6);
                }
            }
        });
    }

    /**
     * Render falling snow particles
     */
    renderSnow() {
        this.ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
        this.snowParticles.forEach(p => {
            p.y += p.speed;
            p.x += Math.sin(p.drift + p.y * 0.01) * 0.2;
            if (p.y > this.canvas.height + 10) {
                p.y = -10;
                p.x = Math.random() * this.canvas.width;
            }
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
            this.ctx.fill();
        });
    }

    /**
     * Render vignette effect
     */
    renderVignette() {
        const vignette = this.ctx.createRadialGradient(
            this.canvas.width/2, this.canvas.height/2, this.canvas.height * 0.3,
            this.canvas.width/2, this.canvas.height/2, this.canvas.height * 0.7
        );
        vignette.addColorStop(0, 'rgba(8, 10, 14, 0)');
        vignette.addColorStop(1, 'rgba(8, 10, 14, 0.35)');
        this.ctx.fillStyle = vignette;
        this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
    }

    /**
     * Handle mouse move events
     */
    handleMouseMove(e) {
        if (!this.gridState) return;

        const rect = this.canvas.getBoundingClientRect();
        const mx = e.clientX - rect.left;
        const my = e.clientY - rect.top;
        const vx = Math.floor(mx / (this.TILE_SIZE + this.GAP));
        const vy = Math.floor(my / (this.TILE_SIZE + this.GAP));

        if (vx >= 0 && vx < this.VIEW_SIZE && vy >= 0 && vy < this.VIEW_SIZE) {
            const { x: worldX, y: worldY } = this.viewToWorld(vx, vy);
            const tile = this.findTile(worldX, worldY);

            if (tile && tile.isAdjacent && tile.isPassable) {
                this.currentHover = { x: worldX, y: worldY };
            } else {
                this.currentHover = null;
            }
        } else {
            this.currentHover = null;
        }
    }

    /**
     * Handle mouse leave events
     */
    handleMouseLeave() {
        this.currentHover = null;
    }

    /**
     * Handle click events
     */
    handleClick() {
        console.log('[GridRenderer] Click detected, currentHover:', this.currentHover);
        if (this.currentHover && this.onMoveRequest) {
            const tile = this.findTile(this.currentHover.x, this.currentHover.y);
            console.log('[GridRenderer] Sending move request to:', this.currentHover.x, this.currentHover.y);

            // Add current position to history before moving
            if (this.gridState) {
                this.addToHistory(this.gridState.playerX, this.gridState.playerY);
            }

            this.onMoveRequest(this.currentHover.x, this.currentHover.y, tile);
            this.currentHover = null;
        } else {
            console.log('[GridRenderer] Click ignored - no valid hover or no callback');
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

        // Wait for fonts to load before starting
        if (document.fonts && document.fonts.ready) {
            document.fonts.ready.then(() => animate());
        } else {
            animate();
        }
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
     * Destroy the renderer
     */
    destroy() {
        this.stopAnimation();
        if (this.canvas) {
            this.canvas.removeEventListener('mousemove', this.handleMouseMove);
            this.canvas.removeEventListener('mouseleave', this.handleMouseLeave);
            this.canvas.removeEventListener('click', this.handleClick);
        }
    }
}

// Singleton instance
let rendererInstance = null;

export function getGridRenderer() {
    if (!rendererInstance) {
        rendererInstance = new CanvasGridRenderer();
    }
    return rendererInstance;
}
