// modules/GameClient.js (simplified excerpt)
import { InputHandler } from './core/InputHandler.js';
import { EventOverlay } from './overlays/EventOverlay.js';
import { HuntOverlay } from './overlays/HuntOverlay.js';
import { CombatOverlay } from './overlays/CombatOverlay.js';
import { InventoryOverlay } from './overlays/InventoryOverlay.js';
import { ConfirmOverlay } from './overlays/ConfirmOverlay.js';
import { HazardOverlay } from './overlays/HazardOverlay.js';
import { DeathOverlay } from './overlays/DeathOverlay.js';
import { ForageOverlay } from './overlays/ForageOverlay.js';
import { ButcherOverlay } from './overlays/ButcherOverlay.js';
import { TransferOverlay } from './overlays/TransferOverlay.js';
import { CookingOverlay } from './overlays/CookingOverlay.js';
import { CraftingOverlay } from './overlays/CraftingOverlay.js';
import { FireOverlay } from './overlays/FireOverlay.js';
import { EatingOverlay } from './overlays/EatingOverlay.js';
import { DiscoveryOverlay } from './overlays/DiscoveryOverlay.js';
import { WeatherChangeOverlay } from './overlays/WeatherChangeOverlay.js';
import { ConnectionOverlay } from './modules/connection.js';
import { Utils, ICON_CLASS, createIcon, show, hide } from './modules/utils.js';
import { ProgressDisplay } from './modules/progress.js';
import { FrameQueue } from './modules/frameQueue.js';
import { NarrativeLog } from './modules/log.js';
import { TemperatureDisplay } from './modules/temperature.js';
import { FireDisplay } from './modules/fire.js';
import { SurvivalDisplay } from './modules/survival.js';
import { EffectsDisplay } from './modules/effects.js';
import { getGridRenderer } from './modules/grid/CanvasGridRenderer.js';
import { getWeatherIcon, getFeatureIconLabel, getFeatureTypeIcon } from './modules/icons.js';

// Actions handled elsewhere (sidebar buttons, grid clicks) - hidden from popup
// Work/exploration actions now go to sidebar instead
const POPUP_HIDDEN_ACTIONS = ['Inventory', 'Crafting', 'Travel', 'Storage',
    'Forage', 'Hunt', 'Harvest', 'Explore', 'Trap', 'Chop', 'Work',
    'Butcher', 'Check', 'Set', 'Build', 'Salvage', 'Cache'];

class GameClient {
    constructor() {
        this.socket = null;
        this.currentInputId = 0;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.awaitingResponse = false;
        this.gridRenderer = null;
        this.gridInitialized = false;
        this.tilePopup = null;
        this.resumeBlockUntil = 0;
        this.currentInput = null;

        // Core systems
        this.inputHandler = new InputHandler(
            () => this.socket,
            () => this.currentInputId
        );

        // Create unified combat overlay (handles both encounter and combat phases)
        const combatOverlay = new CombatOverlay(this.inputHandler);

        // Overlay registry
        this.overlays = {
            event: new EventOverlay(this.inputHandler),
            hunt: new HuntOverlay(this.inputHandler),
            combat: combatOverlay,
            inventory: new InventoryOverlay(this.inputHandler),
            crafting: new CraftingOverlay(this.inputHandler),
            fire: new FireOverlay(this.inputHandler),
            eating: new EatingOverlay(this.inputHandler),
            discovery: new DiscoveryOverlay(this.inputHandler),
            weatherChange: new WeatherChangeOverlay(this.inputHandler),
            transfer: new TransferOverlay(this.inputHandler),
            forage: new ForageOverlay(this.inputHandler),
            butcher: new ButcherOverlay(this.inputHandler),
            encounter: combatOverlay,  // Route encounter to combat overlay
            hazard: new HazardOverlay(this.inputHandler),
            confirm: new ConfirmOverlay(this.inputHandler),
            deathScreen: new DeathOverlay(this.inputHandler),
            cooking: new CookingOverlay(this.inputHandler),
        };

        // Initialize frame queue with render callback
        FrameQueue.init((frame) => this.renderFrame(frame));

        this.connect();
        this.initQuickActions();
        this.initTilePopup();
        this.initVisibilityHandler();
    }

