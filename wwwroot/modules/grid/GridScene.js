/**
 * GridScene - Phaser scene for tile-based map rendering
 * Renders terrain tiles, player position, and handles click-to-move
 */
export class GridScene extends Phaser.Scene {
    constructor() {
        super({ key: 'GridScene' });
        this.tiles = new Map(); // Map of "x,y" -> tile graphics
        this.tileSize = 64;     // Pixels per tile
        this.gridState = null;
        this.onTileClick = null; // Callback for tile clicks
        this.playerMarker = null;
        this.adjacentHighlights = [];
        this.featureIcons = new Map(); // Map of "x,y" -> icon container
    }

    init(data) {
        this.onTileClick = data.onTileClick || null;
    }

    preload() {
        // No assets to preload yet - using colored rectangles
    }

    create() {
        // Set up camera
        this.cameras.main.setBackgroundColor(0x141820);

        // Create container for all tile graphics
        this.tileContainer = this.add.container(0, 0);

        // Create player marker (will be positioned in updateGrid)
        this.playerMarker = this.add.graphics();

        // Set up input handling
        this.input.on('pointerdown', this.handleClick, this);

        // Enable camera drag for panning (optional)
        // this.input.on('pointermove', this.handleDrag, this);
    }

    /**
     * Update the grid display with new state from server
     */
    updateGrid(gridState) {
        this.gridState = gridState;

        if (!gridState) return;

        // Clear existing tiles
        this.tiles.forEach((tile) => tile.destroy());
        this.tiles.clear();
        this.adjacentHighlights.forEach(h => h.destroy());
        this.adjacentHighlights = [];
        this.featureIcons.forEach(icons => icons.destroy());
        this.featureIcons.clear();

        // Draw all visible tiles
        gridState.tiles.forEach(tileData => {
            this.drawTile(tileData);
        });

        // Position player marker
        this.updatePlayerMarker(gridState.playerX, gridState.playerY);

        // Center camera on player
        this.centerCameraOnPlayer(gridState.playerX, gridState.playerY);

        // Highlight adjacent tiles
        this.highlightAdjacentTiles(gridState);
    }

    /**
     * Draw a single tile
     */
    drawTile(tileData) {
        const x = tileData.x * this.tileSize;
        const y = tileData.y * this.tileSize;
        const key = `${tileData.x},${tileData.y}`;

        // Get terrain color (snowy theme)
        const color = this.getTerrainColor(tileData.terrain);

        // Create tile graphics
        const graphics = this.add.graphics();

        // Apply fog of war (darken unexplored/explored tiles)
        let alpha = 1.0;
        if (tileData.visibility === 'unexplored') {
            alpha = 0.3;
        } else if (tileData.visibility === 'explored') {
            alpha = 0.6;
        }

        // Draw base tile
        graphics.fillStyle(color, alpha);
        graphics.fillRect(x + 1, y + 1, this.tileSize - 2, this.tileSize - 2);

        // Draw border
        graphics.lineStyle(1, 0x2a3040, alpha);
        graphics.strokeRect(x + 1, y + 1, this.tileSize - 2, this.tileSize - 2);

        // Hazard indicator (orange tint on border)
        if (tileData.isHazardous && alpha > 0.5) {
            graphics.lineStyle(2, 0xb87333, 0.6);
            graphics.strokeRect(x + 2, y + 2, this.tileSize - 4, this.tileSize - 4);
        }

        // Fire glow effect
        if (tileData.hasFire) {
            graphics.lineStyle(3, 0xff6622, 0.7);
            graphics.strokeRect(x + 3, y + 3, this.tileSize - 6, this.tileSize - 6);

            // Add subtle glow
            graphics.fillStyle(0xff6622, 0.15);
            graphics.fillCircle(x + this.tileSize/2, y + this.tileSize/2, this.tileSize * 0.6);
        }

        this.tiles.set(key, graphics);

        // Draw location name if present and visible
        if (tileData.locationName && tileData.visibility === 'visible') {
            const nameText = this.add.text(
                x + this.tileSize / 2,
                y + 6,
                tileData.locationName,
                {
                    fontSize: '9px',
                    fontFamily: 'Oswald, sans-serif',
                    color: '#ffffff',
                    stroke: '#000000',
                    strokeThickness: 2,
                    align: 'center'
                }
            );
            nameText.setOrigin(0.5, 0);
            // Store with tile for cleanup
            graphics.nameText = nameText;
        }

        // Draw feature icons
        if (tileData.featureIcons && tileData.featureIcons.length > 0 && tileData.visibility === 'visible') {
            this.drawFeatureIcons(tileData, x, y);
        }
    }

