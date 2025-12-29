/**
 * CanvasGridRenderer - Canvas-based tile grid rendering
 * Replaces Phaser-based GridScene with direct canvas rendering
 */
export class CanvasGridRenderer {
    constructor() {
        this.canvas = null;
        this.ctx = null;
        this.gridState = null;
        this.onTileClick = null;

        // Grid settings
        this.TILE_SIZE = 120;
        this.GAP = 3;
        this.VIEW_SIZE = 7;

        // State
        this.currentHover = null;
        this.playerHistory = [];
        this.snowParticles = [];
        this.animationId = null;

        // Camera transition animation
        this.cameraOffsetX = 0;
        this.cameraOffsetY = 0;
        this.currentOffsetX = 0;
        this.currentOffsetY = 0;
        this.transitionStartTime = null;
        this.TRANSITION_DURATION = 300;  // ms
        this.synchronizedPan = false;  // True when animated pan is in progress
        this.panOriginX = null;  // Origin position for player icon animation
        this.panOriginY = null;

        // Time of day factor (0 = midnight, 1 = noon)
        this.timeFactor = 0.5;

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
            Forest: '#2a4038',      // Dark green - evergreen cover
            Clearing: '#a8b8c0',    // Medium gray - snowy clearing
            Plain: '#c8d0d8',       // Light gray - open snow
            Hills: '#8090a0',       // Blue-gray - snow-dusted hills
            Water: '#90b0c8',       // Light ice blue - frozen
            Marsh: '#607068',       // Muted green-gray - frozen wetland
            Rock: '#606068',        // Medium gray - exposed stone
            Mountain: '#404048',    // Dark gray - high peaks
            DeepWater: '#6090b0',   // Deeper blue - thick ice
            unexplored: '#080a0c'   // Nearly black
        };

        // Icons with special styling (glow, color)
        this.SPECIAL_ICONS = {
            'local_fire_department': { color: '#e08830', glow: true },  // Fire
            'fireplace': { color: '#a06030', glow: true },              // Embers
            'water_drop': { color: '#60a0b0', glow: false },            // Water
            'catching_pokemon': { color: '#e0a030', glow: true }        // Catch ready!
        };

