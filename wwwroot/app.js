import { ConnectionOverlay } from './modules/connection.js';
import { Utils } from './modules/utils.js';
import { ProgressDisplay } from './modules/progress.js';
import { FrameQueue } from './modules/frameQueue.js';
import { NarrativeLog } from './modules/log.js';
import { TemperatureDisplay } from './modules/temperature.js';
import { FireDisplay } from './modules/fire.js';
import { SurvivalDisplay } from './modules/survival.js';
import { EffectsDisplay } from './modules/effects.js';
import { getGridRenderer } from './modules/grid/CanvasGridRenderer.js';

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

        if (!choices || choices.length === 0) {
            // No choices available - disable both
            if (inventoryBtn) inventoryBtn.disabled = true;
            if (craftingBtn) craftingBtn.disabled = true;
            return;
        }

        // Check if Inventory/Crafting are in the available choices
        const hasInventory = choices.some(c => c.label.includes('Inventory'));
        const hasCrafting = choices.some(c => c.label.includes('Crafting'));

        if (inventoryBtn) inventoryBtn.disabled = !hasInventory;
        if (craftingBtn) craftingBtn.disabled = !hasCrafting;
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
        console.log('[GameClient] Frame received:', {
            mode: frame.mode?.type,
            overlays: frame.overlays?.map(o => o.type),
            inputType: frame.input?.type
        });
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
    }

    /**
     * Show event popup overlay
     */
    showEventPopup(eventData) {
        const overlay = document.getElementById('eventOverlay');
        if (!overlay) return;

        overlay.classList.remove('hidden');

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

        // Capture input ID for this set of buttons
        this.currentInputId++;
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
            if (descEl) descEl.classList.add('hidden');
            if (choicesEl) choicesEl.classList.add('hidden');

            // Show and animate progress bar
            progressEl.classList.remove('hidden');
            progressText.textContent = `Acting... (+${outcome.timeAddedMinutes} min)`;
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
                        progressEl.classList.add('hidden');
                        this.showOutcomeContent(eventData, descEl, choicesEl);
                    }, 150);
                }
            };

            requestAnimationFrame(animateProgress);
        } else {
            // No time added - show outcome immediately
            progressEl.classList.add('hidden');
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
            descEl.classList.remove('hidden');
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
            choicesEl.classList.remove('hidden');
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
            this.currentInputId++;
            const inputId = this.currentInputId;

            const continueBtn = document.createElement('button');
            continueBtn.className = 'event-continue-btn';
            continueBtn.textContent = 'Continue';
            continueBtn.onclick = () => this.respond(null, inputId);
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
        iconEl.className = 'material-symbols-outlined';
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
        if (overlay) overlay.classList.add('hidden');

        // Reset progress bar state
        const progressEl = document.getElementById('eventProgress');
        const progressBar = document.getElementById('eventProgressBar');
        if (progressEl) progressEl.classList.add('hidden');
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
        const envEl = document.getElementById('popupEnvironment');
        const hazardsEl = document.getElementById('popupHazards');
        const tacticalEl = document.getElementById('popupTactical');
        const featuresEl = document.getElementById('popupFeatures');
        const actionsEl = document.getElementById('popupActions');

        // Set location info
        nameEl.textContent = tileData.locationName || tileData.terrain;
        terrainEl.textContent = tileData.locationName ? tileData.terrain : '';

        // Clear all sections
        Utils.clearElement(envEl);
        Utils.clearElement(hazardsEl);
        Utils.clearElement(tacticalEl);
        Utils.clearElement(featuresEl);

        // Build environment section (for explored locations)
        const isExplored = tileData.visibility === 'visible' && tileData.locationName && tileData.locationName !== '???';
        if (isExplored) {
            this.buildEnvironmentSection(envEl, tileData);
            this.buildHazardsSection(hazardsEl, tileData);
            this.buildTacticalSection(tacticalEl, tileData);
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
                iconEl.className = 'material-symbols-outlined';
                iconEl.textContent = icon;
                featureEl.appendChild(iconEl);

                const labelEl = document.createElement('span');
                labelEl.textContent = this.getIconLabel(icon);
                featureEl.appendChild(labelEl);

                featuresEl.appendChild(featureEl);
            });
        }

        // Build actions
        Utils.clearElement(actionsEl);

        const isPlayerHere = tileData.isPlayerHere;
        const canTravel = tileData.isAdjacent && tileData.isPassable && !isPlayerHere;

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
            this.currentInputId++;
            const inputId = this.currentInputId;
            const hiddenActions = ['Inventory', 'Crafting', 'Travel'];

            this.currentInput.choices.forEach((choice) => {
                // Skip actions handled elsewhere (sidebar buttons, grid clicks)
                if (hiddenActions.some(action => choice.label.includes(action))) return;

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

        // Show examine buttons for environmental details on current tile
        if (isPlayerHere && tileData.details && tileData.details.length > 0) {
            tileData.details.forEach(detail => {
                const btn = document.createElement('button');
                btn.className = 'popup-action-btn';
                btn.textContent = detail.hint ? `${detail.displayName} (${detail.hint})` : detail.displayName;
                btn.onclick = (e) => {
                    e.stopPropagation();
                    this.handleExamineRequest(detail.id);
                };
                actionsEl.appendChild(btn);
            });
        }

        // Position popup horizontally
        popup.style.left = `${screenPos.x + 8}px`;

        // Show popup to measure its height
        popup.classList.remove('hidden');
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
     */
    getIconLabel(icon) {
        const labels = {
            'local_fire_department': 'Active fire',
            'fireplace': 'Embers (relight possible)',
            'eco': 'Foraging area',
            'nutrition': 'Harvestable resources',
            'cruelty_free': 'Wildlife territory',
            'pets': 'Predator territory',
            'water_drop': 'Water source',
            'park': 'Wooded area',
            'cabin': 'Shelter',
            'ac_unit': 'Snow shelter',
            'bed': 'Bedding',
            'inventory_2': 'Storage cache',
            'circle': 'Snares set',
            'catching_pokemon': 'Snare catch ready!',
            'search': 'Salvage site',
            'timelapse': 'Curing in progress',
            'done_all': 'Curing complete!',
            'construction': 'Construction project',
            // Environmental details
            'footprint': 'Animal tracks',
            'scatter_plot': 'Animal droppings',
            'call_split': 'Bent branches',
            'forest': 'Fallen log',
            'nature': 'Hollow tree',
            'skeleton': 'Scattered bones',
            'landscape': 'Stone pile'
        };
        return labels[icon] || icon;
    }

    /**
     * Build environment section showing wind, cover, temperature
     */
    buildEnvironmentSection(container, tileData) {
        // Wind factor: 0-0.7 = sheltered, 0.7-1.3 = normal (skip), 1.3+ = exposed
        if (tileData.windFactor != null && tileData.windFactor < 0.7) {
            this.addPopupItem(container, 'air', 'Sheltered', 'wind');
        } else if (tileData.windFactor != null && tileData.windFactor > 1.3) {
            this.addPopupItem(container, 'air', 'Exposed', 'hazard');
        }

        // Overhead cover: only show if significant
        if (tileData.overheadCoverLevel != null && tileData.overheadCoverLevel > 0.2) {
            const pct = Math.round(tileData.overheadCoverLevel * 100);
            this.addPopupItem(container, 'roofing', `${pct}% cover`, 'neutral');
        }

        // Temperature modifier: only show if notable
        if (tileData.temperatureDeltaF != null && Math.abs(tileData.temperatureDeltaF) > 3) {
            const sign = tileData.temperatureDeltaF > 0 ? '+' : '';
            const icon = tileData.temperatureDeltaF > 0 ? 'sunny' : 'ac_unit';
            const cls = tileData.temperatureDeltaF > 0 ? 'warm' : 'cold';
            this.addPopupItem(container, icon, `${sign}${Math.round(tileData.temperatureDeltaF)}°F`, cls);
        }
    }

    /**
     * Build hazards section showing terrain danger and climb risk
     */
    buildHazardsSection(container, tileData) {
        // Terrain hazard
        if (tileData.terrainHazardLevel != null && tileData.terrainHazardLevel > 0.1) {
            const label = tileData.terrainHazardLevel > 0.5 ? 'Treacherous' :
                         tileData.terrainHazardLevel > 0.2 ? 'Hazardous' : 'Minor hazards';
            this.addPopupItem(container, 'warning', label, 'hazard');
        }

        // Climb risk
        if (tileData.climbRiskFactor != null && tileData.climbRiskFactor > 0.1) {
            const label = tileData.climbRiskFactor > 0.5 ? 'Technical climbing' :
                         tileData.climbRiskFactor > 0.2 ? 'Scrambling required' : 'Some climbing';
            this.addPopupItem(container, 'hiking', label, 'hazard');
        }

        // Darkness
        if (tileData.isDark) {
            this.addPopupItem(container, 'dark_mode', 'Requires light', 'hazard');
        }
    }

    /**
     * Build tactical section showing escape terrain, vantage points, visibility
     */
    buildTacticalSection(container, tileData) {
        if (tileData.isEscapeTerrain) {
            this.addPopupItem(container, 'sprint', 'Escape terrain', 'tactical-good');
        }

        if (tileData.isVantagePoint) {
            this.addPopupItem(container, 'visibility', 'Vantage point', 'tactical-good');
        }

        // Visibility factor: only show if notably different
        if (tileData.visibilityFactor != null && tileData.visibilityFactor < 0.7) {
            this.addPopupItem(container, 'visibility_off', 'Limited sight', 'neutral');
        } else if (tileData.visibilityFactor != null && tileData.visibilityFactor > 1.3) {
            this.addPopupItem(container, 'preview', 'Wide view', 'tactical-good');
        }
    }

    /**
     * Build detailed feature cards
     */
    buildDetailedFeatures(container, featureDetails) {
        const featureIcons = {
            'shelter': 'cabin',
            'forage': 'eco',
            'animal': 'cruelty_free',
            'cache': 'inventory_2',
            'water': 'water_drop',
            'wood': 'park',
            'snares': 'circle',
            'curing': 'timelapse'
        };

        featureDetails.forEach(feature => {
            const featureEl = document.createElement('div');
            featureEl.className = 'popup-feature-detailed';

            const iconEl = document.createElement('span');
            iconEl.className = 'material-symbols-outlined';
            iconEl.textContent = featureIcons[feature.type] || 'info';
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
        iconEl.className = 'material-symbols-outlined';
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
        popup.classList.add('hidden');
        this.tilePopup = null;
    }

    /**
     * Update just the action buttons in an already-visible tile popup
     */
    updateTilePopupActions() {
        if (!this.tilePopup) return;

        const actionsEl = document.getElementById('popupActions');
        if (!actionsEl) return;

        Utils.clearElement(actionsEl);

        // Only show actions if player is at this tile
        if (!this.tilePopup.tileData?.isPlayerHere) return;
        if (!this.currentInput?.choices) return;

        this.currentInputId++;
        const inputId = this.currentInputId;
        const hiddenActions = ['Inventory', 'Crafting'];

        this.currentInput.choices.forEach((choice) => {
            if (hiddenActions.some(action => choice.label.includes(action))) return;

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
            this.socket.send(JSON.stringify({
                type: 'travel_to',
                targetX: x,
                targetY: y
            }));
        }
    }

    /**
     * Send examine request for environmental detail
     */
    handleExamineRequest(detailId) {
        if (this.awaitingResponse) return;
        this.awaitingResponse = true;

        if (this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify({
                type: 'examine',
                detailId: detailId
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

        overlay.classList.remove('hidden');
    }

    /**
     * Hide hazard prompt overlay
     */
    hideHazardPrompt() {
        document.getElementById('hazardOverlay').classList.add('hidden');
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
        this.currentInputId++;
        const inputId = this.currentInputId;

        // Create Yes/No buttons from input choices
        if (input?.choices) {
            input.choices.forEach(choice => {
                const btn = document.createElement('button');
                btn.className = 'event-choice-btn';
                btn.textContent = choice.label;
                btn.onclick = () => {
                    this.hideConfirmPrompt();
                    this.respond(choice.id, inputId);
                };
                choicesEl.appendChild(btn);
            });
        }

        overlay.classList.remove('hidden');
    }

    /**
     * Hide confirm prompt overlay
     */
    hideConfirmPrompt() {
        const overlay = document.getElementById('confirmOverlay');
        if (overlay) overlay.classList.add('hidden');
    }

    /**
     * Respond to hazard choice (quick vs careful)
     */
    respondHazardChoice(quickTravel) {
        if (this.awaitingResponse) return;
        this.awaitingResponse = true;

        if (this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify({
                type: 'hazard_choice',
                quickTravel: quickTravel,
                choiceId: quickTravel ? 'quick' : 'careful'
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
            this.socket.send(JSON.stringify({
                type: 'action',
                action: action
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
        document.getElementById('weatherIcon').textContent = this.getWeatherIcon(state.weatherCondition);
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
                storageRow.classList.remove('hidden');
            } else {
                storageRow.classList.add('hidden');
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

    getWeatherIcon(condition) {
        const icons = {
            'Clear': 'wb_sunny',
            'Cloudy': 'cloud',
            'Overcast': 'cloud',
            'Light Snow': 'weather_snowy',
            'Snow': 'weather_snowy',
            'Heavy Snow': 'ac_unit',
            'Blizzard': 'ac_unit',
            'Fog': 'foggy',
            'Wind': 'air'
        };
        return icons[condition] || 'partly_cloudy_day';
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
        actionsContainer?.classList.remove('hidden');

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

        // Increment input ID to track which button set is active
        this.currentInputId++;
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
            btn.onclick = () => this.respond(null, inputId);
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
            this.socket.send(JSON.stringify({
                type: 'menu'
            }));
        }
    }

    respond(choiceId, inputId) {
        // Prevent duplicate responses or responses to stale buttons
        if (this.awaitingResponse) {
            return;
        }

        // Verify this click is for the current input set (prevents stale button clicks)
        if (inputId !== undefined && inputId !== this.currentInputId) {
            return;
        }

        this.awaitingResponse = true;

        // Disable all action buttons immediately to prevent double-clicks
        document.querySelectorAll('.action-btn').forEach(btn => {
            btn.disabled = true;
        });

        if (this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify({ choiceId }));
        }
    }

    showInventory(inv, input) {
        const overlay = document.getElementById('inventoryOverlay');
        overlay.classList.remove('hidden');

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

        // Increment input ID for inventory buttons
        this.currentInputId++;
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
            closeBtn.onclick = () => this.respond(null, inputId);
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

        // Generic fuel
        if (inv.logCount > 0)
            this.addInvItem(content, `${inv.logCount} logs`, `${inv.logsKg.toFixed(1)}kg`);
        if (inv.stickCount > 0)
            this.addInvItem(content, `${inv.stickCount} sticks`, `${inv.sticksKg.toFixed(1)}kg`);
        if (inv.tinderCount > 0)
            this.addInvItem(content, `${inv.tinderCount} tinder`, `${inv.tinderKg.toFixed(2)}kg`);

        // Wood types
        if (inv.pineCount > 0)
            this.addInvItem(content, `${inv.pineCount} pine`, `${inv.pineKg.toFixed(1)}kg`, 'wood-pine');
        if (inv.birchCount > 0)
            this.addInvItem(content, `${inv.birchCount} birch`, `${inv.birchKg.toFixed(1)}kg`, 'wood-birch');
        if (inv.oakCount > 0)
            this.addInvItem(content, `${inv.oakCount} oak`, `${inv.oakKg.toFixed(1)}kg`, 'wood-oak');
        if (inv.birchBarkCount > 0)
            this.addInvItem(content, `${inv.birchBarkCount} birch bark`, `${inv.birchBarkKg.toFixed(2)}kg`, 'tinder');

        // Burn time summary
        if (content.children.length > 0) {
            this.addInvItem(content, 'Burn time', `~${inv.fuelBurnTimeHours.toFixed(1)} hrs`, 'summary');
        } else {
            this.addNoneItem(content);
        }
    }

    renderInvFood(inv) {
        const content = document.querySelector('#invFood .inv-content');
        Utils.clearElement(content);

        // Cooked (best)
        if (inv.cookedMeatCount > 0)
            this.addInvItem(content, `${inv.cookedMeatCount} cooked meat`, `${inv.cookedMeatKg.toFixed(1)}kg`, 'food-cooked');

        // Preserved
        if (inv.driedMeatCount > 0)
            this.addInvItem(content, `${inv.driedMeatCount} dried meat`, `${inv.driedMeatKg.toFixed(1)}kg`, 'food-preserved');
        if (inv.driedBerriesCount > 0)
            this.addInvItem(content, `${inv.driedBerriesCount} dried berries`, `${inv.driedBerriesKg.toFixed(2)}kg`, 'food-preserved');

        // Raw
        if (inv.rawMeatCount > 0)
            this.addInvItem(content, `${inv.rawMeatCount} raw meat`, `${inv.rawMeatKg.toFixed(1)}kg`, 'food-raw');

        // Foraged
        if (inv.berryCount > 0)
            this.addInvItem(content, `${inv.berryCount} berries`, `${inv.berriesKg.toFixed(2)}kg`, 'food-foraged');
        if (inv.nutsCount > 0)
            this.addInvItem(content, `${inv.nutsCount} nuts`, `${inv.nutsKg.toFixed(2)}kg`, 'food-foraged');
        if (inv.rootsCount > 0)
            this.addInvItem(content, `${inv.rootsCount} roots`, `${inv.rootsKg.toFixed(2)}kg`, 'food-raw');

        if (content.children.length === 0) {
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

        // Stone types (highlight rare)
        if (inv.stoneCount > 0)
            this.addInvItem(content, `${inv.stoneCount} stone`, `${inv.stoneKg.toFixed(1)}kg`);
        if (inv.shaleCount > 0)
            this.addInvItem(content, `${inv.shaleCount} shale`, `${inv.shaleKg.toFixed(1)}kg`, 'material-stone');
        if (inv.flintCount > 0)
            this.addInvItem(content, `${inv.flintCount} flint`, `${inv.flintKg.toFixed(1)}kg`, 'material-rare');
        if (inv.pyriteKg > 0)
            this.addInvItem(content, 'Pyrite', `${inv.pyriteKg.toFixed(2)}kg`, 'material-precious');

        // Organics
        if (inv.boneCount > 0)
            this.addInvItem(content, `${inv.boneCount} bone`, `${inv.boneKg.toFixed(1)}kg`);
        if (inv.hideCount > 0)
            this.addInvItem(content, `${inv.hideCount} hide`, `${inv.hideKg.toFixed(1)}kg`);
        if (inv.plantFiberCount > 0)
            this.addInvItem(content, `${inv.plantFiberCount} plant fiber`, `${inv.plantFiberKg.toFixed(2)}kg`);
        if (inv.sinewCount > 0)
            this.addInvItem(content, `${inv.sinewCount} sinew`, `${inv.sinewKg.toFixed(2)}kg`);

        // Processed
        if (inv.scrapedHideCount > 0)
            this.addInvItem(content, `${inv.scrapedHideCount} scraped hide`, '', 'material-processed');
        if (inv.curedHideCount > 0)
            this.addInvItem(content, `${inv.curedHideCount} cured hide`, '', 'material-processed');
        if (inv.rawFiberCount > 0)
            this.addInvItem(content, `${inv.rawFiberCount} raw fiber`, '');
        if (inv.rawFatCount > 0)
            this.addInvItem(content, `${inv.rawFatCount} raw fat`, '');
        if (inv.tallowCount > 0)
            this.addInvItem(content, `${inv.tallowCount} tallow`, '', 'material-processed');
        if (inv.charcoalKg > 0)
            this.addInvItem(content, 'Charcoal', `${inv.charcoalKg.toFixed(2)}kg`);

        if (content.children.length === 0) {
            this.addNoneItem(content);
        }
    }

    renderInvMedicinals(inv) {
        const content = document.querySelector('#invMedicinals .inv-content');
        Utils.clearElement(content);

        // Fungi
        if (inv.birchPolyporeCount > 0)
            this.addInvItem(content, `${inv.birchPolyporeCount} birch polypore`, '', 'medicinal-wound');
        if (inv.chagaCount > 0)
            this.addInvItem(content, `${inv.chagaCount} chaga`, '', 'medicinal-health');
        if (inv.amadouCount > 0)
            this.addInvItem(content, `${inv.amadouCount} amadou`, '', 'medicinal-versatile');

        // Plants
        if (inv.roseHipsCount > 0)
            this.addInvItem(content, `${inv.roseHipsCount} rose hips`, '', 'medicinal-vitamin');
        if (inv.juniperBerriesCount > 0)
            this.addInvItem(content, `${inv.juniperBerriesCount} juniper berries`, '', 'medicinal-antiseptic');
        if (inv.willowBarkCount > 0)
            this.addInvItem(content, `${inv.willowBarkCount} willow bark`, '', 'medicinal-pain');
        if (inv.pineNeedlesCount > 0)
            this.addInvItem(content, `${inv.pineNeedlesCount} pine needles`, '', 'medicinal-vitamin');

        // Tree products
        if (inv.pineResinCount > 0)
            this.addInvItem(content, `${inv.pineResinCount} pine resin`, '', 'medicinal-wound');
        if (inv.usneaCount > 0)
            this.addInvItem(content, `${inv.usneaCount} usnea`, '', 'medicinal-antiseptic');
        if (inv.sphagnumCount > 0)
            this.addInvItem(content, `${inv.sphagnumCount} sphagnum moss`, '', 'medicinal-wound');

        if (content.children.length === 0) {
            this.addNoneItem(content);
        }
    }

    hideInventory() {
        document.getElementById('inventoryOverlay').classList.add('hidden');
    }

    showCrafting(crafting, input) {
        const overlay = document.getElementById('craftingOverlay');
        overlay.classList.remove('hidden');

        document.getElementById('craftingTitle').textContent = crafting.title;

        // Increment input ID FIRST
        this.currentInputId++;
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
            icon.className = 'material-symbols-outlined';
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
                    this.respond(null, inputId);
                }
            } else {
                this.respond(null, inputId);
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
                icon.className = 'material-symbols-outlined';

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
        document.getElementById('craftingOverlay').classList.add('hidden');
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
