import { GridScene } from './GridScene.js';
import { show as showEl, hide as hideEl } from '../utils.js';

/**
 * GridDisplay - Manages the Phaser game instance for grid rendering
 * Provides interface between GameClient and GridScene
 */
export class GridDisplay {
    constructor() {
        this.game = null;
        this.scene = null;
        this.isActive = false;
        this.onMoveRequest = null; // Callback for move requests
        this.pendingHazardPrompt = null;
    }

    /**
     * Initialize Phaser and create the grid scene
     */
    init(containerId, onMoveRequest) {
        this.onMoveRequest = onMoveRequest;

        const container = document.getElementById(containerId);
        if (!container) {
            console.error('Grid container not found:', containerId);
            return;
        }

        // Get container dimensions
        const width = container.clientWidth || 600;
        const height = container.clientHeight || 400;

        // Create Phaser config
        const config = {
            type: Phaser.AUTO,
            width: width,
            height: height,
            parent: containerId,
            backgroundColor: '#141820',
            scene: GridScene,
            scale: {
                mode: Phaser.Scale.RESIZE,
                autoCenter: Phaser.Scale.CENTER_BOTH
            }
        };

        // Create Phaser game
        this.game = new Phaser.Game(config);

        // Get scene reference once it's ready
        this.game.events.once('ready', () => {
            this.scene = this.game.scene.getScene('GridScene');
            if (this.scene) {
                this.scene.onTileClick = (x, y, tileData) => this.handleTileClick(x, y, tileData);
            }
        });

        // Handle resize
        window.addEventListener('resize', () => this.handleResize(container));
    }

    /**
     * Handle window/container resize
     */
    handleResize(container) {
        if (!this.game) return;

        const width = container.clientWidth || 600;
        const height = container.clientHeight || 400;

        this.game.scale.resize(width, height);

        if (this.scene) {
            this.scene.resize(width, height);
        }
    }

    /**
     * Show the grid display (switch to grid mode)
     */
    show() {
        const gridContainer = document.getElementById('gridContainer');
        const narrativePanel = document.querySelector('.narrative-panel');

        showEl(gridContainer);
        if (narrativePanel) {
            narrativePanel.classList.add('grid-mode');
        }

        this.isActive = true;
    }

    /**
     * Hide the grid display (switch to text mode)
     */
    hide() {
        const gridContainer = document.getElementById('gridContainer');
        const narrativePanel = document.querySelector('.narrative-panel');

        hideEl(gridContainer);
        if (narrativePanel) {
            narrativePanel.classList.remove('grid-mode');
        }

        this.isActive = false;
    }

    /**
     * Update grid display with new state from server
     */
    update(gridState, statusText) {
        if (!this.scene) {
            // Scene not ready yet, wait a bit
            setTimeout(() => this.update(gridState, statusText), 100);
            return;
        }

        this.scene.updateGrid(gridState);

        // Update status text
        const statusEl = document.getElementById('gridStatus');
        if (statusEl && statusText) {
            statusEl.textContent = statusText;
        }
    }

    /**
     * Show hazard prompt overlay using safe DOM methods
     */
    showHazardPrompt(hazardPrompt, onChoice) {
        this.pendingHazardPrompt = { prompt: hazardPrompt, callback: onChoice };

        // Create hazard prompt UI
        const container = document.getElementById('gridCanvas');
        if (!container) return;

        // Remove any existing prompt
        this.hideHazardPrompt();

        // Build prompt using safe DOM methods
        const promptDiv = document.createElement('div');
        promptDiv.className = 'hazard-prompt';
        promptDiv.id = 'hazardPromptOverlay';

        // Title
        const title = document.createElement('h3');
        title.textContent = 'Hazardous Terrain';
        promptDiv.appendChild(title);

        // Description
        const desc = document.createElement('div');
        desc.className = 'hazard-description';
        desc.textContent = hazardPrompt.hazardDescription;
        promptDiv.appendChild(desc);

        // Options container
        const options = document.createElement('div');
        options.className = 'hazard-options';

        // Quick button
        const quickBtn = document.createElement('button');
        quickBtn.className = 'hazard-option quick';
        quickBtn.id = 'hazardQuick';

        const quickLabel = document.createTextNode('Quick');
        quickBtn.appendChild(quickLabel);

        const quickTime = document.createElement('span');
        quickTime.className = 'option-time';
        quickTime.textContent = `${hazardPrompt.quickTimeMinutes} min`;
        quickBtn.appendChild(quickTime);

        const quickRisk = document.createElement('span');
        quickRisk.className = 'option-risk';
        quickRisk.textContent = `${hazardPrompt.injuryRiskPercent.toFixed(0)}% injury risk`;
        quickBtn.appendChild(quickRisk);

        quickBtn.addEventListener('click', () => {
            this.hideHazardPrompt();
            onChoice(true); // quick = true
        });
        options.appendChild(quickBtn);

        // Careful button
        const carefulBtn = document.createElement('button');
        carefulBtn.className = 'hazard-option careful';
        carefulBtn.id = 'hazardCareful';

        const carefulLabel = document.createTextNode('Careful');
        carefulBtn.appendChild(carefulLabel);

        const carefulTime = document.createElement('span');
        carefulTime.className = 'option-time';
        carefulTime.textContent = `${hazardPrompt.carefulTimeMinutes} min`;
        carefulBtn.appendChild(carefulTime);

        carefulBtn.addEventListener('click', () => {
            this.hideHazardPrompt();
            onChoice(false); // quick = false
        });
        options.appendChild(carefulBtn);

        promptDiv.appendChild(options);
        container.appendChild(promptDiv);
    }

    /**
     * Hide hazard prompt overlay
     */
    hideHazardPrompt() {
        const existing = document.getElementById('hazardPromptOverlay');
        if (existing) {
            existing.remove();
        }
        this.pendingHazardPrompt = null;
    }

    /**
     * Handle tile click from scene
     */
    handleTileClick(x, y, tileData) {
        if (this.onMoveRequest) {
            this.onMoveRequest(x, y, tileData);
        }
    }

    /**
     * Destroy the Phaser game instance
     */
    destroy() {
        if (this.game) {
            this.game.destroy(true);
            this.game = null;
            this.scene = null;
        }
    }
}

// Singleton instance
let gridDisplayInstance = null;

export function getGridDisplay() {
    if (!gridDisplayInstance) {
        gridDisplayInstance = new GridDisplay();
    }
    return gridDisplayInstance;
}
