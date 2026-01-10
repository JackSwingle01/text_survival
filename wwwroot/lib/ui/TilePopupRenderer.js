// lib/ui/TilePopupRenderer.js
import { show, hide, clear } from '../helpers.js';
import { ICON_CLASS } from '../components/Icon.js';
import { getFeatureTypeIcon } from '../../modules/icons.js';

/**
 * Handles tile popup rendering and positioning.
 * Extracts popup logic from GameClient for better separation of concerns.
 */
export class TilePopupRenderer {
    /**
     * @param {Object} options
     * @param {function} options.getVisualTileSize - Returns the tile size in pixels
     * @param {function} options.onTravelTo - Called when user clicks Go button: (x, y) => void
     * @param {function} options.canTravel - Returns whether travel is currently allowed
     */
    constructor({ getVisualTileSize, onTravelTo, canTravel }) {
        this.getVisualTileSize = getVisualTileSize;
        this.onTravelTo = onTravelTo;
        this.canTravel = canTravel;

        // Cache DOM elements
        this.popup = document.getElementById('tilePopup');
        this.nameEl = document.getElementById('popupName');
        this.terrainEl = document.getElementById('popupTerrain');
        this.glanceEl = document.getElementById('popupGlance');
        this.featuresEl = document.getElementById('popupFeatures');
        this.actionsEl = document.getElementById('popupActions');

        // Current state
        this.currentTile = null;
    }

    /**
     * Show tile popup at screen position
     */
    show(x, y, tileData, screenPos) {
        this.currentTile = { x, y, tileData };

        // Set location info
        this.nameEl.textContent = tileData.locationName || tileData.terrain;
        this.terrainEl.textContent = tileData.locationName ? tileData.terrain : '';

        // Clear sections
        clear(this.glanceEl);
        clear(this.featuresEl);

        // Build quick glance badges (for visible locations)
        const isExplored = tileData.visibility === 'visible';
        if (isExplored) {
            this._buildGlanceBar(tileData);
        }

        // Build features list - use detailed features if available
        if (isExplored && tileData.featureDetails && tileData.featureDetails.length > 0) {
            this._buildDetailedFeatures(tileData.featureDetails);
        }

        const isPlayerHere = tileData.isPlayerHere;

        // Build actions
        clear(this.actionsEl);
        const canTravelHere = tileData.isAdjacent && tileData.isPassable && !isPlayerHere && this.canTravel();

        if (canTravelHere) {
            const goBtn = document.createElement('button');
            goBtn.className = 'btn btn--primary btn--full';

            // Show travel time if available
            if (tileData.travelTimeMinutes) {
                goBtn.textContent = `Go (${tileData.travelTimeMinutes} min)`;
            } else {
                goBtn.textContent = 'Go';
            }

            goBtn.onclick = (e) => {
                e.stopPropagation();
                this.hide();
                this.onTravelTo(x, y);
            };
            this.actionsEl.appendChild(goBtn);
        }

        // Position popup
        this._positionPopup(screenPos);
    }

    /**
     * Hide tile popup
     */
    hide() {
        hide(this.popup);
        this.currentTile = null;
    }

    /**
     * Update just the action buttons in an already-visible tile popup
     */
    updateActions() {
        if (!this.currentTile) return;
        clear(this.actionsEl);
    }

    /**
     * Get current tile data (for external state access)
     */
    getCurrentTile() {
        return this.currentTile;
    }

    // ==================== PRIVATE METHODS ====================