        // Bound event handlers for proper add/remove
        this._boundHandleMouseMove = (e) => this.handleMouseMove(e);
        this._boundHandleMouseLeave = () => this.handleMouseLeave();
        this._boundHandleClick = (e) => this.handleClick(e);
    }

    /**
     * Seeded random for consistent terrain patterns per tile
     */
    seededRandom(worldX, worldY, seed) {
        const h = (worldX * 73856093) ^ (worldY * 19349663) ^ (seed * 83492791);
        return Math.abs(Math.sin(h)) % 1.0;
    }

    /**
     * Initialize the canvas renderer
     */
    init(canvasId, onTileClick) {
        this.onTileClick = onTileClick;
        this.canvas = document.getElementById(canvasId);

        if (!this.canvas) {
            console.error('[GridRenderer] Canvas not found:', canvasId);
            return;
        }

        this.ctx = this.canvas.getContext('2d');

        // Set canvas size
        this.canvas.width = this.VIEW_SIZE * this.TILE_SIZE + (this.VIEW_SIZE - 1) * this.GAP;
        this.canvas.height = this.VIEW_SIZE * this.TILE_SIZE + (this.VIEW_SIZE - 1) * this.GAP;

        // Initialize snow particles
        this.initSnowParticles();

        // Remove any existing handlers first (idempotent init)
        this.canvas.removeEventListener('mousemove', this._boundHandleMouseMove);
        this.canvas.removeEventListener('mouseleave', this._boundHandleMouseLeave);
        this.canvas.removeEventListener('click', this._boundHandleClick);

        // Set up event handlers using bound references
        this.canvas.addEventListener('mousemove', this._boundHandleMouseMove);
        this.canvas.addEventListener('mouseleave', this._boundHandleMouseLeave);
        this.canvas.addEventListener('click', this._boundHandleClick);

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
        // Detect player movement and start camera transition
        // Skip auto-animation if synchronized pan is in progress
        if (this.gridState && gridState && !this.synchronizedPan) {
            const oldX = this.gridState.playerX;
            const oldY = this.gridState.playerY;
            const newX = gridState.playerX;
            const newY = gridState.playerY;

            if (oldX !== newX || oldY !== newY) {
                // Calculate pixel displacement (camera moves opposite to player)
                const cellSize = this.TILE_SIZE + this.GAP;
                this.cameraOffsetX = (newX - oldX) * cellSize;
                this.cameraOffsetY = (newY - oldY) * cellSize;
                this.transitionStartTime = performance.now();
                this.TRANSITION_DURATION = 300;  // Reset to default duration
            }
        }

        this.gridState = gridState;
    }

    /**
     * Easing function for smooth deceleration
     */
    easeOutCubic(t) {
        return 1 - Math.pow(1 - t, 3);
    }

    /**
     * Set time of day factor for night dimming
     * @param {number} factor - 0 = midnight (darkest), 1 = noon (brightest)
     */
    setTimeFactor(factor) {
        this.timeFactor = Math.max(0, Math.min(1, factor));
    }

    /**
     * Get the current lightness multiplier based on time of day
     * At noon (t=1): returns 1.0 (full brightness)
     * At midnight (t=0): returns ~0.19 (matching 5/26 ratio from background)
     */
    getLightnessFactor() {
        const minFactor = 0.19;  // 5/26 ≈ 0.19 (midnight/noon ratio from background)
        return minFactor + (1 - minFactor) * this.timeFactor;
    }

    /**
     * Adjust a hex color's lightness based on time of day
     * @param {string} hexColor - Hex color like '#2a4038'
     * @returns {string} - Adjusted hex color
     */
    adjustHexForTime(hexColor) {
        // Parse hex to RGB
        const hex = hexColor.replace('#', '');
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);

        // Convert RGB to HSL
        const rNorm = r / 255, gNorm = g / 255, bNorm = b / 255;
        const max = Math.max(rNorm, gNorm, bNorm);
        const min = Math.min(rNorm, gNorm, bNorm);
        let h, s, l = (max + min) / 2;

        if (max === min) {
            h = s = 0;
        } else {
            const d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            switch (max) {
                case rNorm: h = ((gNorm - bNorm) / d + (gNorm < bNorm ? 6 : 0)) / 6; break;
                case gNorm: h = ((bNorm - rNorm) / d + 2) / 6; break;
                case bNorm: h = ((rNorm - gNorm) / d + 4) / 6; break;
            }
        }

        // Adjust lightness
        l *= this.getLightnessFactor();

        // Convert HSL back to RGB
        let rOut, gOut, bOut;
        if (s === 0) {
            rOut = gOut = bOut = l;
        } else {
            const hue2rgb = (p, q, t) => {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1/6) return p + (q - p) * 6 * t;
                if (t < 1/2) return q;
                if (t < 2/3) return p + (q - p) * (2/3 - t) * 6;
                return p;
            };
            const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            const p = 2 * l - q;
            rOut = hue2rgb(p, q, h + 1/3);
            gOut = hue2rgb(p, q, h);
            bOut = hue2rgb(p, q, h - 1/3);
        }

        // Convert back to hex
        const toHex = (v) => Math.round(v * 255).toString(16).padStart(2, '0');
        return `#${toHex(rOut)}${toHex(gOut)}${toHex(bOut)}`;
    }

    /**
     * Get the canvas background color adjusted for time of day
     */
    getBackgroundColor() {
        // Base: hsl(215, 30%, 5%) at midnight, brighter during day
        const baseLightness = 5;
        const adjustedLightness = baseLightness + (21 * this.timeFactor);  // 5-26% range like main bg
        return `hsl(215, 30%, ${adjustedLightness.toFixed(1)}%)`;
    }

    /**
     * Update camera offset animation
     */
    updateCameraTransition() {
        if (this.transitionStartTime === null) {
            this.currentOffsetX = 0;
            this.currentOffsetY = 0;
            return;
        }

        const elapsed = performance.now() - this.transitionStartTime;
        const progress = Math.min(1, elapsed / this.TRANSITION_DURATION);
        const eased = this.easeOutCubic(progress);

        // Interpolate offset toward zero
        this.currentOffsetX = this.cameraOffsetX * (1 - eased);
        this.currentOffsetY = this.cameraOffsetY * (1 - eased);

        if (progress >= 1) {
            this.transitionStartTime = null;
            this.currentOffsetX = 0;
            this.currentOffsetY = 0;
            this.synchronizedPan = false;  // Clear synchronized pan flag
            this.panOriginX = null;  // Clear origin for player icon animation
            this.panOriginY = null;
        }
    }

    /**
     * Start a synchronized camera pan from origin to current position.
     * Used for travel progress to animate camera movement over the progress duration.
     * @param {number} originX - Origin grid X position
     * @param {number} originY - Origin grid Y position
     * @param {number} durationMs - Animation duration in milliseconds
     */
    startAnimatedPan(originX, originY, durationMs) {
        if (!this.gridState) return;

        const cellSize = this.TILE_SIZE + this.GAP;
        const destX = this.gridState.playerX;
        const destY = this.gridState.playerY;

        // Calculate offset: same as auto-animation (dest - origin)
        // This shifts tiles so we visually start at origin, then animate to dest
        this.cameraOffsetX = (destX - originX) * cellSize;
        this.cameraOffsetY = (destY - originY) * cellSize;

        // Store origin for player icon animation
        this.panOriginX = originX;
        this.panOriginY = originY;

        // Start transition with custom duration
        this.transitionStartTime = performance.now();
        this.TRANSITION_DURATION = durationMs;
        this.synchronizedPan = true;  // Mark as synchronized pan to skip auto-animation
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
     * Convert view coordinates to screen position (with camera offset applied)
     */
    getTileScreenPos(vx, vy) {
        return {
            px: vx * (this.TILE_SIZE + this.GAP) + this.currentOffsetX,
            py: vy * (this.TILE_SIZE + this.GAP) + this.currentOffsetY
        };
    }

    /**
     * Get the visual (CSS-scaled) tile size in screen pixels
     */
    getVisualTileSize() {
        if (!this.canvas) return this.TILE_SIZE;
        const rect = this.canvas.getBoundingClientRect();
        const scale = rect.width / this.canvas.width;
        return (this.TILE_SIZE + this.GAP) * scale;
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
     * Draw a material icon at position with optional outline for contrast
     */
    drawMaterialIcon(icon, x, y, size, color, alpha = 1) {
        this.ctx.save();

        this.ctx.font = `${size}px 'Material Symbols Outlined'`;
        this.ctx.textAlign = 'center';
        this.ctx.textBaseline = 'middle';

        // Draw dark outline for contrast (softer at night)
        const outlineAlpha = 0.3 + (0.3 * this.timeFactor);  // 0.3-0.6 range
        this.ctx.strokeStyle = `rgba(0, 0, 0, ${outlineAlpha})`;
        this.ctx.lineWidth = 2 + this.timeFactor;  // 2-3 range
        this.ctx.strokeText(icon, x, y);

        // Draw icon
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

        // Update camera transition animation
        this.updateCameraTransition();

        // Clear canvas with time-adjusted background
        ctx.fillStyle = this.getBackgroundColor();
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
                    ctx.fillStyle = this.adjustHexForTime(this.TERRAIN_COLORS.unexplored);
                    ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
                    continue;
                }

                this.renderTile(tile, px, py, worldX, worldY);
            }
        }

        // Render player icon (after tiles so it's always on top)
        this.renderPlayer();

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
            ctx.fillStyle = this.adjustHexForTime(this.TERRAIN_COLORS.unexplored);
            ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.02)';
            ctx.lineWidth = 1;
            ctx.strokeRect(px, py, this.TILE_SIZE, this.TILE_SIZE);
            return;
        }

        // Draw terrain base with time-adjusted color
        const baseTerrainColor = this.TERRAIN_COLORS[tile.terrain] || this.TERRAIN_COLORS.Plain;
        ctx.fillStyle = this.adjustHexForTime(baseTerrainColor);
        ctx.fillRect(px, py, this.TILE_SIZE, this.TILE_SIZE);

        // Draw terrain textures for visible tiles
        if (isVisible) {
            this.renderTerrainTexture(tile.terrain, px, py, worldX, worldY);
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

        // Draw location name with badge background (matches event choice button theme)
        if (isVisible && tile.locationName) {
            const nameText = tile.locationName.toUpperCase();
            ctx.font = "500 10px 'Oswald', sans-serif";
            ctx.letterSpacing = '1px';

            // Measure text to size badge
            const textMetrics = ctx.measureText(nameText);
            const textWidth = textMetrics.width;
            const badgePaddingX = 10;
            const badgePaddingY = 5;
            const badgeWidth = textWidth + badgePaddingX * 2;
            const badgeHeight = 20;
            const badgeX = px + 4;
            const badgeY = py + 4;

            // Draw square badge background - matches --bg-surface (#1d2734)
            ctx.fillStyle = 'rgb(31, 39, 51)';
            ctx.fillRect(badgeX, badgeY, badgeWidth, badgeHeight);

            // Draw border (matching event choice button theme - 2px border)
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
            ctx.lineWidth = 2;
            ctx.strokeRect(badgeX, badgeY, badgeWidth, badgeHeight);

            // Draw text
            ctx.textAlign = 'left';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
            ctx.fillText(nameText, badgeX + badgePaddingX, badgeY + badgeHeight / 2);
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

    }

    /**
     * Render player icon on top of all tiles
     */
    renderPlayer() {
        if (!this.gridState) return;

        const viewCenterX = Math.floor(this.VIEW_SIZE / 2);
        const viewCenterY = Math.floor(this.VIEW_SIZE / 2);
        const { px, py } = this.getTileScreenPos(viewCenterX, viewCenterY);

        let playerX = px + this.TILE_SIZE/2;
        let playerY = py + this.TILE_SIZE/2 + 4;

        // During synchronized pan, animate player icon from origin to destination
        // Uses linear interpolation to match progress bar timing
        if (this.synchronizedPan && this.panOriginX !== null) {
            const elapsed = performance.now() - this.transitionStartTime;
            const progress = Math.min(1, elapsed / this.TRANSITION_DURATION);

            // Player offset: starts at (origin - dest), animates to 0 (linear to match progress bar)
            const cellSize = this.TILE_SIZE + this.GAP;
            const offsetX = (this.panOriginX - this.gridState.playerX) * cellSize * (1 - progress);
            const offsetY = (this.panOriginY - this.gridState.playerY) * cellSize * (1 - progress);

            playerX += offsetX;
            playerY += offsetY;
        }

        this.drawMaterialIcon('person_pin_circle', playerX, playerY, 26, this.COLORS.fireOrange, 1);
    }

    /**
     * Render terrain texture details - makes each terrain type immediately recognizable
     */
    renderTerrainTexture(terrain, px, py, worldX, worldY) {
        const ctx = this.ctx;
        const size = this.TILE_SIZE;

        switch (terrain) {
            case 'Forest':
                this.renderForestTexture(ctx, px, py, worldX, worldY);
                break;
            case 'Water':
            case 'DeepWater':
                this.renderWaterTexture(ctx, px, py, worldX, worldY, terrain === 'DeepWater');
                break;
            case 'Plain':
            case 'Clearing':
                this.renderSnowTexture(ctx, px, py, worldX, worldY, terrain === 'Clearing');
                break;
            case 'Hills':
                this.renderHillsTexture(ctx, px, py, worldX, worldY);
                break;
            case 'Rock':
            case 'Mountain':
                this.renderRockTexture(ctx, px, py, worldX, worldY, terrain === 'Mountain');
                break;
            case 'Marsh':
                this.renderMarshTexture(ctx, px, py, worldX, worldY);
                break;
        }
    }

    /**
     * Forest - recognizable evergreen trees
     */
    renderForestTexture(ctx, px, py, worldX, worldY) {
        const size = this.TILE_SIZE;
        ctx.fillStyle = 'rgba(15, 35, 25, 0.7)';

        // Generate 5-7 trees at seeded random positions
        const treeCount = 5 + Math.floor(this.seededRandom(worldX, worldY, 100) * 3);
        for (let i = 0; i < treeCount; i++) {
            const tx = px + size * (0.1 + this.seededRandom(worldX, worldY, i * 10) * 0.8);
            const ty = py + size * (0.15 + this.seededRandom(worldX, worldY, i * 10 + 1) * 0.7);
            const treeHeight = 12 + this.seededRandom(worldX, worldY, i * 10 + 2) * 8;
            const treeWidth = treeHeight * 0.6;

            // Draw layered triangle tree (evergreen shape)
            ctx.beginPath();
            ctx.moveTo(tx, ty - treeHeight);
            ctx.lineTo(tx - treeWidth / 2, ty);
            ctx.lineTo(tx + treeWidth / 2, ty);
            ctx.closePath();
            ctx.fill();

            // Second layer (slightly smaller, overlapping)
            ctx.beginPath();
            ctx.moveTo(tx, ty - treeHeight * 0.85);
            ctx.lineTo(tx - treeWidth * 0.35, ty - treeHeight * 0.2);
            ctx.lineTo(tx + treeWidth * 0.35, ty - treeHeight * 0.2);
            ctx.closePath();
            ctx.fill();
        }

        // Snow on some trees (white highlights)
        ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
        for (let i = 0; i < 3; i++) {
            const sx = px + size * (0.2 + this.seededRandom(worldX, worldY, i + 50) * 0.6);
            const sy = py + size * (0.2 + this.seededRandom(worldX, worldY, i + 51) * 0.4);
            ctx.fillRect(sx - 3, sy, 6, 2);
        }
    }

    /**
     * Water/Ice - frozen surface with cracks
     */
    renderWaterTexture(ctx, px, py, worldX, worldY, isDeep) {
        const size = this.TILE_SIZE;

        // Ice cracks - multiple branching lines
        ctx.strokeStyle = isDeep ? 'rgba(40, 70, 100, 0.5)' : 'rgba(60, 100, 130, 0.4)';
        ctx.lineWidth = 1;

        // Main crack
        const startX = px + size * (0.1 + this.seededRandom(worldX, worldY, 1) * 0.3);
        const startY = py + size * (0.1 + this.seededRandom(worldX, worldY, 2) * 0.3);
        ctx.beginPath();
        ctx.moveTo(startX, startY);

        // Jagged crack path
        let cx = startX, cy = startY;
        for (let i = 0; i < 4; i++) {
            cx += size * (0.15 + this.seededRandom(worldX, worldY, i + 10) * 0.15);
            cy += size * (0.1 + this.seededRandom(worldX, worldY, i + 11) * 0.15);
            ctx.lineTo(cx, cy);
        }
        ctx.stroke();

        // Secondary crack branching off
        const branchPoint = 1 + Math.floor(this.seededRandom(worldX, worldY, 20) * 2);
        ctx.beginPath();
        ctx.moveTo(
            startX + size * 0.2 * branchPoint,
            startY + size * 0.15 * branchPoint
        );
        ctx.lineTo(
            px + size * (0.5 + this.seededRandom(worldX, worldY, 21) * 0.4),
            py + size * (0.6 + this.seededRandom(worldX, worldY, 22) * 0.3)
        );
        ctx.stroke();

        // Ice surface variation - lighter patches
        ctx.fillStyle = 'rgba(255, 255, 255, 0.08)';
        for (let i = 0; i < 2; i++) {
            const patchX = px + size * (0.2 + this.seededRandom(worldX, worldY, i + 30) * 0.6);
            const patchY = py + size * (0.3 + this.seededRandom(worldX, worldY, i + 31) * 0.5);
            ctx.beginPath();
            ctx.ellipse(patchX, patchY, 12, 8, this.seededRandom(worldX, worldY, i + 32) * Math.PI, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    /**
     * Plains/Clearing - snow drifts and wind patterns
     */
    renderSnowTexture(ctx, px, py, worldX, worldY, isClearing) {
        const size = this.TILE_SIZE;

        // Snow drift curves
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
        ctx.lineWidth = 1.5;

        const driftCount = isClearing ? 2 : 3;
        for (let i = 0; i < driftCount; i++) {
            const startY = py + size * (0.2 + i * 0.3 + this.seededRandom(worldX, worldY, i) * 0.1);
            ctx.beginPath();
            ctx.moveTo(px + 5, startY);

            // Gentle curve across tile
            ctx.bezierCurveTo(
                px + size * 0.3, startY + (this.seededRandom(worldX, worldY, i + 10) - 0.5) * 15,
                px + size * 0.7, startY + (this.seededRandom(worldX, worldY, i + 11) - 0.5) * 15,
                px + size - 5, startY + (this.seededRandom(worldX, worldY, i + 12) - 0.5) * 10
            );
            ctx.stroke();
        }

        // Scattered snow sparkles
        ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
        for (let i = 0; i < 6; i++) {
            const sx = px + size * (0.1 + this.seededRandom(worldX, worldY, i + 20) * 0.8);
            const sy = py + size * (0.1 + this.seededRandom(worldX, worldY, i + 21) * 0.8);
            ctx.fillRect(sx, sy, 2, 2);
        }

        // Clearing: add sparse vegetation hints
        if (isClearing) {
            ctx.fillStyle = 'rgba(80, 100, 90, 0.3)';
            for (let i = 0; i < 2; i++) {
                const vx = px + size * (0.3 + this.seededRandom(worldX, worldY, i + 40) * 0.4);
                const vy = py + size * (0.5 + this.seededRandom(worldX, worldY, i + 41) * 0.4);
                ctx.fillRect(vx, vy, 3, 6);
            }
        }
    }

    /**
     * Hills - contour lines suggesting elevation
     */
    renderHillsTexture(ctx, px, py, worldX, worldY) {
        const size = this.TILE_SIZE;

        // Contour lines
        ctx.strokeStyle = 'rgba(60, 80, 100, 0.25)';
        ctx.lineWidth = 1;

        for (let i = 0; i < 3; i++) {
            const baseY = py + size * (0.25 + i * 0.25);
            ctx.beginPath();
            ctx.moveTo(px + 8, baseY);

            // Curved contour
            ctx.bezierCurveTo(
                px + size * 0.35, baseY - 8 - this.seededRandom(worldX, worldY, i) * 6,
                px + size * 0.65, baseY - 5 - this.seededRandom(worldX, worldY, i + 1) * 8,
                px + size - 8, baseY + this.seededRandom(worldX, worldY, i + 2) * 4
            );
            ctx.stroke();
        }

        // Exposed rock patches
        ctx.fillStyle = 'rgba(70, 80, 90, 0.4)';
        for (let i = 0; i < 2; i++) {
            const rx = px + size * (0.2 + this.seededRandom(worldX, worldY, i + 20) * 0.6);
            const ry = py + size * (0.3 + this.seededRandom(worldX, worldY, i + 21) * 0.5);
            ctx.beginPath();
            ctx.ellipse(rx, ry, 8, 5, this.seededRandom(worldX, worldY, i + 22) * Math.PI, 0, Math.PI * 2);
            ctx.fill();
        }

        // Snow on high points
        ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
        ctx.fillRect(px + size * 0.3, py + 8, size * 0.4, 3);
    }

    /**
     * Rock/Mountain - angular stone shapes
     */
    renderRockTexture(ctx, px, py, worldX, worldY, isMountain) {
        const size = this.TILE_SIZE;

        // Angular rock shapes
        ctx.fillStyle = isMountain ? 'rgba(30, 35, 40, 0.5)' : 'rgba(80, 85, 90, 0.4)';

        const rockCount = isMountain ? 5 : 4;
        for (let i = 0; i < rockCount; i++) {
            const rx = px + size * (0.1 + this.seededRandom(worldX, worldY, i * 5) * 0.8);
            const ry = py + size * (0.1 + this.seededRandom(worldX, worldY, i * 5 + 1) * 0.8);
            const rockSize = 8 + this.seededRandom(worldX, worldY, i * 5 + 2) * 12;

            // Draw angular polygon
            ctx.beginPath();
            const points = 4 + Math.floor(this.seededRandom(worldX, worldY, i * 5 + 3) * 2);
            for (let j = 0; j < points; j++) {
                const angle = (j / points) * Math.PI * 2;
                const dist = rockSize * (0.6 + this.seededRandom(worldX, worldY, i * 5 + j + 10) * 0.4);
                const ptX = rx + Math.cos(angle) * dist;
                const ptY = ry + Math.sin(angle) * dist * 0.7;
                if (j === 0) ctx.moveTo(ptX, ptY);
                else ctx.lineTo(ptX, ptY);
            }
            ctx.closePath();
            ctx.fill();
        }

        // Crack lines between stones
        ctx.strokeStyle = 'rgba(20, 25, 30, 0.4)';
        ctx.lineWidth = 1;
        for (let i = 0; i < 3; i++) {
            ctx.beginPath();
            ctx.moveTo(
                px + size * this.seededRandom(worldX, worldY, i + 50),
                py + size * this.seededRandom(worldX, worldY, i + 51)
            );
            ctx.lineTo(
                px + size * this.seededRandom(worldX, worldY, i + 52),
                py + size * this.seededRandom(worldX, worldY, i + 53)
            );
            ctx.stroke();
        }

        // Mountain: snow on peaks
        if (isMountain) {
            ctx.fillStyle = 'rgba(255, 255, 255, 0.25)';
            ctx.beginPath();
            ctx.moveTo(px + size * 0.3, py + 5);
            ctx.lineTo(px + size * 0.5, py + 15);
            ctx.lineTo(px + size * 0.7, py + 5);
            ctx.closePath();
            ctx.fill();
        }
    }

    /**
     * Marsh - reeds and murky frozen wetland
     */
    renderMarshTexture(ctx, px, py, worldX, worldY) {
        const size = this.TILE_SIZE;

        // Murky ice patches
        ctx.fillStyle = 'rgba(40, 55, 50, 0.3)';
        for (let i = 0; i < 2; i++) {
            const patchX = px + size * (0.2 + this.seededRandom(worldX, worldY, i) * 0.6);
            const patchY = py + size * (0.3 + this.seededRandom(worldX, worldY, i + 1) * 0.4);
            ctx.beginPath();
            ctx.ellipse(patchX, patchY, 15, 10, 0, 0, Math.PI * 2);
            ctx.fill();
        }

        // Dead reeds/cattails poking through
        ctx.strokeStyle = 'rgba(80, 90, 70, 0.6)';
        ctx.lineWidth = 1.5;

        const reedCount = 6 + Math.floor(this.seededRandom(worldX, worldY, 10) * 3);
        for (let i = 0; i < reedCount; i++) {
            const rx = px + size * (0.1 + this.seededRandom(worldX, worldY, i + 20) * 0.8);
            const ry = py + size * (0.4 + this.seededRandom(worldX, worldY, i + 21) * 0.5);
            const height = 15 + this.seededRandom(worldX, worldY, i + 22) * 15;
            const lean = (this.seededRandom(worldX, worldY, i + 23) - 0.5) * 6;

            ctx.beginPath();
            ctx.moveTo(rx, ry);
            ctx.lineTo(rx + lean, ry - height);
            ctx.stroke();

            // Cattail head on some reeds
            if (this.seededRandom(worldX, worldY, i + 24) > 0.6) {
                ctx.fillStyle = 'rgba(60, 50, 40, 0.7)';
                ctx.beginPath();
                ctx.ellipse(rx + lean, ry - height - 3, 2, 5, 0, 0, Math.PI * 2);
                ctx.fill();
                ctx.strokeStyle = 'rgba(80, 90, 70, 0.6)';
            }
        }

        // Ice crack
        ctx.strokeStyle = 'rgba(50, 70, 65, 0.3)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(px + 10, py + size * 0.7);
        ctx.lineTo(px + size * 0.6, py + size * 0.8);
        ctx.stroke();
    }

    /**
     * Render feature icons on a tile
     * Icons are now material symbol names sent directly from backend
     */
    renderFeatureIcons(icons, px, py, hasFire) {
        const iconPositions = [
            { ox: this.TILE_SIZE * 0.28, oy: this.TILE_SIZE * 0.38 },
            { ox: this.TILE_SIZE * 0.72, oy: this.TILE_SIZE * 0.38 },
            { ox: this.TILE_SIZE * 0.28, oy: this.TILE_SIZE * 0.73 },
            { ox: this.TILE_SIZE * 0.72, oy: this.TILE_SIZE * 0.73 }
        ];

        icons.slice(0, 4).forEach((iconName, i) => {
            if (!iconName) return;

            const pos = iconPositions[i];
            const iconX = px + pos.ox;
            const iconY = py + pos.oy;

            // Check for special icon styling
            const special = this.SPECIAL_ICONS[iconName];
            const baseColor = special?.color || '#cccccc';
            const glow = special?.glow || false;

            // Light sources (fire, embers) don't dim - they're the light!
            const isLightSource = iconName === 'local_fire_department' || iconName === 'fireplace';
            const color = isLightSource ? baseColor : this.adjustHexForTime(baseColor);

            // Draw dark background circle for readability (dimmer at night)
            const bgAlpha = 0.25 + (0.15 * this.timeFactor);  // 0.25-0.4 range
            this.ctx.fillStyle = `rgba(0, 0, 0, ${bgAlpha})`;
            this.ctx.beginPath();
            this.ctx.arc(iconX, iconY, 14, 0, Math.PI * 2);
            this.ctx.fill();

            if (glow) {
                this.ctx.save();
                this.ctx.shadowColor = baseColor;  // Glow uses original color
                this.ctx.shadowBlur = 6;
            }

            this.drawMaterialIcon(iconName, iconX, iconY, 24, color, 1);

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

        // Adjust footprint color for time of day
        const footprintColor = this.adjustHexForTime('#60a0b0');

        this.playerHistory.forEach((pos, i) => {
            const { vx, vy } = this.worldToView(pos.x, pos.y);
            if (vx >= 0 && vx < this.VIEW_SIZE && vy >= 0 && vy < this.VIEW_SIZE) {
                const { px, py } = this.getTileScreenPos(vx, vy);
                const alpha = 0.2 - (i * 0.06);
                if (alpha > 0) {
                    this.ctx.globalAlpha = alpha;
                    this.ctx.fillStyle = footprintColor;
                    this.ctx.fillRect(px + this.TILE_SIZE/2 - 6, py + this.TILE_SIZE/2 - 2, 4, 6);
                    this.ctx.fillRect(px + this.TILE_SIZE/2 + 2, py + this.TILE_SIZE/2 - 2, 4, 6);
                    this.ctx.globalAlpha = 1;
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
        // Vignette intensity varies with time - stronger at night for atmosphere
        const baseAlpha = 0.35;
        const nightBonus = 0.15 * (1 - this.timeFactor);  // Extra darkness at night
        const alpha = baseAlpha + nightBonus;

        const vignette = this.ctx.createRadialGradient(
            this.canvas.width/2, this.canvas.height/2, this.canvas.height * 0.3,
            this.canvas.width/2, this.canvas.height/2, this.canvas.height * 0.7
        );
        vignette.addColorStop(0, 'rgba(8, 10, 14, 0)');
        vignette.addColorStop(1, `rgba(8, 10, 14, ${alpha})`);
        this.ctx.fillStyle = vignette;
        this.ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
    }

    /**
     * Handle mouse move events
     */
    handleMouseMove(e) {
        if (!this.gridState) return;

        const rect = this.canvas.getBoundingClientRect();
        // Account for CSS scaling: convert screen coords to canvas coords
        const scaleX = this.canvas.width / rect.width;
        const scaleY = this.canvas.height / rect.height;
        const mx = (e.clientX - rect.left) * scaleX;
        const my = (e.clientY - rect.top) * scaleY;
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
    handleClick(e) {
        if (!this.gridState || !this.onTileClick) return;

        const rect = this.canvas.getBoundingClientRect();
        // Account for CSS scaling: convert screen coords to canvas coords
        const scaleX = this.canvas.width / rect.width;
        const scaleY = this.canvas.height / rect.height;
        const mx = (e.clientX - rect.left) * scaleX;
        const my = (e.clientY - rect.top) * scaleY;
        const vx = Math.floor(mx / (this.TILE_SIZE + this.GAP));
        const vy = Math.floor(my / (this.TILE_SIZE + this.GAP));

        if (vx >= 0 && vx < this.VIEW_SIZE && vy >= 0 && vy < this.VIEW_SIZE) {
            const { x: worldX, y: worldY } = this.viewToWorld(vx, vy);
            const tile = this.findTile(worldX, worldY);

            if (tile && tile.visibility !== 'unexplored') {
                // Calculate popup position (screen coordinates, accounting for CSS scaling)
                const cellSize = (this.TILE_SIZE + this.GAP) / scaleX;
                const tileScreenX = rect.left + (vx + 1) * cellSize;
                const tileScreenY = rect.top + vy * cellSize;

                this.onTileClick(worldX, worldY, tile, { x: tileScreenX, y: tileScreenY });
            }
        }
    }

    /**
     * Record a move (called when movement actually happens)
     */
    recordMove() {
        if (this.gridState) {
            this.addToHistory(this.gridState.playerX, this.gridState.playerY);
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
            this.canvas.removeEventListener('mousemove', this._boundHandleMouseMove);
            this.canvas.removeEventListener('mouseleave', this._boundHandleMouseLeave);
            this.canvas.removeEventListener('click', this._boundHandleClick);
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