    /**
     * Handle incoming frame
     */
    renderFrame(frame) {
        this.inputHandler.reset();
        this.inputHandler.enableAllButtons();
        this.awaitingResponse = false;

        // Update input ID first
        if (frame.input?.inputId) {
            this.currentInputId = frame.input.inputId;
        }

        // Check if this is a progress mode (animation will handle stat interpolation)
        const isProgressMode = frame.mode?.type === 'progress' ||
                              frame.mode?.type === 'travel_progress';

        // Render state
        if (frame.state) {
            this.renderState(frame.state, isProgressMode);
        }

        // Set mode
        this.setMode(frame.mode);

        // Clear and show overlays
        this.hideAllOverlays();
        for (const overlay of frame.overlays || []) {
            this.showOverlay(overlay, frame.input);
        }

        // Store current input BEFORE updating popup (popup uses this.currentInput)
        this.currentInput = frame.input;

        // Update tile popup actions if it's currently shown (but not during progress/state-only frames)
        const tilePopupElement = document.getElementById('tilePopup');
        if (this.tilePopup && tilePopupElement && !tilePopupElement.classList.contains('hidden')) {
            if (!isProgressMode && frame.input?.choices) {
                this.updateTilePopupActions();
            }
        }

        // Update quick actions
        this.updateQuickActionStates(frame.input?.choices);

        // Update available actions in sidebar
        this.updateAvailableActions(frame.input?.choices);
    }

    /**
     * Show overlay by type - delegates to registered overlay handlers
     */
    showOverlay(overlay, input) {
        const handler = this.overlays[overlay.type];
        if (!handler) {
            console.error(`[showOverlay] Unknown overlay type: ${overlay.type}`);
            return;
        }
        // Special case: confirm overlay uses prompt instead of data
        const data = overlay.type === 'confirm' ? overlay.prompt : overlay.data;
        handler.safeRender(data, this.currentInputId, input);
    }

    /**
     * Hide all overlays
     */
    hideAllOverlays() {
        Object.values(this.overlays).forEach(handler => handler.hide());
    }

    /**
     * Establish WebSocket connection
     */
    connect() {
        const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
        this.socket = new WebSocket(`${protocol}//${location.host}/ws`);

        this.socket.onopen = () => {
            this.reconnectAttempts = 0;
            // Block clicks until first frame arrives to prevent stale button race condition
            this.awaitingResponse = true;
            // Reset inputId - the next frame from server will set the correct value
            this.currentInputId = 0;

            // Clear all stale UI to prevent clicking old buttons with captured state
            this.hideAllOverlays();
            this.updateAvailableActions(null);
            this.hideTilePopup();

            ConnectionOverlay.hide();
        };

        this.socket.onmessage = (event) => {
            const frame = JSON.parse(event.data);
            this.handleFrame(frame);
        };

        this.socket.onclose = () => {
            ConnectionOverlay.show('Connection lost. Reconnecting...');
            this.attemptReconnect();
        };

        this.socket.onerror = () => {
            ConnectionOverlay.show('Connection error', true);
        };
    }