    /**
     * Position popup horizontally at tile edge, vertically centered
     */
    _positionPopup(screenPos) {
        // Position popup horizontally (screenPos.x is already at tile's right edge)
        this.popup.style.left = `${screenPos.x}px`;

        // Show popup to measure its height
        show(this.popup);
        const rect = this.popup.getBoundingClientRect();

        // Calculate vertically centered position
        const tileSize = this.getVisualTileSize();
        const tileCenterY = screenPos.y + (tileSize / 2);
        let topPos = tileCenterY - (rect.height / 2);

        // Clamp to screen bounds
        if (topPos < 10) {
            topPos = 10;
        }
        if (topPos + rect.height > window.innerHeight - 10) {
            topPos = window.innerHeight - rect.height - 10;
        }

        this.popup.style.top = `${topPos}px`;

        // Adjust horizontal if popup would go off-screen (flip to left of tile)
        if (rect.right > window.innerWidth - 10) {
            this.popup.style.left = `${screenPos.x - rect.width - tileSize}px`;
        }
    }

    /**
     * Build quick glance bar with color-coded badges
     */
    _buildGlanceBar(tileData) {
        if (!tileData.glanceBadges || tileData.glanceBadges.length === 0) {
            return;
        }

        tileData.glanceBadges.forEach(badge => {
            const badgeEl = document.createElement('span');
            badgeEl.className = `badge badge--${badge.type}`;

            const iconEl = document.createElement('span');
            iconEl.className = ICON_CLASS;
            iconEl.textContent = badge.icon;
            badgeEl.appendChild(iconEl);

            const labelEl = document.createElement('span');
            labelEl.textContent = badge.label;
            badgeEl.appendChild(labelEl);

            this.glanceEl.appendChild(badgeEl);
        });
    }

    /**
     * Build detailed feature cards
     */
    _buildDetailedFeatures(featureDetails) {
        featureDetails.forEach(feature => {
            // Special compact layout for NPCs with health bars
            if (feature.type === 'npc' && feature.healthPct != null) {
                this._buildNpcFeature(feature);
                return;
            }

            // Standard feature rendering
            this._buildStandardFeature(feature);
        });
    }

    /**
     * Build NPC feature card with survival stat bars
     */
    _buildNpcFeature(feature) {
        const featureEl = document.createElement('div');
        featureEl.className = 'popup-feature-npc';

        // Header row: emoji + name + status (all inline)
        const headerEl = document.createElement('div');
        headerEl.className = 'npc-header';

        const iconEl = document.createElement('span');
        iconEl.className = 'feature-emoji';
        iconEl.textContent = feature.details?.[0] || '🧑';
        headerEl.appendChild(iconEl);

        const nameEl = document.createElement('span');
        nameEl.className = 'npc-name';
        nameEl.textContent = feature.label;
        headerEl.appendChild(nameEl);

        if (feature.status) {
            const statusEl = document.createElement('span');
            statusEl.className = 'npc-status';
            statusEl.textContent = feature.status;
            headerEl.appendChild(statusEl);
        }
        featureEl.appendChild(headerEl);

        // Build all survival stat bars
        this._buildNpcStatBar(featureEl, 'health', 'monitor_heart', feature.healthPct);
        this._buildNpcTempBar(featureEl, feature.bodyTempF, feature.bodyTempBarPct);
        this._buildNpcStatBar(featureEl, 'food', 'restaurant', feature.foodPct);
        this._buildNpcStatBar(featureEl, 'water', 'water_drop', feature.waterPct);
        this._buildNpcStatBar(featureEl, 'energy', 'bolt', feature.energyPct);

        this.featuresEl.appendChild(featureEl);
    }

