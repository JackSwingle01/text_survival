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
const POPUP_HIDDEN_ACTIONS = ['Inventory', 'Crafting', 'Travel'];

class GameClient {
    constructor() {
        this.socket = null;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.awaitingResponse = false;  // Prevent button clicks during frame transitions
        this.currentInputId = 0;        // Track which input set is active
        this.gridRenderer = null;       // Canvas grid renderer
        this.gridInitialized = false;
        this.tilePopup = null;          // Current tile popup state
        this.lastHuntTime = 0;          // Track hunt time for animation

        // Initialize FrameQueue with render callback
        FrameQueue.init((frame) => this.renderFrame(frame));

        this.connect();
        this.initQuickActions();
        this.initTilePopup();
    }

    /**
     * Initialize persistent quick action buttons (inventory, crafting, storage)
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
     * Initialize tile popup system
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

    connect() {
        const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
        this.socket = new WebSocket(`${protocol}//${location.host}/ws`);

        this.socket.onopen = () => {
            this.reconnectAttempts = 0;
            this.awaitingResponse = false;
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
     * Render a frame - called by FrameQueue after sequencing
     */
    renderFrame(frame) {
        this.awaitingResponse = false;
        ProgressDisplay.stop();

        // Re-enable all buttons from previous frame
        document.querySelectorAll('.action-btn').forEach(btn => {
            btn.disabled = false;
        });

        // CRITICAL: Update currentInputId FIRST, before rendering anything
        // Overlays and buttons need the current inputId to send valid responses
        if (frame.input?.inputId) {
            this.currentInputId = frame.input.inputId;
        }

        // Render game state
        if (frame.state) {
            this.renderState(frame.state);
        }

        // Set mode (mutually exclusive)
        this.setMode(frame.mode);

        // Render overlays (stackable) - pass input for overlay action buttons
        this.clearOverlays();
        const hasOverlay = (frame.overlays?.length || 0) > 0;
        for (const overlay of frame.overlays || []) {
            this.showOverlay(overlay, frame.input);
        }

        // Update quick action button states
        this.updateQuickActionStates(frame.input?.choices);

        // Store input for tile popup access
        this.currentInput = frame.input;

        // Update tile popup if visible and we have new choices (no overlay active)
        if (!hasOverlay && this.tilePopup && frame.input?.choices?.length > 0) {
            this.updateTilePopupActions();
        }

        // All actions now handled via popups:
        // - Location mode: tile popup
        // - Travel mode: tile popup (Go) + hazard overlay
        // - Events: event overlay
    }

