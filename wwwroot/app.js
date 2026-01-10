// modules/GameClient.js (simplified excerpt)
import { InputHandler } from './core/InputHandler.js';
import { EventOverlay } from './overlays/EventOverlay.js';
import { HuntOverlay } from './overlays/HuntOverlay.js';
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
import { show, hide, clear } from './lib/helpers.js';
import { ProgressDisplay } from './modules/progress.js';
import { FrameQueue } from './modules/frameQueue.js';
import { NotificationRenderer } from './modules/notifications.js';
import { TemperatureDisplay } from './modules/temperature.js';
import { FireDisplay } from './modules/fire.js';
import { SurvivalDisplay } from './modules/survival.js';
import { EffectsDisplay } from './modules/effects.js';
import { getGridRenderer } from './modules/grid/CanvasGridRenderer.js';
import { getCombatGridRenderer } from './modules/grid/CombatGridRenderer.js';
import { getWeatherIcon, getFeatureIconLabel } from './modules/icons.js';
import { TilePopupRenderer } from './lib/ui/TilePopupRenderer.js';

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
        this.tilePopupRenderer = null;
        this.resumeBlockUntil = 0;
        this.currentInput = null;

        // Core systems
        this.inputHandler = new InputHandler(
            () => this.socket,
            () => this.currentInputId
        );

        // Renderer state - only one renderer active at a time
        this.combatRenderer = null;
        this.activeRenderer = null;  // 'world' | 'combat'

        // Notification renderer
        this.notificationRenderer = new NotificationRenderer();

        // Overlay registry
        this.overlays = {
            event: new EventOverlay(this.inputHandler),
            hunt: new HuntOverlay(this.inputHandler),
            inventory: new InventoryOverlay(this.inputHandler),
            crafting: new CraftingOverlay(this.inputHandler),
            fire: new FireOverlay(this.inputHandler),
            eating: new EatingOverlay(this.inputHandler),
            discovery: new DiscoveryOverlay(this.inputHandler),
            weatherChange: new WeatherChangeOverlay(this.inputHandler),
            transfer: new TransferOverlay(this.inputHandler),
            forage: new ForageOverlay(this.inputHandler),
            butcher: new ButcherOverlay(this.inputHandler),
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
        const currentPopupTile = this.tilePopupRenderer?.getCurrentTile();
        if (currentPopupTile && tilePopupElement && !tilePopupElement.classList.contains('hidden')) {
            if (!isProgressMode && frame.input?.choices) {
                this.tilePopupRenderer.updateActions();
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
            this.tilePopupRenderer?.hide();

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

        // Deep Ocean time-based background - use pre-computed values from server
        this.updateDeepOceanBackground(state.background);

        // Info badges - Time
        document.getElementById('badgeTime').textContent = state.clockTime;
        document.getElementById('badgeDay').textContent = `Day ${state.dayNumber}`;

        // Info badges - Feels Like Temperature
        const feelsLikeTemp = Math.round(state.airTemp);
        document.getElementById('badgeFeelsLike').textContent = `${feelsLikeTemp}°F`;

        // Update temperature badge color - use pre-computed class from server
        const tempBadge = document.querySelector('.temp-badge');
        if (tempBadge) {
            tempBadge.classList.remove('freezing', 'cold', 'warm', 'hot');
            if (state.tempBadgeClass) {
                tempBadge.classList.add(state.tempBadgeClass);
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
        this.notificationRenderer.render(state.log);
    }

    renderGearSummary(summary) {
        // Tool pills
        const pillsContainer = document.getElementById('toolPills');
        clear(pillsContainer);

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

    updateDeepOceanBackground(background) {
        // Use pre-computed HSL values from server
        const { h, s, l } = background;

        // Update CSS custom properties
        document.documentElement.style.setProperty('--bg-h', h.toFixed(1));
        document.documentElement.style.setProperty('--bg-s', s.toFixed(1) + '%');
        document.documentElement.style.setProperty('--bg-l', l.toFixed(1) + '%');

        // Calculate time factor for grid renderer (0 = midnight, 1 = noon)
        // Derived from lightness: l=5 at midnight, l=26 at noon
        const t = (l - 5) / (26 - 5);
        if (this.gridRenderer) {
            this.gridRenderer.setTimeFactor(t);
        }
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
                this.deactivateCombatRenderer();
                this.setUIMode('location');
                break;
            case 'travel':
                this.deactivateCombatRenderer();
                this.setUIMode('travel');
                this.activateWorldRenderer(mode.grid);
                break;
            case 'travel_progress':
                // Travel progress: show grid and let FrameQueue handle animation
                this.deactivateCombatRenderer();
                this.setUIMode('travel');
                this.activateWorldRenderer(mode.grid);
                break;
            case 'progress':
                // Progress animation is handled by FrameQueue
                break;
            case 'combat':
                this.deactivateWorldRenderer();
                this.setUIMode('combat');
                this.activateCombatRenderer(mode.combat);
                break;
        }
    }

    /**
     * Activate world renderer - deactivates combat renderer if active
     */
    activateWorldRenderer(gridState) {
        if (this.activeRenderer === 'world' && this.gridRenderer) {
            // Already active, just update state
            if (gridState) {
                this.updateGrid(gridState);
            }
            return;
        }

        // Initialize grid renderer if needed
        if (!this.gridRenderer) {
            this.gridRenderer = getGridRenderer();
            this.gridRenderer.init('gridCanvas', (x, y, tileData, screenPos) => {
                this.handleTileClick(x, y, tileData, screenPos);
            }, 'gridViewport');

            // Initialize tile popup renderer
            this.tilePopupRenderer = new TilePopupRenderer({
                getVisualTileSize: () => this.gridRenderer.getVisualTileSize(),
                onTravelTo: (x, y) => this.handleTravelToRequest(x, y),
                canTravel: () => {
                    const hasBlockingInput = this.currentInput?.choices?.length > 0 &&
                        !this.currentInput.choices.some(c => c.label.includes('Travel'));
                    return !hasBlockingInput;
                }
            });
        } else {
            // Activate existing renderer
            this.gridRenderer.activate();
        }

        if (gridState) {
            this.gridRenderer.update(gridState);
        }

        this.activeRenderer = 'world';
    }

    /**
     * Deactivate world renderer
     */
    deactivateWorldRenderer() {
        if (this.activeRenderer !== 'world') return;

        if (this.gridRenderer) {
            this.gridRenderer.deactivate();
        }
        this.tilePopupRenderer?.hide();
        this.activeRenderer = null;
    }

    /**
     * Activate combat renderer - deactivates world renderer if active
     */
    activateCombatRenderer(combatState) {
        if (this.activeRenderer === 'combat' && this.combatRenderer) {
            // Already active, just update state
            this.combatRenderer.update(combatState);
            this.updateCombatNarrative(combatState);
            return;
        }

        // Initialize combat renderer if needed
        if (!this.combatRenderer) {
            this.combatRenderer = getCombatGridRenderer();
            this.combatRenderer.init(
                document.getElementById('gridCanvas'),
                (unit) => this.handleCombatUnitClick(unit)
            );
        }

        // Activate and update
        this.combatRenderer.activate();
        this.combatRenderer.update(combatState);
        this.updateCombatNarrative(combatState);
        this.activeRenderer = 'combat';
    }

    /**
     * Deactivate combat renderer
     */
    deactivateCombatRenderer() {
        if (this.activeRenderer !== 'combat') return;

        if (this.combatRenderer) {
            this.combatRenderer.deactivate();
        }
        this.activeRenderer = null;
    }

    /**
     * Update narrative log with combat message
     */
    updateCombatNarrative(combatState) {
        if (combatState?.narrativeMessage) {
            const logEl = document.getElementById('narrativeLog');
            if (logEl) {
                clear(logEl);
                const entry = document.createElement('div');
                entry.className = 'log-entry';
                entry.textContent = combatState.narrativeMessage;
                logEl.appendChild(entry);
            }
        }
    }

    /**
     * Handle combat unit click - show unit detail panel
     */
    handleCombatUnitClick(unit) {
        const detailPanel = document.getElementById('combatUnitDetail');
        if (!detailPanel) return;

        if (!unit) {
            detailPanel.classList.remove('visible');
            return;
        }

        clear(detailPanel);

        // Header with icon and name
        const header = document.createElement('div');
        header.className = 'unit-detail-header';
        const iconSpan = document.createElement('span');
        iconSpan.className = 'unit-detail-icon';
        iconSpan.textContent = unit.icon;
        const nameSpan = document.createElement('span');
        nameSpan.className = 'unit-detail-name';
        nameSpan.textContent = unit.name;
        header.appendChild(iconSpan);
        header.appendChild(nameSpan);
        detailPanel.appendChild(header);

        // Vitality gauge
        const vitalityPct = (unit.vitality * 100).toFixed(0);
        const vitalityDiv = document.createElement('div');
        vitalityDiv.className = 'unit-detail-gauge';
        const vitalityLabel = document.createElement('div');
        vitalityLabel.className = 'unit-detail-gauge-label';
        vitalityLabel.innerHTML = `<span>Vitality</span><span class="unit-detail-gauge-value">${vitalityPct}%</span>`;
        const vitalityBar = document.createElement('div');
        vitalityBar.className = 'unit-detail-gauge-bar';
        const vitalityFill = document.createElement('div');
        vitalityFill.className = 'unit-detail-gauge-fill unit-detail-gauge-fill--vitality';
        vitalityFill.style.width = vitalityPct + '%';
        vitalityBar.appendChild(vitalityFill);
        vitalityDiv.appendChild(vitalityLabel);
        vitalityDiv.appendChild(vitalityBar);
        detailPanel.appendChild(vitalityDiv);

        // Threat gauge (shown for all units - affects how others react)
        const threatValue = unit.threat.toFixed(1);
        const threatDiv = document.createElement('div');
        threatDiv.className = 'unit-detail-gauge';
        const threatLabel = document.createElement('div');
        threatLabel.className = 'unit-detail-gauge-label';
        threatLabel.innerHTML = `<span>Threat</span><span class="unit-detail-gauge-value">${threatValue}</span>`;
        const threatBar = document.createElement('div');
        threatBar.className = 'unit-detail-gauge-bar';
        const threatFill = document.createElement('div');
        threatFill.className = 'unit-detail-gauge-fill unit-detail-gauge-fill--threat';
        threatFill.style.width = Math.min(100, unit.threat * 50) + '%';
        threatBar.appendChild(threatFill);
        threatDiv.appendChild(threatLabel);
        threatDiv.appendChild(threatBar);
        detailPanel.appendChild(threatDiv);

        // Boldness and Aggression gauges for enemies
        if (unit.team === 'enemy') {
            // Boldness gauge
            const boldnessValue = unit.boldness.toFixed(2);
            const boldnessDiv = document.createElement('div');
            boldnessDiv.className = 'unit-detail-gauge';
            const boldnessLabel = document.createElement('div');
            boldnessLabel.className = 'unit-detail-gauge-label';
            boldnessLabel.innerHTML = `<span>Boldness</span><span class="unit-detail-gauge-value ${unit.boldnessDescriptor}">${boldnessValue}</span>`;
            const boldnessBar = document.createElement('div');
            boldnessBar.className = 'unit-detail-gauge-bar';
            const boldnessFill = document.createElement('div');
            boldnessFill.className = 'unit-detail-gauge-fill unit-detail-gauge-fill--boldness';
            boldnessFill.style.width = Math.min(100, unit.boldness * 50) + '%';
            boldnessBar.appendChild(boldnessFill);
            boldnessDiv.appendChild(boldnessLabel);
            boldnessDiv.appendChild(boldnessBar);
            detailPanel.appendChild(boldnessDiv);

            // Aggression gauge
            const aggressionValue = unit.aggression.toFixed(2);
            const aggressionDiv = document.createElement('div');
            aggressionDiv.className = 'unit-detail-gauge';
            const aggressionLabel = document.createElement('div');
            aggressionLabel.className = 'unit-detail-gauge-label';
            aggressionLabel.innerHTML = `<span>Aggression</span><span class="unit-detail-gauge-value">${aggressionValue}</span>`;
            const aggressionBar = document.createElement('div');
            aggressionBar.className = 'unit-detail-gauge-bar';
            const aggressionFill = document.createElement('div');
            aggressionFill.className = 'unit-detail-gauge-fill unit-detail-gauge-fill--aggression';
            aggressionFill.style.width = Math.min(100, unit.aggression * 100) + '%';
            aggressionBar.appendChild(aggressionFill);
            aggressionDiv.appendChild(aggressionLabel);
            aggressionDiv.appendChild(aggressionBar);
            detailPanel.appendChild(aggressionDiv);

            // Threat factors from combat state
            const combatState = this.combatRenderer?.combatState;
            if (combatState?.threatFactors?.length > 0) {
                const threatDiv = document.createElement('div');
                threatDiv.className = 'unit-detail-threats';
                const threatLabel = document.createElement('div');
                threatLabel.className = 'unit-detail-threats-label';
                threatLabel.textContent = 'Threat factors:';
                threatDiv.appendChild(threatLabel);

                const threatList = document.createElement('div');
                threatList.className = 'unit-detail-threats-list';
                for (const factor of combatState.threatFactors) {
                    const factorEl = document.createElement('div');
                    factorEl.className = 'unit-detail-threat';
                    factorEl.innerHTML = `<span class="material-symbols-outlined">${factor.icon}</span>${factor.description}`;
                    threatList.appendChild(factorEl);
                }
                threatDiv.appendChild(threatList);
                detailPanel.appendChild(threatDiv);
            }
        }

        detailPanel.classList.add('visible');
    }

    /**
     * Update grid with new state (called by activateWorldRenderer)
     */
    updateGrid(gridState) {
        // Only hide popup if player actually moved to a different tile
        const currentTile = this.tilePopupRenderer?.getCurrentTile();
        if (currentTile) {
            const playerMoved = currentTile.x !== gridState.playerX ||
                               currentTile.y !== gridState.playerY;
            if (playerMoved) {
                this.tilePopupRenderer.hide();
            }
        }

        // Update grid with new state
        if (this.gridRenderer) {
            this.gridRenderer.update(gridState);
        }
    }

    /**
     * Toggle UI mode between location, travel, and combat
     */
    setUIMode(mode) {
        const centerArea = document.querySelector('.center-area');
        centerArea.classList.remove('location-mode', 'travel-mode', 'combat-mode');
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
                    this.tilePopupRenderer?.hide();
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
        this.tilePopupRenderer.hide();

        // Only show new popup if tile data exists and is explored
        if (tileData && tileData.visibility !== 'unexplored') {
            this.tilePopupRenderer.show(x, y, tileData, screenPos);
        }
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
        const inCombat = this.activeRenderer === 'combat';

        // Handle combat mode Continue button when no choices (anykey input)
        if (inCombat && (!choices || choices.length === 0)) {
            if (this.currentInput?.type === 'anykey') {
                show(panel);
                clear(container);
                const inputId = this.currentInputId;
                const btn = document.createElement('button');
                btn.className = 'btn btn--primary btn--full';
                btn.textContent = this.currentInput.prompt || 'Continue';
                btn.onclick = () => this.respond('continue', inputId);
                container.appendChild(btn);
                return;
            }
            hide(panel);
            return;
        }

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
        clear(container);

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