    /**
     * Draw feature icons on a tile
     */
    drawFeatureIcons(tileData, x, y) {
        const icons = tileData.featureIcons;
        const iconSize = 12;
        const padding = 4;
        const startY = y + this.tileSize - iconSize - padding;
        const key = `${tileData.x},${tileData.y}`;

        const container = this.add.container(x, startY);

        // Position icons in a row at bottom of tile
        icons.slice(0, 4).forEach((iconType, i) => {
            const iconX = padding + i * (iconSize + 2);
            const iconColor = this.getFeatureIconColor(iconType);

            const iconGraphics = this.add.graphics();
            iconGraphics.fillStyle(iconColor, 0.9);
            iconGraphics.fillCircle(iconX + iconSize/2, iconSize/2, iconSize/2 - 1);
            iconGraphics.lineStyle(1, 0xffffff, 0.5);
            iconGraphics.strokeCircle(iconX + iconSize/2, iconSize/2, iconSize/2 - 1);

            container.add(iconGraphics);
        });

        this.featureIcons.set(key, container);
    }

    /**
     * Get color for feature icon type
     */
    getFeatureIconColor(iconType) {
        const colors = {
            'fire': 0xff6622,
            'forage': 0x4a8a4a,
            'harvest': 0x8a6a4a,
            'animals': 0xa08060,
            'water': 0x4a6a9a,
            'cache': 0x9a8a5a,
            'shelter': 0x6a7a8a,
            'wood': 0x6a5a4a,
            'trap': 0x7a6a5a,
            'curing': 0x8a7a6a,
            'project': 0x5a6a7a,
            'salvage': 0x7a7a7a
        };
        return colors[iconType] || 0x808080;
    }

    /**
     * Get terrain color (snowy winter theme)
     */
    getTerrainColor(terrainType) {
        const colors = {
            'Forest': 0x2d4a3a,       // Dark snowy evergreen
            'Clearing': 0x8a9aa8,     // Light snow
            'Plain': 0x7a8a98,        // Snowy plain
            'Hills': 0x5a6a78,        // Snow-dusted hills
            'Water': 0x4a6080,        // Frozen water/ice
            'Marsh': 0x4a5a60,        // Frozen marsh
            'Rock': 0x6a7080,         // Snow-covered rock
            'Mountain': 0x3a4050,     // Dark mountain (impassable)
            'DeepWater': 0x2a3a50     // Deep water (impassable)
        };
        return colors[terrainType] || 0x5a6a7a;
    }

    /**
     * Update player marker position
     */
    updatePlayerMarker(gridX, gridY) {
        const x = gridX * this.tileSize + this.tileSize / 2;
        const y = gridY * this.tileSize + this.tileSize / 2;

        this.playerMarker.clear();

        // Draw player as a bright circle with border
        this.playerMarker.fillStyle(0xffffff, 1);
        this.playerMarker.fillCircle(x, y, 8);
        this.playerMarker.lineStyle(2, 0x2080c0, 1);
        this.playerMarker.strokeCircle(x, y, 8);

        // Add a subtle pulsing ring (static for now)
        this.playerMarker.lineStyle(1, 0x40a0e0, 0.5);
        this.playerMarker.strokeCircle(x, y, 12);
    }

    /**
     * Highlight tiles adjacent to player (valid move targets)
     */
    highlightAdjacentTiles(gridState) {
        // Clear existing highlights
        this.adjacentHighlights.forEach(h => h.destroy());
        this.adjacentHighlights = [];

        // Find adjacent tiles
        gridState.tiles.forEach(tileData => {
            if (tileData.isAdjacent && tileData.isPassable) {
                const x = tileData.x * this.tileSize;
                const y = tileData.y * this.tileSize;

                const highlight = this.add.graphics();
                highlight.lineStyle(3, 0x40a0e0, 0.8);
                highlight.strokeRect(x + 3, y + 3, this.tileSize - 6, this.tileSize - 6);

                this.adjacentHighlights.push(highlight);
            }
        });
    }

    /**
     * Center camera on player position
     */
    centerCameraOnPlayer(gridX, gridY) {
        const x = gridX * this.tileSize + this.tileSize / 2;
        const y = gridY * this.tileSize + this.tileSize / 2;

        this.cameras.main.centerOn(x, y);
    }

    /**
     * Handle click/tap on the grid
     */
    handleClick(pointer) {
        if (!this.gridState || !this.onTileClick) return;

        // Convert screen coordinates to world coordinates
        const worldPoint = this.cameras.main.getWorldPoint(pointer.x, pointer.y);

        // Convert to grid coordinates
        const gridX = Math.floor(worldPoint.x / this.tileSize);
        const gridY = Math.floor(worldPoint.y / this.tileSize);

        // Find the tile data
        const tileData = this.gridState.tiles.find(
            t => t.x === gridX && t.y === gridY
        );

        // Only allow clicking on adjacent passable tiles
        if (tileData && tileData.isAdjacent && tileData.isPassable) {
            this.onTileClick(gridX, gridY, tileData);
        }
    }

    /**
     * Resize handler - call when container size changes
     */
    resize(width, height) {
        this.scale.resize(width, height);

        if (this.gridState) {
            this.centerCameraOnPlayer(this.gridState.playerX, this.gridState.playerY);
        }
    }
}