    /**
     * Build a single NPC stat bar (health, food, water, energy)
     */
    _buildNpcStatBar(container, statName, iconName, pctValue) {
        if (pctValue == null) return;

        const pct = Math.round(pctValue * 100);

        const row = document.createElement('div');
        row.className = 'survival-stat';
        row.dataset.stat = statName;

        const icon = document.createElement('span');
        icon.className = 'stat-icon material-symbols-outlined';
        icon.textContent = iconName;
        row.appendChild(icon);

        const barContainer = document.createElement('div');
        barContainer.className = `bar bar--${statName}`;
        const barFill = document.createElement('div');
        barFill.className = 'bar__fill';
        if (statName === 'health') {
            if (pct < 30) barFill.classList.add('danger');
            else if (pct < 60) barFill.classList.add('warning');
        }
        barFill.style.width = `${pct}%`;
        barContainer.appendChild(barFill);
        row.appendChild(barContainer);

        const tooltip = document.createElement('div');
        tooltip.className = 'stat-tooltip';
        const nameSpan = document.createElement('span');
        nameSpan.className = 'stat-name';
        nameSpan.textContent = statName.charAt(0).toUpperCase() + statName.slice(1);
        const valueSpan = document.createElement('span');
        valueSpan.className = 'stat-value';
        valueSpan.textContent = `${pct}%`;
        tooltip.appendChild(nameSpan);
        tooltip.appendChild(valueSpan);
        row.appendChild(tooltip);

        container.appendChild(row);
    }

    /**
     * Build NPC temperature bar (shows °F instead of %)
     */
    _buildNpcTempBar(container, tempF, barPct) {
        if (tempF == null || barPct == null) return;

        const row = document.createElement('div');
        row.className = 'survival-stat';
        row.dataset.stat = 'temp';

        const icon = document.createElement('span');
        icon.className = 'stat-icon material-symbols-outlined';
        icon.textContent = 'thermostat';
        row.appendChild(icon);

        const barContainer = document.createElement('div');
        barContainer.className = 'bar bar--temp';
        const barFill = document.createElement('div');
        barFill.className = 'bar__fill';
        // Danger at <30% WarmPct (approaching hypothermia threshold at 95°F)
        // Warning at <60% WarmPct (getting cold)
        if (barPct < 30) barFill.classList.add('danger');
        else if (barPct < 60) barFill.classList.add('warning');
        barFill.style.width = `${barPct}%`;
        barContainer.appendChild(barFill);
        row.appendChild(barContainer);

        const tooltip = document.createElement('div');
        tooltip.className = 'stat-tooltip';
        const nameSpan = document.createElement('span');
        nameSpan.className = 'stat-name';
        nameSpan.textContent = 'Body';
        const valueSpan = document.createElement('span');
        valueSpan.className = 'stat-value';
        valueSpan.textContent = `${tempF.toFixed(1)}°F`;
        tooltip.appendChild(nameSpan);
        tooltip.appendChild(valueSpan);
        row.appendChild(tooltip);

        container.appendChild(row);
    }

    /**
     * Build standard feature card (herd, fire, cache, etc.)
     */
    _buildStandardFeature(feature) {
        const featureEl = document.createElement('div');
        featureEl.className = 'popup-feature-detailed';

        const iconEl = document.createElement('span');
        // Herds use emoji from details[0], others use Material Icons
        if (feature.type === 'herd' && feature.details && feature.details.length > 0) {
            iconEl.className = 'feature-emoji';
            iconEl.textContent = feature.details[0];
        } else {
            iconEl.className = ICON_CLASS;
            iconEl.textContent = getFeatureTypeIcon(feature.type);
        }
        featureEl.appendChild(iconEl);

        const contentEl = document.createElement('div');
        contentEl.className = 'feature-content';

        const labelEl = document.createElement('span');
        labelEl.className = 'feature-label';
        labelEl.textContent = feature.label;
        contentEl.appendChild(labelEl);

        if (feature.status) {
            const statusEl = document.createElement('span');
            statusEl.className = 'feature-status';
            statusEl.textContent = feature.status;
            contentEl.appendChild(statusEl);
        }

        // Don't show details for herds (emoji is in details[0])
        if (feature.details && feature.details.length > 0 && feature.type !== 'herd') {
            const detailsEl = document.createElement('span');
            detailsEl.className = 'feature-details';
            detailsEl.textContent = feature.details.slice(0, 3).join(', ');
            contentEl.appendChild(detailsEl);
        }

        featureEl.appendChild(contentEl);
        this.featuresEl.appendChild(featureEl);
    }
}