    /**
     * Set the UI mode based on frame.mode
     */
    setMode(mode) {
        if (!mode) return;

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
     * Show an overlay based on its type
     */
    showOverlay(overlay, input) {
        switch (overlay.type) {
            case 'inventory':
                this.showInventory(overlay.data, input);
                break;
            case 'crafting':
                this.showCrafting(overlay.data, input);
                break;
            case 'event':
                this.showEventPopup(overlay.data);
                break;
            case 'hazard':
                this.showHazardPrompt(overlay.data);
                break;
            case 'confirm':
                this.showConfirmPrompt(overlay.prompt, input);
                break;
            case 'forage':
                this.showForagePopup(overlay.data);
                break;
            case 'deathScreen':
                this.showDeathScreen(overlay.data, input);
                break;
            case 'hunt':
                this.showHuntPopup(overlay.data);
                break;
        }
    }

    /**
     * Clear all overlays
     */
    clearOverlays() {
        this.hideInventory();
        this.hideCrafting();
        this.hideEventPopup();
        this.hideHazardPrompt();
        this.hideConfirmPrompt();
        this.hideForagePopup();
        this.hideDeathScreen();
        this.hideHuntPopup();
    }

    /**
     * Show event popup overlay
     */
    showEventPopup(eventData) {
        const overlay = document.getElementById('eventOverlay');
        if (!overlay) return;

        show(overlay);

        const nameEl = document.getElementById('eventName');
        const descEl = document.getElementById('eventDescription');
        const choicesEl = document.getElementById('eventChoices');

        if (nameEl) nameEl.textContent = eventData.name;

        // Check if this is outcome mode (has outcome data, no choices)
        if (eventData.outcome) {
            this.showEventOutcome(eventData, descEl, choicesEl);
        } else {
            this.showEventChoices(eventData, descEl, choicesEl);
        }
    }

    /**
     * Show event choices (Phase 1)
     */
    showEventChoices(eventData, descEl, choicesEl) {
        if (descEl) descEl.textContent = eventData.description;

        // Capture input ID for this set of buttons (from backend)
        const inputId = this.currentInputId;

        if (choicesEl) {
            Utils.clearElement(choicesEl);
            eventData.choices.forEach((choice) => {
                const btn = document.createElement('button');
                btn.className = 'event-choice-btn';
                btn.disabled = !choice.isAvailable;

                const label = document.createElement('span');
                label.className = 'choice-label';
                label.textContent = choice.label;
                btn.appendChild(label);

                if (choice.description) {
                    const desc = document.createElement('span');
                    desc.className = 'choice-desc';
                    desc.textContent = choice.description;
                    btn.appendChild(desc);
                }

                btn.onclick = () => this.respond(choice.id, inputId);
                choicesEl.appendChild(btn);
            });
        }
    }

    /**
     * Show event outcome (Phase 2)
     */
    showEventOutcome(eventData, descEl, choicesEl) {
        const outcome = eventData.outcome;
        const progressEl = document.getElementById('eventProgress');
        const progressBar = document.getElementById('eventProgressBar');
        const progressText = document.getElementById('eventProgressText');

        // If there's time added, show progress animation first
        if (outcome.timeAddedMinutes > 0) {
            // Hide content during progress
            hide(descEl);
            hide(choicesEl);

            // Show and animate progress bar
            show(progressEl);
            progressText.textContent = `${eventData.description} (+${outcome.timeAddedMinutes} min)`;
            progressBar.style.width = '0%';

            // Convert game minutes to animation seconds (~5 game-min per real second)
            const durationSeconds = Math.max(0.5, outcome.timeAddedMinutes / 5);
            const durationMs = durationSeconds * 1000;
            const startTime = Date.now();

            const animateProgress = () => {
                const elapsed = Date.now() - startTime;
                const pct = Math.min(100, Math.round((elapsed / durationMs) * 100));
                progressBar.style.width = pct + '%';

                if (pct < 100) {
                    requestAnimationFrame(animateProgress);
                } else {
                    // Animation complete - show outcome
                    setTimeout(() => {
                        hide(progressEl);
                        this.showOutcomeContent(eventData, descEl, choicesEl);
                    }, 150);
                }
            };

            requestAnimationFrame(animateProgress);
        } else {
            // No time added - show outcome immediately
            hide(progressEl);
            this.showOutcomeContent(eventData, descEl, choicesEl);
        }
    }

    /**
     * Show outcome content (after progress animation if any)
     */
    showOutcomeContent(eventData, descEl, choicesEl) {
        const outcome = eventData.outcome;

        // Show choice context + outcome message
        if (descEl) {
            show(descEl);
            Utils.clearElement(descEl);

            // Choice context (what the player chose)
            const contextEl = document.createElement('div');
            contextEl.className = 'event-choice-context';
            contextEl.textContent = eventData.description;
            descEl.appendChild(contextEl);

            // Outcome message
            const messageEl = document.createElement('div');
            messageEl.className = 'event-outcome-message';
            messageEl.textContent = outcome.message;
            descEl.appendChild(messageEl);
        }

        // Build outcome summary
        if (choicesEl) {
            show(choicesEl);
            Utils.clearElement(choicesEl);

            const summaryEl = document.createElement('div');
            summaryEl.className = 'event-outcome-summary';

            // Time added (already shown in progress, but include in summary)
            if (outcome.timeAddedMinutes > 0) {
                this.addOutcomeItem(summaryEl, 'schedule',
                    `+${outcome.timeAddedMinutes} minutes`, 'time');
            }

            // Damage taken
            if (outcome.damageTaken && outcome.damageTaken.length > 0) {
                outcome.damageTaken.forEach(dmg => {
                    this.addOutcomeItem(summaryEl, 'personal_injury', dmg, 'damage');
                });
            }

            // Effects applied
            if (outcome.effectsApplied && outcome.effectsApplied.length > 0) {
                outcome.effectsApplied.forEach(effect => {
                    this.addOutcomeItem(summaryEl, 'warning', effect, 'effect');
                });
            }

            // Items gained
            if (outcome.itemsGained && outcome.itemsGained.length > 0) {
                outcome.itemsGained.forEach(item => {
                    this.addOutcomeItem(summaryEl, 'add', item, 'gain');
                });
            }

            // Items lost
            if (outcome.itemsLost && outcome.itemsLost.length > 0) {
                outcome.itemsLost.forEach(item => {
                    this.addOutcomeItem(summaryEl, 'remove', item, 'loss');
                });
            }

            // Tensions changed
            if (outcome.tensionsChanged && outcome.tensionsChanged.length > 0) {
                outcome.tensionsChanged.forEach(tension => {
                    const isPositive = tension.startsWith('-');
                    this.addOutcomeItem(summaryEl, isPositive ? 'trending_down' : 'trending_up',
                        tension, isPositive ? 'tension-down' : 'tension-up');
                });
            }

            // Stats delta (energy, calories, hydration, temperature changes) - grouped at bottom
            if (outcome.statsDelta) {
                const d = outcome.statsDelta;
                const statItems = [];
                if (Math.abs(d.energyDelta) >= 1) {
                    const val = Math.round(d.energyDelta);
                    statItems.push({ icon: 'bolt', text: `${val > 0 ? '+' : ''}${val} energy` });
                }
                if (Math.abs(d.calorieDelta) >= 1) {
                    const val = Math.round(d.calorieDelta);
                    statItems.push({ icon: 'restaurant', text: `${val > 0 ? '+' : ''}${val} kcal` });
                }
                if (Math.abs(d.hydrationDelta) >= 10) {
                    const val = Math.round(d.hydrationDelta);
                    statItems.push({ icon: 'water_drop', text: `${val > 0 ? '+' : ''}${val} mL` });
                }
                if (Math.abs(d.temperatureDelta) >= 0.1) {
                    const val = d.temperatureDelta.toFixed(1);
                    statItems.push({ icon: 'thermostat', text: `${d.temperatureDelta > 0 ? '+' : ''}${val}°F` });
                }
                if (statItems.length > 0) {
                    const statsGroup = document.createElement('div');
                    statsGroup.className = 'outcome-stats-group';
                    statItems.forEach(item => {
                        this.addOutcomeItem(statsGroup, item.icon, item.text, 'stat');
                    });
                    summaryEl.appendChild(statsGroup);
                }
            }

            // Only show summary if there's content
            if (summaryEl.children.length > 0) {
                choicesEl.appendChild(summaryEl);
            }

            // Continue button - uses null to signal "just continue"
            const inputId = this.currentInputId;

            const continueBtn = document.createElement('button');
            continueBtn.className = 'event-continue-btn';
            continueBtn.textContent = 'Continue';
            continueBtn.onclick = () => this.respond('continue', inputId);
            choicesEl.appendChild(continueBtn);
        }
    }

    /**
     * Add an outcome summary item with icon
     */
    addOutcomeItem(container, icon, text, styleClass) {
        const item = document.createElement('div');
        item.className = `outcome-item ${styleClass}`;

        const iconEl = document.createElement('span');
        iconEl.className = ICON_CLASS;
        iconEl.textContent = icon;
        item.appendChild(iconEl);

        const textEl = document.createElement('span');
        textEl.textContent = text;
        item.appendChild(textEl);

        container.appendChild(item);
    }

    /**
     * Hide event popup overlay
     */
    hideEventPopup() {
        const overlay = document.getElementById('eventOverlay');
        hide(overlay);

        // Reset progress bar state
        const progressEl = document.getElementById('eventProgress');
        const progressBar = document.getElementById('eventProgressBar');
        if (progressEl) hide(progressEl);
        if (progressBar) progressBar.style.width = '0%';
    }

    /**
     * Update grid display with new state
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
            });
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
     * Handle tile click - show popup with tile info and actions
     */
    handleTileClick(x, y, tileData, screenPos) {
        if (this.awaitingResponse) return;

        // Hide any existing popup first
        this.hideTilePopup();

        // Store current tile for actions
        this.tilePopup = { x, y, tileData };

        // Show the popup
        this.showTilePopup(x, y, tileData, screenPos);
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
        const promptEl = document.getElementById('popupPrompt');
        const actionsEl = document.getElementById('popupActions');

        // Set location info
        nameEl.textContent = tileData.locationName || tileData.terrain;
        terrainEl.textContent = tileData.locationName ? tileData.terrain : '';

        // Clear sections
        Utils.clearElement(glanceEl);
        Utils.clearElement(featuresEl);

        // Build quick glance badges (for explored locations)
        const isExplored = tileData.visibility === 'visible' && tileData.locationName && tileData.locationName !== '???';
        if (isExplored) {
            this.buildGlanceBar(glanceEl, tileData);
        }

        // Build features list - use detailed features if available, otherwise icons
        if (isExplored && tileData.featureDetails && tileData.featureDetails.length > 0) {
            this.buildDetailedFeatures(featuresEl, tileData.featureDetails);
        } else if (tileData.featureIcons && tileData.featureIcons.length > 0) {
            tileData.featureIcons.forEach(icon => {
                const featureEl = document.createElement('div');
                featureEl.className = 'popup-feature';

                // Add special classes for certain icons
                if (icon === 'local_fire_department' || icon === 'fireplace') {
                    featureEl.classList.add('fire');
                } else if (icon === 'water_drop') {
                    featureEl.classList.add('water');
                } else if (icon === 'catching_pokemon' || icon === 'done_all') {
                    featureEl.classList.add('urgent');
                }

                const iconEl = document.createElement('span');
                iconEl.className = ICON_CLASS;
                iconEl.textContent = icon;
                featureEl.appendChild(iconEl);

                const labelEl = document.createElement('span');
                labelEl.textContent = this.getIconLabel(icon);
                featureEl.appendChild(labelEl);

                featuresEl.appendChild(featureEl);
            });
        }

        // Show prompt if player is here and there's a prompt
        const isPlayerHere = tileData.isPlayerHere;
        if (isPlayerHere && this.currentInput?.prompt) {
            promptEl.textContent = this.currentInput.prompt;
            show(promptEl);
        } else {
            hide(promptEl);
        }

        // Build actions
        Utils.clearElement(actionsEl);
        // Travel blocked if there's a pending input without Travel option
        const hasBlockingInput = this.currentInput?.choices?.length > 0 &&
            !this.currentInput.choices.some(c => c.label.includes('Travel'));
        const canTravel = tileData.isAdjacent && tileData.isPassable && !isPlayerHere && !hasBlockingInput;

        if (canTravel) {
            const goBtn = document.createElement('button');
            goBtn.className = 'popup-action-btn primary';

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

        // Show location actions when clicking current tile
        if (isPlayerHere && this.currentInput?.choices) {
            const inputId = this.currentInputId;

            this.currentInput.choices.forEach((choice) => {
                if (POPUP_HIDDEN_ACTIONS.some(action => choice.label.includes(action))) return;

                const btn = document.createElement('button');
                btn.className = 'popup-action-btn';
                btn.textContent = choice.label;
                btn.onclick = (e) => {
                    e.stopPropagation();
                    // Don't hide popup - let next frame update it with new choices
                    this.respond(choice.id, inputId);
                };
                actionsEl.appendChild(btn);
            });
        }

        // Position popup horizontally
        popup.style.left = `${screenPos.x + 8}px`;

        // Show popup to measure its height
        show(popup);
        const rect = popup.getBoundingClientRect();

        // Calculate vertically centered position
        const tileSize = this.gridRenderer.TILE_SIZE;
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

        // Adjust horizontal if popup would go off-screen
        if (rect.right > window.innerWidth - 10) {
            popup.style.left = `${screenPos.x - rect.width - tileSize - 16}px`;
        }
    }

    /**
     * Get human-readable label for a feature icon
     * (delegates to centralized icons module)
     */
    getIconLabel(icon) {
        return getFeatureIconLabel(icon);
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
            badgeEl.className = `glance-badge ${badge.type}`;

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
            const featureEl = document.createElement('div');
            featureEl.className = 'popup-feature-detailed';

            const iconEl = document.createElement('span');
            iconEl.className = ICON_CLASS;
            iconEl.textContent = getFeatureTypeIcon(feature.type);
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

            if (feature.details && feature.details.length > 0) {
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
     * Helper to add a simple icon + text item to a section
     */
    addPopupItem(container, icon, text, typeClass) {
        const item = document.createElement('div');
        item.className = `popup-item ${typeClass}`;

        const iconEl = document.createElement('span');
        iconEl.className = ICON_CLASS;
        iconEl.textContent = icon;
        item.appendChild(iconEl);

        const textEl = document.createElement('span');
        textEl.textContent = text;
        item.appendChild(textEl);

        container.appendChild(item);
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
     */
    updateTilePopupActions() {
        if (!this.tilePopup) return;

        const promptEl = document.getElementById('popupPrompt');
        const actionsEl = document.getElementById('popupActions');
        if (!actionsEl) return;

        Utils.clearElement(actionsEl);

        // Only show actions if player is at this tile
        if (!this.tilePopup.tileData?.isPlayerHere) {
            hide(promptEl);
            return;
        }

        // Update prompt
        if (this.currentInput?.prompt && promptEl) {
            promptEl.textContent = this.currentInput.prompt;
            show(promptEl);
        } else {
            hide(promptEl);
        }

        // Build action buttons from current input choices
        if (this.currentInput?.choices) {
            const inputId = this.currentInputId;

            this.currentInput.choices.forEach((choice) => {
                if (POPUP_HIDDEN_ACTIONS.some(action => choice.label.includes(action))) return;

                const btn = document.createElement('button');
                btn.className = 'popup-action-btn';
                btn.textContent = choice.label;
                btn.onclick = (e) => {
                    e.stopPropagation();
                    this.respond(choice.id, inputId);
                };
                actionsEl.appendChild(btn);
            });
        }
    }

    /**
     * Send travel_to request from map click
     */
    handleTravelToRequest(x, y) {
        if (this.awaitingResponse) return;
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
     * Show hazard choice prompt as overlay popup
     */
    showHazardPrompt(hazardPrompt) {
        const overlay = document.getElementById('hazardOverlay');
        const descEl = document.getElementById('hazardDescription');
        const choicesEl = document.getElementById('hazardChoices');

        descEl.textContent = hazardPrompt.hazardDescription;

        Utils.clearElement(choicesEl);

        // Quick option
        const quickBtn = document.createElement('button');
        quickBtn.className = 'event-choice-btn';

        const quickLabel = document.createElement('span');
        quickLabel.className = 'choice-label';
        quickLabel.textContent = 'Quick';
        quickBtn.appendChild(quickLabel);

        const quickDesc = document.createElement('span');
        quickDesc.className = 'choice-desc';
        quickDesc.textContent = `${hazardPrompt.quickTimeMinutes} min • ${hazardPrompt.injuryRiskPercent.toFixed(0)}% injury risk`;
        quickBtn.appendChild(quickDesc);

        quickBtn.onclick = () => {
            this.hideHazardPrompt();
            this.respondHazardChoice(true);
        };
        choicesEl.appendChild(quickBtn);

        // Careful option
        const carefulBtn = document.createElement('button');
        carefulBtn.className = 'event-choice-btn';

        const carefulLabel = document.createElement('span');
        carefulLabel.className = 'choice-label';
        carefulLabel.textContent = 'Careful';
        carefulBtn.appendChild(carefulLabel);

        const carefulDesc = document.createElement('span');
        carefulDesc.className = 'choice-desc';
        carefulDesc.textContent = `${hazardPrompt.carefulTimeMinutes} min • Safe passage`;
        carefulBtn.appendChild(carefulDesc);

        carefulBtn.onclick = () => {
            this.hideHazardPrompt();
            this.respondHazardChoice(false);
        };
        choicesEl.appendChild(carefulBtn);

        show(overlay);
    }

    /**
     * Hide hazard prompt overlay
     */
    hideHazardPrompt() {
        hide(document.getElementById('hazardOverlay'));
    }

    /**
     * Show confirm prompt overlay
     */
    showConfirmPrompt(prompt, input) {
        const overlay = document.getElementById('confirmOverlay');
        const promptEl = document.getElementById('confirmPrompt');
        const choicesEl = document.getElementById('confirmChoices');

        if (!overlay) return;

        promptEl.textContent = prompt;
        Utils.clearElement(choicesEl);

        // Track input ID for button deduplication
        const inputId = this.currentInputId;

        // Create Yes/No buttons from input choices
        if (input?.choices) {
            input.choices.forEach(choice => {
                const btn = document.createElement('button');
                btn.className = 'event-choice-btn';

                const label = document.createElement('span');
                label.className = 'choice-label';
                label.textContent = choice.label;
                btn.appendChild(label);

                btn.onclick = () => {
                    this.hideConfirmPrompt();
                    this.respond(choice.id, inputId);
                };
                choicesEl.appendChild(btn);
            });
        }

        show(overlay);
    }

    /**
     * Hide confirm prompt overlay
     */
    hideConfirmPrompt() {
        const overlay = document.getElementById('confirmOverlay');
        hide(overlay);
    }

    /**
     * Show death screen overlay
     */
    showDeathScreen(data, input) {
        const overlay = document.getElementById('deathOverlay');
        const causeEl = document.getElementById('deathCause');
        const statsEl = document.getElementById('deathStats');
        const choicesEl = document.getElementById('deathChoices');

        if (!overlay) return;

        causeEl.textContent = data.causeOfDeath;

        // Build stats using safe DOM methods
        Utils.clearElement(statsEl);
        const statLines = [
            `Time Survived: ${data.timeSurvived}`,
            `Final Vitality: ${data.finalVitality.toFixed(0)}%`,
            `Final Calories: ${data.finalCalories.toFixed(0)} kcal`,
            `Final Hydration: ${data.finalHydration.toFixed(0)}%`,
            `Body Temperature: ${data.finalTemperature.toFixed(1)}°F`
        ];
        statLines.forEach(line => {
            const div = document.createElement('div');
            div.textContent = line;
            statsEl.appendChild(div);
        });

        // Add restart button
        Utils.clearElement(choicesEl);
        const inputId = this.currentInputId;

        if (input?.choices) {
            input.choices.forEach(choice => {
                const btn = document.createElement('button');
                btn.className = 'event-choice-btn';

                const label = document.createElement('span');
                label.className = 'choice-label';
                label.textContent = choice.label;
                btn.appendChild(label);

                btn.onclick = () => {
                    this.hideDeathScreen();
                    this.respond(choice.id, inputId);
                };
                choicesEl.appendChild(btn);
            });
        }

        show(overlay);
    }

    /**
     * Hide death screen overlay
     */
    hideDeathScreen() {
        const overlay = document.getElementById('deathOverlay');
        hide(overlay);
    }

    /**
     * Show forage popup overlay
     */
    showForagePopup(forageData) {
        const overlay = document.getElementById('forageOverlay');
        if (!overlay) return;

        show(overlay);

        // Store selection state
        this.forageSelection = {
            focusId: null,
            timeId: null
        };

        // Quality indicator
        const qualityEl = document.getElementById('forageQuality');
        qualityEl.textContent = `Resources look ${forageData.locationQuality}.`;

        // Clues list
        const cluesListEl = document.getElementById('forageCluesList');
        Utils.clearElement(cluesListEl);

        // Hide clues section if no clues
        const cluesSection = document.getElementById('forageClues');
        if (forageData.clues && forageData.clues.length > 0) {
            show(cluesSection);
            forageData.clues.forEach(clue => {
                const clueEl = document.createElement('div');
                clueEl.className = 'forage-clue';
                if (clue.suggestedFocusId) {
                    clueEl.classList.add('clickable');
                    clueEl.dataset.focusId = clue.suggestedFocusId;
                }

                const bulletEl = document.createElement('span');
                bulletEl.className = 'clue-bullet';
                bulletEl.textContent = '•';
                clueEl.appendChild(bulletEl);

                const descEl = document.createElement('span');
                descEl.className = 'clue-desc';
                descEl.textContent = clue.description;
                clueEl.appendChild(descEl);

                if (clue.hintText) {
                    const hintEl = document.createElement('span');
                    hintEl.className = 'clue-hint';
                    hintEl.textContent = ` ${clue.hintText}`;
                    clueEl.appendChild(hintEl);
                }

                // Click handler to select matching focus
                if (clue.suggestedFocusId) {
                    clueEl.onclick = () => this.selectForageFocus(clue.suggestedFocusId);
                }

                cluesListEl.appendChild(clueEl);
            });
        } else {
            hide(cluesSection);
        }

        // Warnings
        const warningsEl = document.getElementById('forageWarnings');
        Utils.clearElement(warningsEl);

        if (forageData.warnings && forageData.warnings.length > 0) {
            forageData.warnings.forEach(warning => {
                const warnEl = document.createElement('div');
                warnEl.className = 'forage-warning';

                const iconEl = document.createElement('span');
                iconEl.className = ICON_CLASS;
                iconEl.textContent = warning.includes('dark') ? 'dark_mode' :
                                    warning.includes('axe') ? 'carpenter' :
                                    warning.includes('shovel') ? 'agriculture' : 'info';
                warnEl.appendChild(iconEl);

                const textEl = document.createElement('span');
                textEl.textContent = warning;
                warnEl.appendChild(textEl);

                warningsEl.appendChild(warnEl);
            });
        }

        // Focus options
        const focusOptionsEl = document.getElementById('forageFocusOptions');
        Utils.clearElement(focusOptionsEl);

        forageData.focusOptions.forEach(focus => {
            const btn = document.createElement('button');
            btn.className = 'focus-btn';
            btn.dataset.focusId = focus.id;

            const labelEl = document.createElement('span');
            labelEl.className = 'focus-label';
            labelEl.textContent = focus.label;
            btn.appendChild(labelEl);

            const descEl = document.createElement('span');
            descEl.className = 'focus-desc';
            descEl.textContent = focus.description;
            btn.appendChild(descEl);

            btn.onclick = () => this.selectForageFocus(focus.id);
            focusOptionsEl.appendChild(btn);
        });

        // Time options
        const timeOptionsEl = document.getElementById('forageTimeOptions');
        Utils.clearElement(timeOptionsEl);

        forageData.timeOptions.forEach(time => {
            const btn = document.createElement('button');
            btn.className = 'time-btn';
            btn.dataset.timeId = time.id;
            btn.textContent = time.label;
            btn.onclick = () => this.selectForageTime(time.id);
            timeOptionsEl.appendChild(btn);
        });

        // Action buttons
        const inputId = this.currentInputId;

        document.getElementById('forageConfirmBtn').onclick = () => {
            if (this.forageSelection.focusId && this.forageSelection.timeId) {
                const choiceId = `${this.forageSelection.focusId}_${this.forageSelection.timeId}`;
                this.respond(choiceId, inputId);
            }
        };

        document.getElementById('forageCancelBtn').onclick = () => {
            this.respond('cancel', inputId);
        };

        this.updateForageConfirmButton();
    }

    /**
     * Select a focus option in forage popup
     */
    selectForageFocus(focusId) {
        this.forageSelection.focusId = focusId;

        // Update visual selection
        document.querySelectorAll('.focus-btn').forEach(btn => {
            btn.classList.toggle('selected', btn.dataset.focusId === focusId);
        });

        // Highlight matching clues
        document.querySelectorAll('.forage-clue').forEach(clue => {
            clue.classList.toggle('highlighted', clue.dataset.focusId === focusId);
        });

        this.updateForageConfirmButton();
    }

    /**
     * Select a time option in forage popup
     */
    selectForageTime(timeId) {
        this.forageSelection.timeId = timeId;

        // Update visual selection
        document.querySelectorAll('.time-btn').forEach(btn => {
            btn.classList.toggle('selected', btn.dataset.timeId === timeId);
        });

        this.updateForageConfirmButton();
    }

    /**
     * Update the confirm button based on selection state
     */
    updateForageConfirmButton() {
        const btn = document.getElementById('forageConfirmBtn');
        const desc = document.getElementById('forageConfirmDesc');

        if (this.forageSelection.focusId && this.forageSelection.timeId) {
            btn.disabled = false;
            const focusLabel = document.querySelector(`.focus-btn[data-focus-id="${this.forageSelection.focusId}"] .focus-label`)?.textContent || '';
            const timeLabel = document.querySelector(`.time-btn[data-time-id="${this.forageSelection.timeId}"]`)?.textContent || '';
            desc.textContent = `${focusLabel} - ${timeLabel}`;
        } else if (this.forageSelection.focusId) {
            btn.disabled = true;
            desc.textContent = 'Select time';
        } else if (this.forageSelection.timeId) {
            btn.disabled = true;
            desc.textContent = 'Select focus';
        } else {
            btn.disabled = true;
            desc.textContent = 'Select focus and time';
        }
    }

    /**
     * Hide forage popup overlay
     */
    hideForagePopup() {
        hide(document.getElementById('forageOverlay'));
        this.forageSelection = null;
    }

    /**
     * Show hunt popup overlay
     */
    showHuntPopup(huntData) {
        const overlay = document.getElementById('huntOverlay');
        if (!overlay) return;

        show(overlay);

        // Animal info
        document.getElementById('huntAnimalName').textContent = huntData.animalName;
        document.getElementById('huntAnimalDesc').textContent = huntData.animalDescription || '';

        // Distance bar with animation
        this.updateHuntDistanceBar(huntData);

        // Status
        document.getElementById('huntActivity').textContent = huntData.animalActivity || '';

        // Time with animation
        const timeEl = document.getElementById('huntTime');
        const newTime = huntData.minutesSpent;
        if (huntData.isAnimatingDistance && this.lastHuntTime < newTime) {
            this.animateTimeValue(this.lastHuntTime, newTime, timeEl);
        } else {
            timeEl.textContent = newTime + ' min';
        }
        this.lastHuntTime = newTime;

        this.updateHuntStateDisplay(huntData.animalState);

        // Message
        const messageEl = document.getElementById('huntMessage');
        if (huntData.statusMessage) {
            messageEl.textContent = huntData.statusMessage;
            show(messageEl);
        } else {
            hide(messageEl);
        }

        // Check if outcome phase
        if (huntData.outcome) {
            this.showHuntOutcome(huntData);
        } else {
            this.showHuntChoices(huntData);
        }
    }

    /**
     * Update distance bar with animation
     * Uses a mask that covers the unfilled portion from the right
     */
    updateHuntDistanceBar(huntData) {
        const mask = document.getElementById('huntDistanceMask');
        const valueEl = document.getElementById('huntDistanceValue');

        // Mask covers right portion: 100m = 100% mask (all covered), 0m = 0% mask (all revealed)
        const maxDistance = 100;
        const targetPct = Math.max(0, Math.min(100, huntData.currentDistanceMeters / maxDistance * 100));

        valueEl.textContent = `${Math.round(huntData.currentDistanceMeters)}m`;

        if (huntData.isAnimatingDistance && huntData.previousDistanceMeters != null) {
            // Animate from previous to current
            const startPct = Math.max(0, Math.min(100, huntData.previousDistanceMeters / maxDistance * 100));
            mask.style.transition = 'none';
            mask.style.width = startPct + '%';

            // Force reflow to ensure initial state is painted
            mask.offsetHeight;

            // Trigger animation (double rAF ensures browser has painted)
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    mask.style.transition = 'width 0.8s ease-out';
                    mask.style.width = targetPct + '%';
                });
            });

            // Animate the text value too
            this.animateDistanceValue(
                huntData.previousDistanceMeters,
                huntData.currentDistanceMeters,
                valueEl
            );
        } else {
            mask.style.transition = 'none';
            mask.style.width = targetPct + '%';
        }
    }

    /**
     * Animate distance value text
     */
    animateDistanceValue(fromDistance, toDistance, element) {
        const duration = 800;
        const startTime = Date.now();

        const animate = () => {
            const elapsed = Date.now() - startTime;
            const progress = Math.min(1, elapsed / duration);
            const eased = 1 - Math.pow(1 - progress, 3); // ease-out cubic

            const current = fromDistance + (toDistance - fromDistance) * eased;
            element.textContent = `${Math.round(current)}m`;

            if (progress < 1) {
                requestAnimationFrame(animate);
            }
        };

        requestAnimationFrame(animate);
    }

    /**
     * Animate time value text
     */
    animateTimeValue(fromTime, toTime, element) {
        const duration = 800;
        const startTime = Date.now();

        const animate = () => {
            const elapsed = Date.now() - startTime;
            const progress = Math.min(1, elapsed / duration);
            const eased = 1 - Math.pow(1 - progress, 3); // ease-out cubic

            const current = fromTime + (toTime - fromTime) * eased;
            element.textContent = `${Math.round(current)} min`;

            if (progress < 1) {
                requestAnimationFrame(animate);
            }
        };

        requestAnimationFrame(animate);
    }

    /**
     * Update animal state visual indicator
     */
    updateHuntStateDisplay(state) {
        const stateEl = document.getElementById('huntState');
        if (!stateEl) return;

        stateEl.className = 'hunt-state ' + (state || 'idle').toLowerCase();

        const stateText = {
            'idle': 'unaware',
            'alert': 'alert!',
            'detected': 'spotted you!'
        };

        stateEl.textContent = stateText[(state || 'idle').toLowerCase()] || state;
    }

    /**
     * Show hunt choices (during hunt)
     */
    showHuntChoices(huntData) {
        const choicesEl = document.getElementById('huntChoices');
        const outcomeEl = document.getElementById('huntOutcome');

        hide(outcomeEl);
        show(choicesEl);

        Utils.clearElement(choicesEl);
        const inputId = this.currentInputId;

        huntData.choices.forEach(choice => {
            const btn = document.createElement('button');
            btn.className = 'event-choice-btn';
            btn.disabled = !choice.isAvailable;

            const label = document.createElement('span');
            label.className = 'choice-label';
            label.textContent = choice.label;
            btn.appendChild(label);

            if (choice.description) {
                const desc = document.createElement('span');
                desc.className = 'choice-desc';
                desc.textContent = choice.description;
                btn.appendChild(desc);
            }

            if (!choice.isAvailable && choice.disabledReason) {
                btn.title = choice.disabledReason;
            }

            btn.onclick = () => this.respond(choice.id, inputId);
            choicesEl.appendChild(btn);
        });
    }

    /**
     * Show hunt outcome
     */
    showHuntOutcome(huntData) {
        const choicesEl = document.getElementById('huntChoices');
        const outcomeEl = document.getElementById('huntOutcome');
        const messageEl = document.getElementById('huntOutcomeMessage');
        const summaryEl = document.getElementById('huntOutcomeSummary');

        hide(choicesEl);
        show(outcomeEl);

        const outcome = huntData.outcome;
        messageEl.textContent = outcome.message;

        Utils.clearElement(summaryEl);

        // Time spent
        if (outcome.totalMinutesSpent > 0) {
            this.addOutcomeItem(summaryEl, 'schedule',
                `${outcome.totalMinutesSpent} minutes`, 'time');
        }

        // Items gained
        if (outcome.itemsGained && outcome.itemsGained.length > 0) {
            outcome.itemsGained.forEach(item => {
                this.addOutcomeItem(summaryEl, 'add', item, 'gain');
            });
        }

        // Effects
        if (outcome.effectsApplied && outcome.effectsApplied.length > 0) {
            outcome.effectsApplied.forEach(effect => {
                this.addOutcomeItem(summaryEl, 'warning', effect, 'effect');
            });
        }

        // Continue button
        const inputId = this.currentInputId;
        const continueBtn = document.createElement('button');
        continueBtn.className = 'event-continue-btn';
        continueBtn.textContent = outcome.transitionToCombat ? 'Face It!' : 'Continue';
        continueBtn.onclick = () => this.respond('continue', inputId);
        summaryEl.appendChild(continueBtn);
    }

    /**
     * Hide hunt popup
     */
    hideHuntPopup() {
        hide(document.getElementById('huntOverlay'));
        this.lastHuntTime = 0;
    }

    /**
     * Respond to hazard choice (quick vs careful)
     */
    respondHazardChoice(quickTravel) {
        if (this.awaitingResponse) return;
        this.awaitingResponse = true;

        if (this.socket.readyState === WebSocket.OPEN) {
            const inputId = this.currentInputId || 0;
            if (inputId <= 0) {
                console.warn('[respondHazardChoice] No valid inputId, ignoring');
                this.awaitingResponse = false;
                return;
            }
            this.socket.send(JSON.stringify({
                type: 'hazard_choice',
                quickTravel: quickTravel,
                choiceId: quickTravel ? 'quick' : 'careful',
                inputId: inputId
            }));
        }
    }

    /**
     * Request a special action (inventory, crafting) via persistent buttons
     */
    requestAction(action) {
        if (this.awaitingResponse) return;
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

    renderState(state) {

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

        // Location
        document.getElementById('locationName').textContent = state.locationName;
        document.getElementById('locationDesc').textContent = state.locationDescription || '';

        // Fire
        FireDisplay.render(state.fire);

        // Temperature
        TemperatureDisplay.render(state);

        // Survival stats
        SurvivalDisplay.render(state);

        // Effects & Injuries
        EffectsDisplay.render(state.effects);
        EffectsDisplay.renderInjuries(state.injuries, state.bloodPercent);

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
            this.addToolPill(pillsContainer, 'weapon', summary.weaponName);
        }

        // Cutting tools
        if (summary.cuttingToolCount > 0) {
            this.addToolPill(pillsContainer, 'cutting',
                summary.cuttingToolCount > 1
                    ? `${summary.cuttingToolCount} blades`
                    : 'Blade');
        }

        // Fire starters
        if (summary.fireStarterCount > 0) {
            this.addToolPill(pillsContainer, 'fire',
                summary.fireStarterCount > 1
                    ? `${summary.fireStarterCount} fire tools`
                    : 'Fire tool');
        }

        // Other tools
        if (summary.otherToolCount > 0) {
            this.addToolPill(pillsContainer, 'other',
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

    addToolPill(container, type, text) {
        const pill = document.createElement('span');
        pill.className = `tool-pill ${type}`;
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

    renderSegmentBar(containerId, percent, state = 'normal') {
        const container = document.getElementById(containerId);
        Utils.clearElement(container);

        // All bars now use the simple fill style
        const fill = document.createElement('div');
        fill.className = 'bar-fill';
        fill.style.width = percent + '%';
        container.appendChild(fill);
    }

    renderInput(input, statusText) {
        const actionsContainer = document.getElementById('actionsArea');
        const actionsArea = document.getElementById('actionButtons');
        const progressTextEl = document.getElementById('progressText');
        const progressIcon = document.getElementById('progressIcon');
        const progressBar = document.getElementById('progressBar');

        // Show the actions container for travel mode inputs
        show(actionsContainer);

        // Update status display (progress handled by ProgressMode/FrameQueue)
        if (statusText) {
            progressTextEl.textContent = statusText;
            progressTextEl.classList.remove('active');
            progressIcon.textContent = 'hourglass_empty';
            progressBar.style.width = '0%';
        } else {
            progressTextEl.textContent = 'Ready';
            progressTextEl.classList.remove('active');
            progressIcon.textContent = 'hourglass_empty';
            progressBar.style.width = '0%';
        }

        // Clear and render input UI
        Utils.clearElement(actionsArea);

        if (!input) return;

        // Before creating new buttons, clear stale disabled state
        document.querySelectorAll('.action-btn').forEach(btn => {
            btn.disabled = false;
        });

        // Use backend's input ID to track which button set is active
        this.currentInputId = input.inputId;
        const inputId = this.currentInputId;

        if (input.type === 'select') {
            if (input.prompt) {
                const promptDiv = document.createElement('div');
                promptDiv.className = 'action-prompt';
                promptDiv.textContent = input.prompt;
                actionsArea.appendChild(promptDiv);
            }

            // Filter out Inventory/Crafting from main buttons (they're in sidebar now)
            const hiddenActions = ['Inventory', 'Crafting'];

            console.log('[renderInput] All choices:', input.choices);

            input.choices.forEach((choice) => {
                // Skip if this is an inventory/crafting option (handled by sidebar buttons)
                if (hiddenActions.some(action => choice.label.includes(action))) {
                    console.log('[renderInput] Hiding:', choice.label);
                    return;
                }

                console.log('[renderInput] Adding button:', choice.label);
                const btn = document.createElement('button');
                btn.className = 'action-btn';
                btn.textContent = choice.label;
                btn.onclick = () => this.respond(choice.id, inputId);
                actionsArea.appendChild(btn);
            });

        } else if (input.type === 'confirm') {
            if (input.prompt) {
                const promptDiv = document.createElement('div');
                promptDiv.className = 'action-prompt';
                promptDiv.textContent = input.prompt;
                actionsArea.appendChild(promptDiv);
            }

            // Confirm sends choices with IDs
            input.choices.forEach((choice) => {
                const btn = document.createElement('button');
                btn.className = 'action-btn';
                btn.textContent = choice.label;
                btn.onclick = () => this.respond(choice.id, inputId);
                actionsArea.appendChild(btn);
            });

        } else if (input.type === 'anykey') {
            const btn = document.createElement('button');
            btn.className = 'action-btn';
            btn.textContent = input.prompt || 'Continue';
            btn.onclick = () => this.respond('continue', inputId);
            actionsArea.appendChild(btn);

        } else if (input.type === 'grid') {
            // Grid travel mode - show stop button
            const stopBtn = document.createElement('button');
            stopBtn.className = 'action-btn';
            stopBtn.textContent = 'Stop';
            stopBtn.onclick = () => this.respondStopTravel();
            actionsArea.appendChild(stopBtn);
        }
    }

    respondStopTravel() {
        if (this.awaitingResponse) return;
        this.awaitingResponse = true;

        if (this.socket.readyState === WebSocket.OPEN) {
            const inputId = this.currentInputId || 0;
            if (inputId <= 0) {
                console.warn('[respondStopTravel] No valid inputId, ignoring');
                this.awaitingResponse = false;
                return;
            }
            this.socket.send(JSON.stringify({
                type: 'menu',
                inputId: inputId
            }));
        }
    }

    respond(choiceId, inputId) {
        // Prevent duplicate responses or responses to stale buttons
        if (this.awaitingResponse) {
            return;
        }

        // Reject empty string choiceId - this indicates a bug in button creation
        if (choiceId === '') {
            console.error('[respond] Empty choiceId rejected! Stack:', new Error().stack);
            return;
        }

        // Verify this click is for the current input set (prevents stale button clicks)
        if (inputId !== undefined && inputId !== this.currentInputId) {
            return;
        }

        // Use current input ID if parameter is invalid
        const validInputId = (inputId !== undefined && inputId > 0) ? inputId : this.currentInputId;

        // Don't send if we don't have a valid input ID yet
        if (!validInputId || validInputId <= 0) {
            console.warn('[respond] No valid inputId available, ignoring click');
            this.awaitingResponse = false;
            return;
        }

        this.awaitingResponse = true;

        // Disable all action buttons immediately to prevent double-clicks
        document.querySelectorAll('.action-btn').forEach(btn => {
            btn.disabled = true;
        });

        if (this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify({ choiceId, inputId: validInputId }));
        }
    }

    showInventory(inv, input) {
        const overlay = document.getElementById('inventoryOverlay');
        show(overlay);

        document.getElementById('inventoryTitle').textContent = inv.title;
        document.getElementById('inventoryWeight').textContent =
            `${inv.currentWeightKg.toFixed(1)} / ${inv.maxWeightKg.toFixed(0)} kg`;

        // Render each section
        this.renderInvGear(inv);
        this.renderInvFuel(inv);
        this.renderInvFood(inv);
        this.renderInvWater(inv);
        this.renderInvMaterials(inv);
        this.renderInvMedicinals(inv);

        // Track input ID for inventory buttons
        const inputId = this.currentInputId;

        // Render action buttons
        const actionsContainer = document.getElementById('inventoryActions');
        Utils.clearElement(actionsContainer);

        if (input && input.type === 'select' && input.choices) {
            input.choices.forEach((choice) => {
                const btn = document.createElement('button');
                btn.className = 'action-btn';
                btn.textContent = choice.label;
                btn.onclick = () => this.respond(choice.id, inputId);
                actionsContainer.appendChild(btn);
            });
        } else {
            const closeBtn = document.createElement('button');
            closeBtn.className = 'action-btn';
            closeBtn.textContent = 'Close';
            closeBtn.onclick = () => this.respond('continue', inputId);
            actionsContainer.appendChild(closeBtn);
        }
    }

    createSlotElement(label, item, stat, statClass = '') {
        const slot = document.createElement('div');
        slot.className = 'inv-slot';

        const labelSpan = document.createElement('span');
        labelSpan.className = 'slot-label';
        labelSpan.textContent = label;
        slot.appendChild(labelSpan);

        if (item) {
            const itemSpan = document.createElement('span');
            itemSpan.className = 'slot-item';
            itemSpan.textContent = item;
            slot.appendChild(itemSpan);

            if (stat) {
                const statSpan = document.createElement('span');
                statSpan.className = 'slot-stat' + (statClass ? ' ' + statClass : '');
                statSpan.textContent = stat;
                slot.appendChild(statSpan);
            }
        } else {
            const emptySpan = document.createElement('span');
            emptySpan.className = 'slot-empty';
            emptySpan.textContent = '-';
            slot.appendChild(emptySpan);
        }

        return slot;
    }

    renderInvGear(inv) {
        const content = document.querySelector('#invGear .inv-content');
        Utils.clearElement(content);

        // Weapon slot (always show)
        const weaponSlot = this.createSlotElement(
            'Weapon',
            inv.weapon,
            inv.weapon ? `${inv.weaponDamage?.toFixed(0) || 0} dmg` : null
        );
        weaponSlot.classList.add('weapon');
        content.appendChild(weaponSlot);

        // Armor slots (always show all 5)
        const armorSlots = ['Head', 'Chest', 'Hands', 'Legs', 'Feet'];
        armorSlots.forEach(slotName => {
            const equipped = inv.armor?.find(a => a.slot === slotName);
            const slot = this.createSlotElement(
                slotName,
                equipped?.name,
                equipped ? `+${(equipped.insulation * 100).toFixed(0)}%` : null,
                'insulation'
            );
            slot.classList.add('armor');
            content.appendChild(slot);
        });

        // Tools list
        if (inv.tools && inv.tools.length > 0) {
            const toolsDiv = document.createElement('div');
            toolsDiv.className = 'inv-tools';

            inv.tools.forEach(t => {
                const toolEl = document.createElement('div');
                toolEl.className = 'inv-tool';

                let nameText = t.name;
                if (t.damage) {
                    nameText += ` (${t.damage.toFixed(0)} dmg)`;
                }

                const nameSpan = document.createElement('span');
                nameSpan.className = 'tool-name';
                nameSpan.textContent = nameText;
                toolEl.appendChild(nameSpan);

                // Show durability warning if provided
                if (inv.toolWarnings) {
                    const warning = inv.toolWarnings.find(w => w.name === t.name);
                    if (warning) {
                        const warnSpan = document.createElement('span');
                        warnSpan.className = 'tool-warning';
                        warnSpan.textContent = `${warning.durabilityRemaining} uses left`;
                        toolEl.appendChild(warnSpan);
                        toolEl.classList.add('durability-low');
                    }
                }

                toolsDiv.appendChild(toolEl);
            });

            content.appendChild(toolsDiv);
        }

        // Total insulation summary
        if (inv.totalInsulation > 0) {
            const totalDiv = document.createElement('div');
            totalDiv.className = 'inv-total';

            const labelSpan = document.createElement('span');
            labelSpan.textContent = 'Total Insulation';
            totalDiv.appendChild(labelSpan);

            const valueSpan = document.createElement('span');
            valueSpan.className = 'total-value';
            valueSpan.textContent = `+${(inv.totalInsulation * 100).toFixed(0)}%`;
            totalDiv.appendChild(valueSpan);

            content.appendChild(totalDiv);
        }
    }

    renderInvFuel(inv) {
        const content = document.querySelector('#invFuel .inv-content');
        Utils.clearElement(content);

        // Iterate over fuel items from backend
        if (inv.fuel && inv.fuel.length > 0) {
            for (const item of inv.fuel) {
                const weightStr = item.weightKg >= 1 ? `${item.weightKg.toFixed(1)}kg` : `${item.weightKg.toFixed(2)}kg`;
                this.addInvItem(content, `${item.count} ${item.displayName}`, weightStr, item.cssClass || '');
            }
            this.addInvItem(content, 'Burn time', `~${inv.fuelBurnTimeHours.toFixed(1)} hrs`, 'summary');
        } else {
            this.addNoneItem(content);
        }
    }

    renderInvFood(inv) {
        const content = document.querySelector('#invFood .inv-content');
        Utils.clearElement(content);

        // Iterate over food items from backend
        if (inv.food && inv.food.length > 0) {
            for (const item of inv.food) {
                const weightStr = item.weightKg >= 1 ? `${item.weightKg.toFixed(1)}kg` : `${item.weightKg.toFixed(2)}kg`;
                this.addInvItem(content, `${item.count} ${item.displayName}`, weightStr, item.cssClass || '');
            }
        } else {
            this.addNoneItem(content);
        }
    }

    renderInvWater(inv) {
        const content = document.querySelector('#invWater .inv-content');
        Utils.clearElement(content);

        if (inv.waterLiters > 0) {
            this.addInvItem(content, 'Clean water', `${inv.waterLiters.toFixed(1)}L`);
        } else {
            this.addNoneItem(content);
        }
    }

    renderInvMaterials(inv) {
        const content = document.querySelector('#invMaterials .inv-content');
        Utils.clearElement(content);

        // Iterate over material items from backend
        if (inv.materials && inv.materials.length > 0) {
            for (const item of inv.materials) {
                const weightStr = item.weightKg > 0
                    ? (item.weightKg >= 1 ? `${item.weightKg.toFixed(1)}kg` : `${item.weightKg.toFixed(2)}kg`)
                    : '';
                this.addInvItem(content, `${item.count} ${item.displayName}`, weightStr, item.cssClass || '');
            }
        } else {
            this.addNoneItem(content);
        }
    }

    renderInvMedicinals(inv) {
        const content = document.querySelector('#invMedicinals .inv-content');
        Utils.clearElement(content);

        // Iterate over medicinal items from backend
        if (inv.medicinals && inv.medicinals.length > 0) {
            for (const item of inv.medicinals) {
                this.addInvItem(content, `${item.count} ${item.displayName}`, '', item.cssClass || '');
            }
        } else {
            this.addNoneItem(content);
        }
    }

    hideInventory() {
        hide(document.getElementById('inventoryOverlay'));
    }

    showCrafting(crafting, input) {
        const overlay = document.getElementById('craftingOverlay');
        show(overlay);

        document.getElementById('craftingTitle').textContent = crafting.title;

        // Track input ID for crafting buttons
        const inputId = this.currentInputId;

        // Debug: log the crafting data
        console.log('Crafting data:', crafting);

        const categoriesContainer = document.getElementById('craftingCategories');
        Utils.clearElement(categoriesContainer);

        // Render each category
        crafting.categories.forEach(category => {
            console.log(`Category: ${category.categoryName}`);
            console.log(`  Craftable: ${category.craftableRecipes?.length || 0}`);
            console.log(`  Uncraftable: ${category.uncraftableRecipes?.length || 0}`);

            const categoryDiv = document.createElement('div');
            categoryDiv.className = 'crafting-category';

            const categoryHeader = document.createElement('h3');
            categoryHeader.className = 'category-header';
            const icon = document.createElement('span');
            icon.className = ICON_CLASS;
            icon.textContent = 'construction';
            categoryHeader.appendChild(icon);
            categoryHeader.appendChild(document.createTextNode(category.categoryName));
            categoryDiv.appendChild(categoryHeader);

            // Craftable recipes
            if (category.craftableRecipes && category.craftableRecipes.length > 0) {
                category.craftableRecipes.forEach((recipe, index) => {
                    const recipeRow = this.createRecipeRow(recipe, true, input, inputId);
                    categoryDiv.appendChild(recipeRow);
                });
            }

            // Uncraftable recipes
            if (category.uncraftableRecipes && category.uncraftableRecipes.length > 0) {
                const uncraftableHeader = document.createElement('div');
                uncraftableHeader.className = 'uncraftable-header';
                uncraftableHeader.textContent = 'Needs materials:';
                categoryDiv.appendChild(uncraftableHeader);

                category.uncraftableRecipes.forEach(recipe => {
                    const recipeRow = this.createRecipeRow(recipe, false, input, inputId);
                    categoryDiv.appendChild(recipeRow);
                });
            }

            categoriesContainer.appendChild(categoryDiv);
        });

        // Handle close button
        document.getElementById('craftingCloseBtn').onclick = () => {
            if (input && input.type === 'select') {
                // Find the Cancel choice by label and use its ID
                const cancelChoice = input.choices.find(c => c.label === 'Cancel');
                if (cancelChoice) {
                    this.respond(cancelChoice.id, inputId);
                } else {
                    this.respond('continue', inputId);
                }
            } else {
                this.respond('continue', inputId);
            }
        };
    }

    createRecipeRow(recipe, isCraftable, input, inputId) {
        const row = document.createElement('div');
        row.className = `recipe-row ${isCraftable ? 'craftable' : 'uncraftable'}`;

        const info = document.createElement('div');
        info.className = 'recipe-info';

        const name = document.createElement('div');
        name.className = 'recipe-name';
        name.textContent = recipe.name;
        info.appendChild(name);

        const requirements = document.createElement('div');
        requirements.className = 'recipe-requirements';

        recipe.requirements.forEach((req, i) => {
            const reqSpan = document.createElement('span');
            reqSpan.className = `requirement ${req.isMet ? 'met' : 'unmet'}`;
            reqSpan.textContent = `${req.materialName}: ${req.available}/${req.required}`;
            requirements.appendChild(reqSpan);

            if (i < recipe.requirements.length - 1) {
                requirements.appendChild(document.createTextNode(', '));
            }
        });
        info.appendChild(requirements);

        // Tool requirements (if any)
        if (recipe.toolRequirements && recipe.toolRequirements.length > 0) {
            const toolReqs = document.createElement('div');
            toolReqs.className = 'recipe-tool-requirements';

            recipe.toolRequirements.forEach((tool, i) => {
                const toolSpan = document.createElement('span');

                const icon = document.createElement('span');
                icon.className = ICON_CLASS;

                if (!tool.isAvailable) {
                    toolSpan.className = 'tool-requirement missing';
                    icon.textContent = 'close';
                    toolSpan.appendChild(icon);
                    toolSpan.appendChild(document.createTextNode(`${tool.toolName} (required)`));
                } else if (tool.isBroken) {
                    toolSpan.className = 'tool-requirement broken';
                    icon.textContent = 'close';
                    toolSpan.appendChild(icon);
                    toolSpan.appendChild(document.createTextNode(`${tool.toolName} (broken)`));
                } else {
                    toolSpan.className = 'tool-requirement available';
                    icon.textContent = 'check';
                    toolSpan.appendChild(icon);
                    toolSpan.appendChild(document.createTextNode(`${tool.toolName} (${tool.durability} uses left)`));
                }

                toolReqs.appendChild(toolSpan);

                if (i < recipe.toolRequirements.length - 1) {
                    toolReqs.appendChild(document.createTextNode(', '));
                }
            });

            info.appendChild(toolReqs);
        }

        const time = document.createElement('div');
        time.className = 'recipe-time';
        time.textContent = recipe.craftingTimeDisplay;
        info.appendChild(time);

        row.appendChild(info);

        // Add CRAFT button if craftable
        if (isCraftable && input && input.type === 'select') {
            const craftBtn = document.createElement('button');
            craftBtn.className = 'craft-btn';
            craftBtn.textContent = 'CRAFT';

            // Find the matching choice by recipe name
            const matchingChoice = input.choices.find(choice =>
                choice.label.includes(recipe.name)
            );

            if (matchingChoice) {
                craftBtn.onclick = () => this.respond(matchingChoice.id, inputId);
            } else {
                craftBtn.disabled = true;
            }

            row.appendChild(craftBtn);
        }

        return row;
    }

    hideCrafting() {
        hide(document.getElementById('craftingOverlay'));
    }

    addInvItem(container, label, value, styleClass = '') {
        const div = document.createElement('div');
        div.className = 'inv-item' + (styleClass ? ' ' + styleClass : '');
        const labelSpan = document.createElement('span');
        labelSpan.textContent = label;
        const valueSpan = document.createElement('span');
        valueSpan.className = 'qty';
        valueSpan.textContent = value;
        div.appendChild(labelSpan);
        div.appendChild(valueSpan);
        container.appendChild(div);
    }

    addNoneItem(container) {
        const div = document.createElement('div');
        div.className = 'inv-none';
        div.textContent = 'None';
        container.appendChild(div);
    }
}

// Start the client
new GameClient();