    attemptReconnect() {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            setTimeout(() => this.connect(), 2000);
        } else {
            ConnectionOverlay.show('Failed to connect. Refresh to try again.', true);
        }
    }

    /**
     * Handle incoming frame - delegate to FrameQueue
     */
    handleFrame(frame) {
        FrameQueue.enqueue(frame);
    }

    /**
     * Render game state to UI
     * @param {object} state - Game state to render
     * @param {boolean} skipSurvivalStats - Skip survival stat rendering (for progress animation)
     */
    renderState(state, skipSurvivalStats = false) {

        // CSS variables
        document.documentElement.style.setProperty('--warmth', state.warmth);
        document.documentElement.style.setProperty('--vitality', state.vitality);

        // Deep Ocean time-based background interpolation
        this.updateDeepOceanBackground(state.clockTime);

        // Info badges - Time
        document.getElementById('badgeTime').textContent = state.clockTime;
        document.getElementById('badgeDay').textContent = `Day ${state.dayNumber}`;

        // Info badges - Feels Like Temperature
        const feelsLikeTemp = Math.round(state.airTemp);
        document.getElementById('badgeFeelsLike').textContent = `${feelsLikeTemp}°F`;

        // Update temperature badge color based on temperature
        const tempBadge = document.querySelector('.temp-badge');
        if (tempBadge) {
            tempBadge.classList.remove('freezing', 'cold', 'warm', 'hot');
            if (feelsLikeTemp <= 20) {
                tempBadge.classList.add('freezing');
            } else if (feelsLikeTemp <= 40) {
                tempBadge.classList.add('cold');
            } else if (feelsLikeTemp >= 80) {
                tempBadge.classList.add('hot');
            } else if (feelsLikeTemp >= 60) {
                tempBadge.classList.add('warm');
            }
        }

        // Weather
        document.getElementById('weatherFront').textContent = state.weatherFront;
        const weatherEl = document.getElementById('weatherCond');
        weatherEl.textContent = state.weatherCondition;
        document.getElementById('weatherIcon').textContent = getWeatherIcon(state.weatherCondition);
        document.getElementById('windLabel').textContent = state.wind;
        document.getElementById('precipLabel').textContent = state.precipitation;

        // Fire
        FireDisplay.render(state.fire);

        // Temperature and Survival stats - skip if progress animation will handle interpolation
        if (!skipSurvivalStats) {
            TemperatureDisplay.render(state);
            SurvivalDisplay.render(state);
        }

        // Effects, Injuries & Capacities
        EffectsDisplay.render(state.effects);
        EffectsDisplay.renderInjuries(state.injuries, state.bloodPercent);
        EffectsDisplay.renderCapacities(state.capacities);

        // Inventory summary
        document.getElementById('carryDisplay').textContent =
            `${state.carryWeightKg.toFixed(1)} / ${state.maxWeightKg.toFixed(0)} kg`;
        const carryPct = Math.min(100, (state.carryWeightKg / state.maxWeightKg) * 100);
        const carryBar = document.getElementById('carryBar');
        if (carryBar) {
            carryBar.style.width = carryPct + '%';
        }
        document.getElementById('insulationPct').textContent = `${state.insulationPercent}%`;
        document.getElementById('fuelReserve').textContent = `${state.fuelKg.toFixed(1)} kg`;

        // Gear summary (from inventory)
        if (state.gearSummary) {
            this.renderGearSummary(state.gearSummary);
        }

        // Storage button visibility - only show when at a location with storage
        const storageRow = document.getElementById('storageRow');
        if (storageRow) {
            if (state.hasStorage) {
                show(storageRow);
            } else {
                hide(storageRow);
            }
        }

        // Narrative log
        NarrativeLog.render(state.log);
    }

    renderGearSummary(summary) {
        // Tool pills
        const pillsContainer = document.getElementById('toolPills');
        Utils.clearElement(pillsContainer);

        // Weapon
        if (summary.weaponName) {
            this.addToolPill(pillsContainer, summary.weaponName);
        }

        // Cutting tools
        if (summary.cuttingToolCount > 0) {
            this.addToolPill(pillsContainer,
                summary.cuttingToolCount > 1
                    ? `${summary.cuttingToolCount} blades`
                    : 'Blade');
        }

        // Fire starters
        if (summary.fireStarterCount > 0) {
            this.addToolPill(pillsContainer,
                summary.fireStarterCount > 1
                    ? `${summary.fireStarterCount} fire tools`
                    : 'Fire tool');
        }

        // Other tools
        if (summary.otherToolCount > 0) {
            this.addToolPill(pillsContainer,
                summary.otherToolCount > 1
                    ? `${summary.otherToolCount} tools`
                    : 'Tool');
        }

        // Food/Water combined summary
        const foodWaterSummary = document.getElementById('foodWaterSummary');
        if (foodWaterSummary) {
            if (summary.foodPortions === 0 && summary.waterPortions === 0) {
                foodWaterSummary.textContent = 'No food';
                foodWaterSummary.className = 'gear-value';
            } else {
                let parts = [];
                if (summary.foodPortions > 0) {
                    let foodText = `${summary.foodPortions} portions`;
                    if (summary.hasPreservedFood) foodText += ' +dried';
                    parts.push(foodText);
                }
                if (summary.waterPortions > 0) {
                    parts.push(`${summary.waterPortions}L water`);
                }
                foodWaterSummary.textContent = parts.join(', ');
                foodWaterSummary.className = 'gear-value';
            }
        }
    }

    addToolPill(container, text) {
        const pill = document.createElement('span');
        pill.className = 'badge badge--neutral';
        pill.textContent = text;
        container.appendChild(pill);
    }

    updateDeepOceanBackground(clockTime) {
        // Parse clock time (format: "h:mm tt" e.g. "9:00 AM")
        const minutesSinceMidnight = this.parseClockTime(clockTime);

        // Calculate time factor t (0 = midnight, 1 = noon)
        let t;
        if (minutesSinceMidnight <= 720) {
            // 12:00 AM → 12:00 PM (ascending toward noon)
            t = minutesSinceMidnight / 720;
        } else {
            // 12:00 PM → 12:00 AM (descending from noon)
            t = (1440 - minutesSinceMidnight) / 720;
        }

        // Deep Ocean anchor values
        const midnight = { h: 215, s: 30, l: 5 };
        const noon = { h: 212, s: 25, l: 26 };

        // Linear interpolation
        const h = midnight.h + (noon.h - midnight.h) * t;
        const s = midnight.s + (noon.s - midnight.s) * t;
        const l = midnight.l + (noon.l - midnight.l) * t;

        // Update CSS custom properties
        document.documentElement.style.setProperty('--bg-h', h.toFixed(1));
        document.documentElement.style.setProperty('--bg-s', s.toFixed(1) + '%');
        document.documentElement.style.setProperty('--bg-l', l.toFixed(1) + '%');

        // Update canvas grid renderer with same time factor
        if (this.gridRenderer) {
            this.gridRenderer.setTimeFactor(t);
        }
    }

    parseClockTime(clockTime) {
        // Parse "h:mm tt" format (e.g., "9:00 AM", "12:30 PM")
        const match = clockTime.match(/(\d+):(\d+)\s*(AM|PM)/i);
        if (!match) return 0;

        let hours = parseInt(match[1]);
        const minutes = parseInt(match[2]);
        const meridiem = match[3].toUpperCase();

        // Convert to 24-hour format
        if (meridiem === 'AM') {
            if (hours === 12) hours = 0; // 12 AM = 00:00
        } else {
            if (hours !== 12) hours += 12; // PM times except 12 PM
        }

        return hours * 60 + minutes;
    }

    /**
     * Set UI mode based on incoming frame mode
     */
    setMode(mode) {
        if (!mode) return;

        // Clear progress UI when switching away from progress mode
        // stop() will only hide the bar if it completed (reached 100%)
        // If interrupted, bar stays visible showing progress made
        if (mode.type !== 'progress' && mode.type !== 'travel_progress') {
            ProgressDisplay.stop();
        }

        switch (mode.type) {
            case 'location':
                this.setUIMode('location');
                break;
            case 'travel':
                this.setUIMode('travel');
                this.updateGridFromMode(mode);
                break;
            case 'travel_progress':
                // Travel progress: show grid and let FrameQueue handle animation
                this.setUIMode('travel');
                if (mode.grid) {
                    this.updateGrid(mode.grid);
                }
                break;
            case 'progress':
                // Progress animation is handled by FrameQueue
                break;
        }
    }

    /**
     * Update grid from TravelMode data
     */
    updateGridFromMode(mode) {
        if (mode.grid) {
            this.updateGrid(mode.grid);
        }
    }

    /**
     * Update grid with new state
     */
    updateGrid(gridState) {
        // Only hide popup if player actually moved to a different tile
        if (this.tilePopup) {
            const playerMoved = this.tilePopup.x !== gridState.playerX ||
                               this.tilePopup.y !== gridState.playerY;
            if (playerMoved) {
                this.hideTilePopup();
            }
        }

        // Initialize grid renderer if needed
        if (!this.gridInitialized) {
            this.gridRenderer = getGridRenderer();
            this.gridRenderer.init('gridCanvas', (x, y, tileData, screenPos) => {
                this.handleTileClick(x, y, tileData, screenPos);
            }, 'gridViewport');
            this.gridInitialized = true;
        }

        // Update grid with new state
        this.gridRenderer.update(gridState);
    }

    /**
     * Toggle UI mode between location (buttons visible) and travel (map visible)
     */
    setUIMode(mode) {
        const centerArea = document.querySelector('.center-area');
        centerArea.classList.remove('location-mode', 'travel-mode');
        centerArea.classList.add(`${mode}-mode`);
    }

    /**
     * Send player choice to server
     */
    respond(choiceId, inputId) {
        // Prevent duplicate responses or responses to stale buttons
        if (this.awaitingResponse) {
            return;
        }

        // Block clicks briefly after page resumes from idle to prevent accidental selection
        if (Date.now() < this.resumeBlockUntil) {
            return;
        }

        // Reject empty string choiceId - this indicates a bug in button creation
        if (choiceId === '') {
            console.error('[respond] Empty choiceId rejected! Stack:', new Error().stack);
            return;
        }

        // Verify this click is for the current input set (prevents stale button clicks)
        // Reject if inputId is missing/invalid OR doesn't match current
        if (!inputId || inputId <= 0 || inputId !== this.currentInputId) {
            console.log(`[respond] Rejecting stale click: button inputId=${inputId}, current=${this.currentInputId}`);
            this.awaitingResponse = false;
            return;
        }

        const validInputId = inputId;

        // Don't send if we don't have a valid input ID yet
        if (!validInputId || validInputId <= 0) {
            console.warn('[respond] No valid inputId available, ignoring click');
            this.awaitingResponse = false;
            return;
        }

        this.awaitingResponse = true;

        // Disable all action buttons immediately to prevent double-clicks
        document.querySelectorAll('.btn').forEach(btn => {
            btn.disabled = true;
        });

        if (this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify({ choiceId, inputId: validInputId }));
        }
    }

    /**
     * Update quick action button states based on available choices
     */
    updateQuickActionStates(choices) {
        const inventoryBtn = document.getElementById('inventoryBtn');
        const craftingBtn = document.getElementById('craftingBtn');
        const storageBtn = document.getElementById('storageBtn');

        if (!choices || choices.length === 0) {
            // No choices available - disable all
            if (inventoryBtn) inventoryBtn.disabled = true;
            if (craftingBtn) craftingBtn.disabled = true;
            if (storageBtn) storageBtn.disabled = true;
            return;
        }

        // Check if Inventory/Crafting/Storage are in the available choices
        const hasInventory = choices.some(c => c.label.includes('Inventory'));
        const hasCrafting = choices.some(c => c.label.includes('Crafting'));
        const hasStorage = choices.some(c => c.label.includes('Storage'));

        if (inventoryBtn) inventoryBtn.disabled = !hasInventory;
        if (craftingBtn) craftingBtn.disabled = !hasCrafting;
        if (storageBtn) storageBtn.disabled = !hasStorage;
    }

    /**
     * Request a quick action (Inventory, Crafting, Storage)
     */
    requestAction(action) {
        if (this.awaitingResponse) return;
        if (Date.now() < this.resumeBlockUntil) return;  // Block clicks briefly after page resume
        this.awaitingResponse = true;

        if (this.socket.readyState === WebSocket.OPEN) {
            const inputId = this.currentInputId || 0;
            if (inputId <= 0) {
                console.warn('[requestAction] No valid inputId, ignoring');
                this.awaitingResponse = false;
                return;
            }
            this.socket.send(JSON.stringify({
                type: 'action',
                action: action,
                inputId: inputId
            }));
        }
    }

    /**
     * Initialize tile popup click-away handler
     */
    initTilePopup() {
        // Close popup when clicking outside
        document.addEventListener('click', (e) => {
            const popup = document.getElementById('tilePopup');
            if (!popup.classList.contains('hidden') && !popup.contains(e.target)) {
                // Check if click was on the canvas - those are handled separately
                const canvas = document.getElementById('gridCanvas');
                if (!canvas || !canvas.contains(e.target)) {
                    this.hideTilePopup();
                }
            }
        });
    }

    /**
     * Handle tile click - show popup with tile info and actions
     */
    handleTileClick(x, y, tileData, screenPos) {
        if (this.awaitingResponse) return;

        // Hide any existing popup first
        this.hideTilePopup();

        // Only show new popup if tile data exists and is explored
        if (tileData && tileData.visibility !== 'unexplored') {
            // Store current tile for actions
            this.tilePopup = { x, y, tileData };

            // Show the popup
            this.showTilePopup(x, y, tileData, screenPos);
        }
    }

    /**
     * Show tile popup at screen position
     */
    showTilePopup(x, y, tileData, screenPos) {
        const popup = document.getElementById('tilePopup');
        const nameEl = document.getElementById('popupName');
        const terrainEl = document.getElementById('popupTerrain');
        const glanceEl = document.getElementById('popupGlance');
        const featuresEl = document.getElementById('popupFeatures');
        const actionsEl = document.getElementById('popupActions');

        // Set location info
        nameEl.textContent = tileData.locationName || tileData.terrain;
        terrainEl.textContent = tileData.locationName ? tileData.terrain : '';

        // Clear sections
        Utils.clearElement(glanceEl);
        Utils.clearElement(featuresEl);

        // Build quick glance badges (for visible locations)
        const isExplored = tileData.visibility === 'visible';
        if (isExplored) {
            this.buildGlanceBar(glanceEl, tileData);
        }

        // Build features list - use detailed features if available
        if (isExplored && tileData.featureDetails && tileData.featureDetails.length > 0) {
            this.buildDetailedFeatures(featuresEl, tileData.featureDetails);
        }

        const isPlayerHere = tileData.isPlayerHere;

        // Build actions
        Utils.clearElement(actionsEl);
        // Travel blocked if there's a pending input without Travel option
        const hasBlockingInput = this.currentInput?.choices?.length > 0 &&
            !this.currentInput.choices.some(c => c.label.includes('Travel'));
        const canTravel = tileData.isAdjacent && tileData.isPassable && !isPlayerHere && !hasBlockingInput;

        if (canTravel) {
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
                this.hideTilePopup();
                this.handleTravelToRequest(x, y);
            };
            actionsEl.appendChild(goBtn);
        }

        // Don't show any actions in popup when player is at current tile
        // All actions are now shown in the right sidebar instead

        // Position popup horizontally (screenPos.x is already at tile's right edge)
        popup.style.left = `${screenPos.x}px`;

        // Show popup to measure its height
        show(popup);
        const rect = popup.getBoundingClientRect();

        // Calculate vertically centered position (use visual tile size for screen positioning)
        const tileSize = this.gridRenderer.getVisualTileSize();
        const tileCenterY = screenPos.y + (tileSize / 2);
        let topPos = tileCenterY - (rect.height / 2);

        // Clamp to screen bounds
        if (topPos < 10) {
            topPos = 10;
        }
        if (topPos + rect.height > window.innerHeight - 10) {
            topPos = window.innerHeight - rect.height - 10;
        }

        popup.style.top = `${topPos}px`;

        // Adjust horizontal if popup would go off-screen (flip to left of tile)
        if (rect.right > window.innerWidth - 10) {
            popup.style.left = `${screenPos.x - rect.width - tileSize}px`;
        }
    }

    /**
     * Build quick glance bar with color-coded badges
     * Shows key info at a glance - scannable in 2 seconds
     */
    buildGlanceBar(container, tileData) {
        const badges = [];

        // Priority 1: Safety hazards (most important)
        if (tileData.terrainHazardLevel != null && tileData.terrainHazardLevel > 0.2) {
            const label = tileData.terrainHazardLevel > 0.5 ? 'Dangerous' : 'Hazardous';
            badges.push({ icon: 'warning', label, type: 'danger' });
        }

        if (tileData.climbRiskFactor != null && tileData.climbRiskFactor > 0.2) {
            badges.push({ icon: 'hiking', label: 'Climbing', type: 'danger' });
        }

        // Priority 2: Key resources
        if (tileData.featureIcons?.includes('local_fire_department')) {
            badges.push({ icon: 'local_fire_department', label: 'Fire', type: 'fire' });
        } else if (tileData.featureIcons?.includes('fireplace')) {
            badges.push({ icon: 'fireplace', label: 'Embers', type: 'fire' });
        }

        if (tileData.featureIcons?.includes('water_drop')) {
            badges.push({ icon: 'water_drop', label: 'Water', type: 'water' });
        }

        // Priority 3: Temperature effects
        if (tileData.temperatureDeltaF != null && tileData.temperatureDeltaF < -5) {
            badges.push({ icon: 'ac_unit', label: 'Cold', type: 'cold' });
        } else if (tileData.temperatureDeltaF != null && tileData.temperatureDeltaF > 5) {
            badges.push({ icon: 'sunny', label: 'Warm', type: 'warm' });
        }

        // Priority 4: Wind exposure
        if (tileData.windFactor != null && tileData.windFactor > 1.3) {
            badges.push({ icon: 'air', label: 'Exposed', type: 'danger' });
        } else if (tileData.windFactor != null && tileData.windFactor < 0.7) {
            badges.push({ icon: 'forest', label: 'Sheltered', type: 'good' });
        }

        // Priority 5: Special conditions
        if (tileData.isDark) {
            badges.push({ icon: 'dark_mode', label: 'Dark', type: 'neutral' });
        }

        if (tileData.isVantagePoint) {
            badges.push({ icon: 'visibility', label: 'Vantage', type: 'good' });
        }

        // Render badges (limit to 4 most important)
        badges.slice(0, 4).forEach(badge => {
            const badgeEl = document.createElement('span');
            badgeEl.className = `badge badge--${badge.type}`;

            const iconEl = document.createElement('span');
            iconEl.className = ICON_CLASS;
            iconEl.textContent = badge.icon;
            badgeEl.appendChild(iconEl);

            const labelEl = document.createElement('span');
            labelEl.textContent = badge.label;
            badgeEl.appendChild(labelEl);

            container.appendChild(badgeEl);
        });
    }

    /**
     * Build detailed feature cards
     */
    buildDetailedFeatures(container, featureDetails) {
        featureDetails.forEach(feature => {
            // Special compact layout for NPCs with health bars
            if (feature.type === 'npc' && feature.healthPct != null) {
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

                // Health bar - reuse survival-stat structure
                const healthRow = document.createElement('div');
                healthRow.className = 'survival-stat';
                healthRow.dataset.stat = 'health';

                const heartIcon = document.createElement('span');
                heartIcon.className = 'stat-icon material-symbols-outlined';
                heartIcon.textContent = 'monitor_heart';
                healthRow.appendChild(heartIcon);

                const barContainer = document.createElement('div');
                barContainer.className = 'bar bar--health';
                const barFill = document.createElement('div');
                const pct = Math.round(feature.healthPct * 100);
                barFill.className = 'bar__fill';
                if (pct < 30) barFill.classList.add('danger');
                else if (pct < 60) barFill.classList.add('warning');
                barFill.style.width = `${pct}%`;
                barContainer.appendChild(barFill);
                healthRow.appendChild(barContainer);

                const tooltip = document.createElement('div');
                tooltip.className = 'stat-tooltip';
                const statName = document.createElement('span');
                statName.className = 'stat-name';
                statName.textContent = 'Health';
                const statValue = document.createElement('span');
                statValue.className = 'stat-value';
                statValue.textContent = `${pct}%`;
                tooltip.appendChild(statName);
                tooltip.appendChild(statValue);
                healthRow.appendChild(tooltip);

                featureEl.appendChild(healthRow);
                container.appendChild(featureEl);
                return; // Skip normal rendering for this feature
            }

            // Standard feature rendering
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
            container.appendChild(featureEl);
        });
    }

    /**
     * Hide tile popup
     */
    hideTilePopup() {
        const popup = document.getElementById('tilePopup');
        hide(popup);
        this.tilePopup = null;
    }

    /**
     * Update just the action buttons in an already-visible tile popup
     * Actions are now in sidebar, so this just clears the popup actions
     */
    updateTilePopupActions() {
        if (!this.tilePopup) return;

        const actionsEl = document.getElementById('popupActions');
        if (!actionsEl) return;

        Utils.clearElement(actionsEl);
    }

    /**
     * Send travel_to request from map click
     */
    handleTravelToRequest(x, y) {
        if (this.awaitingResponse) return;
        if (Date.now() < this.resumeBlockUntil) return;  // Block clicks briefly after page resume
        this.awaitingResponse = true;

        // Record move for footprint history
        if (this.gridRenderer) {
            this.gridRenderer.recordMove();
        }

        if (this.socket.readyState === WebSocket.OPEN) {
            // Ensure valid inputId
            const inputId = this.currentInputId || 0;
            if (inputId <= 0) {
                console.warn('[handleTravelRequest] No valid inputId, ignoring');
                this.awaitingResponse = false;
                return;
            }
            this.socket.send(JSON.stringify({
                type: 'travel_to',
                targetX: x,
                targetY: y,
                inputId: inputId
            }));
        }
    }

    /**
     * Initialize quick action button handlers
     */
    initQuickActions() {
        const inventoryBtn = document.getElementById('inventoryBtn');
        const craftingBtn = document.getElementById('craftingBtn');
        const storageBtn = document.getElementById('storageBtn');

        if (inventoryBtn) {
            inventoryBtn.onclick = () => this.requestAction('inventory');
        }
        if (craftingBtn) {
            craftingBtn.onclick = () => this.requestAction('crafting');
        }
        if (storageBtn) {
            storageBtn.onclick = () => this.requestAction('storage');
        }
    }

    /**
     * Update available actions in right sidebar
     */
    updateAvailableActions(choices) {
        const panel = document.getElementById('availableActionsPanel');
        const container = document.getElementById('availableActions');

        if (!choices || choices.length === 0) {
            hide(panel);
            return;
        }

        // Filter to only work/exploration actions (those hidden from popup but not quick actions)
        const quickActions = ['Inventory', 'Crafting', 'Storage', 'Travel'];
        const workActions = choices.filter(c =>
            !quickActions.some(qa => c.label.includes(qa))
        );

        if (workActions.length === 0) {
            hide(panel);
            return;
        }

        // Show panel and populate actions
        show(panel);
        Utils.clearElement(container);

        const inputId = this.currentInputId;
        workActions.forEach(choice => {
            // Skip choices without IDs to prevent sending empty strings to server
            if (!choice.id) {
                console.error('[updateAvailableActions] Choice missing id, skipping:', choice);
                return;
            }
            const btn = document.createElement('button');
            btn.className = 'btn btn--sm btn--full';
            btn.textContent = choice.label;
            btn.onclick = () => {
                this.respond(choice.id, inputId);
            };
            container.appendChild(btn);
        });
    }

    /**
     * Initialize visibility change handler to prevent accidental clicks when page resumes from idle.
     * When a browser tab is hidden and becomes visible again, spurious events can sometimes occur.
     * This blocks input briefly after page resume to prevent unintended selections.
     */
    initVisibilityHandler() {
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible') {
                // Page just became visible - block clicks briefly to prevent accidental selection
                this.resumeBlockUntil = Date.now() + 200;  // Block for 200ms after resume
            }
        });
    }
}

// Initialize game client
const gameClient = new GameClient();