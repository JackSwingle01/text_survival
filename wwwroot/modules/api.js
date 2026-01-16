/**
 * REST API client for stateless game communication.
 * Replaces WebSocket-based communication.
 */

const SESSION_KEY = 'textSurvivalSessionId';

class GameAPI {
    constructor() {
        this.sessionId = null;
        this.onFrame = null; // Callback for frame updates
        this.onError = null; // Callback for errors
        this.onConnectionChange = null; // Callback for connection state
    }

    /**
     * Initialize the API client - load or create session
     */
    async init() {
        this.sessionId = localStorage.getItem(SESSION_KEY);

        if (this.sessionId) {
            // Check if session exists on server
            const exists = await this.checkSessionExists();
            if (!exists) {
                // Session expired or invalid, create new one
                await this.createNewGame();
            } else {
                // Load existing game state
                await this.loadState();
            }
        } else {
            // No session, create new game
            await this.createNewGame();
        }
    }

    /**
     * Check if a session exists on the server
     */
    async checkSessionExists() {
        try {
            const response = await fetch(`/api/game/${this.sessionId}/exists`);
            const data = await response.json();
            return data.exists;
        } catch (error) {
            console.error('[API] Error checking session:', error);
            return false;
        }
    }

    /**
     * Create a new game
     */
    async createNewGame() {
        try {
            this.notifyConnectionChange('connecting');

            const response = await fetch('/api/game/new', { method: 'POST' });
            if (!response.ok) {
                throw new Error(`Failed to create game: ${response.status}`);
            }

            const data = await response.json();
            this.sessionId = data.sessionId;
            localStorage.setItem(SESSION_KEY, this.sessionId);

            this.notifyConnectionChange('connected');

            if (data.initialState && this.onFrame) {
                this.onFrame(data.initialState);
            }

            return data;
        } catch (error) {
            console.error('[API] Error creating game:', error);
            this.notifyConnectionChange('error');
            throw error;
        }
    }

    /**
     * Load current game state
     */
    async loadState() {
        try {
            this.notifyConnectionChange('connecting');

            const response = await fetch(`/api/game/${this.sessionId}`);
            if (!response.ok) {
                throw new Error(`Failed to load state: ${response.status}`);
            }

            const data = await response.json();

            this.notifyConnectionChange('connected');

            if (this.onFrame) {
                this.onFrame(data.frame);
            }

            return data;
        } catch (error) {
            console.error('[API] Error loading state:', error);
            this.notifyConnectionChange('error');
            throw error;
        }
    }

    /**
     * Send an action to the server
     * @param {object} action - The action object with 'type' and optional data
     * @returns {Promise<object>} The response frame
     */
    async sendAction(action) {
        if (!this.sessionId) {
            throw new Error('No active session');
        }

        try {
            const response = await fetch(`/api/game/${this.sessionId}/action`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(action)
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.error || `Action failed: ${response.status}`);
            }

            const data = await response.json();

            if (data.isError) {
                throw new Error(data.errorMessage || 'Unknown error');
            }

            if (this.onFrame) {
                this.onFrame(data.frame);
            }

            return data;
        } catch (error) {
            console.error('[API] Error sending action:', error);
            if (this.onError) {
                this.onError(error.message);
            }
            throw error;
        }
    }

    /**
     * Helper method for POST requests to action endpoints
     */
    async _post(endpoint, body = null) {
        if (!this.sessionId) {
            throw new Error('No active session');
        }

        try {
            const options = {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            };

            if (body !== null) {
                options.body = JSON.stringify(body);
            }

            const response = await fetch(`/api/game/${this.sessionId}${endpoint}`, options);

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.error || `Request failed: ${response.status}`);
            }

            const data = await response.json();

            if (data.isError) {
                throw new Error(data.errorMessage || 'Unknown error');
            }

            if (this.onFrame) {
                this.onFrame(data.frame);
            }

            return data;
        } catch (error) {
            console.error(`[API] Error POST ${endpoint}:`, error);
            if (this.onError) {
                this.onError(error.message);
            }
            throw error;
        }
    }

    // Navigation
    async move(x, y) {
        return this._post('/move', { x, y });
    }

    async cancelTravel() {
        return this._post('/travel/cancel');
    }

    async travelHazardChoice(quickTravel) {
        return this._post('/travel/hazard-choice', { quickTravel });
    }

    async travelContinue(shouldContinue) {
        return this._post('/travel/continue', { continue: shouldContinue });
    }

    async travelImpairment(proceed) {
        return this._post('/travel/impairment', { proceed });
    }

    // Camp
    async campFire() {
        return this._post('/camp/fire');
    }

    async campInventory() {
        return this._post('/camp/inventory');
    }

    async campStorage() {
        return this._post('/camp/storage');
    }

    async campCrafting() {
        return this._post('/camp/crafting');
    }

    async campEating() {
        return this._post('/camp/eating');
    }

    async campSleep(durationMinutes) {
        return this._post('/camp/sleep', { durationMinutes });
    }

    async campWait(minutes) {
        return this._post('/camp/wait', { minutes });
    }

    // Fire
    async fireSelectTool(toolId) {
        return this._post('/fire/select-tool', { toolId });
    }

    async fireSelectTinder(tinderId) {
        return this._post('/fire/select-tinder', { tinderId });
    }

    async fireStart(toolId, tinderId) {
        return this._post('/fire/start', { toolId, tinderId });
    }

    async fireAddFuel(fuelId, count = 1) {
        return this._post('/fire/add-fuel', { fuelId, count });
    }

    async fireLightCarrier(carrierId) {
        return this._post('/fire/light-carrier', { carrierId });
    }

    async fireCollectCharcoal() {
        return this._post('/fire/collect-charcoal');
    }

    async fireClose() {
        return this._post('/fire/close');
    }

    // Cooking
    async cookingOpen() {
        return this._post('/cooking/open');
    }

    async cookingCookMeat() {
        return this._post('/cooking/cook-meat');
    }

    async cookingMeltSnow() {
        return this._post('/cooking/melt-snow');
    }

    async cookingClose() {
        return this._post('/cooking/close');
    }

    // Inventory
    async inventoryClose() {
        return this._post('/inventory/close');
    }

    async inventoryEquip(toolId) {
        return this._post('/inventory/equip', { toolId });
    }

    async inventoryUnequip() {
        return this._post('/inventory/unequip');
    }

    // Storage
    async storageToStorage(itemId) {
        return this._post('/storage/to-storage', { itemId });
    }

    async storageToPlayer(itemId) {
        return this._post('/storage/to-player', { itemId });
    }

    async storageClose() {
        return this._post('/storage/close');
    }

    // Crafting
    async craftingCategory(categoryId) {
        return this._post('/crafting/category', { categoryId });
    }

    async craftingCraft(recipeId) {
        return this._post('/crafting/craft', { recipeId });
    }

    async craftingClose() {
        return this._post('/crafting/close');
    }

    // Eating
    async eatingFood(itemId) {
        return this._post('/eating/food', { itemId });
    }

    async eatingWater() {
        return this._post('/eating/water');
    }

    async eatingClose() {
        return this._post('/eating/close');
    }

    // Work
    async workForage(focusId, timeId) {
        return this._post('/work/forage', { focusId, timeId });
    }

    async workHunt() {
        return this._post('/work/hunt');
    }

    async workHarvest(resourceId) {
        return this._post('/work/harvest', { resourceId });
    }

    async workChop() {
        return this._post('/work/chop');
    }

    async workSnaresCheck() {
        return this._post('/work/snares/check');
    }

    async workSnaresSet() {
        return this._post('/work/snares/set');
    }

    async workButcher() {
        return this._post('/work/butcher');
    }

    // Hunt
    async huntApproach() {
        return this._post('/hunt/approach');
    }

    async huntThrow() {
        return this._post('/hunt/throw');
    }

    async huntStrike() {
        return this._post('/hunt/strike');
    }

    async huntWait() {
        return this._post('/hunt/wait');
    }

    async huntAssess() {
        return this._post('/hunt/assess');
    }

    async huntAbandon() {
        return this._post('/hunt/abandon');
    }

    // Encounter
    async encounterStand() {
        return this._post('/encounter/stand');
    }

    async encounterBack() {
        return this._post('/encounter/back');
    }

    async encounterDropMeat() {
        return this._post('/encounter/drop-meat');
    }

    async encounterAttack() {
        return this._post('/encounter/attack');
    }

    async encounterRun() {
        return this._post('/encounter/run');
    }

    // Combat
    async combatAction(actionId) {
        return this._post('/combat/action', { actionId });
    }

    async combatContinue() {
        return this._post('/combat/continue');
    }

    // Event
    async eventChoice(choiceId) {
        return this._post('/event/choice', { choiceId });
    }

    // Universal
    async continue() {
        return this._post('/continue');
    }

    async confirm(confirmed) {
        return this._post('/confirm', { confirmed });
    }

    // Discovery
    async discoveryOpen() {
        return this._post('/discovery/open');
    }

    async discoveryClose() {
        return this._post('/discovery/close');
    }

    // Butcher
    async butcherMode(modeId) {
        return this._post('/butcher/mode', { modeId });
    }

    async butcherCancel() {
        return this._post('/butcher/cancel');
    }

    // ========================================
    // UNIFIED ACTION ENDPOINT (New Router)
    // ========================================

    /**
     * Send an action through the unified /action endpoint.
     * This is the new simplified API that routes through ActionRouter.
     * All game actions can be sent as a simple choiceId string.
     *
     * @param {string} choiceId - The action identifier (e.g., "approach", "move_5_3", "sleep_60")
     * @returns {Promise<object>} The response with updated frame
     */
    async action(choiceId) {
        return this.sendAction({ choiceId });
    }

    /**
     * Restart the game (delete current session and create new)
     */
    async restart() {
        if (this.sessionId) {
            try {
                await fetch(`/api/game/${this.sessionId}`, { method: 'DELETE' });
            } catch (error) {
                console.warn('[API] Error deleting session:', error);
            }
        }

        localStorage.removeItem(SESSION_KEY);
        this.sessionId = null;

        await this.createNewGame();
    }

    /**
     * Notify connection state change
     */
    notifyConnectionChange(state) {
        if (this.onConnectionChange) {
            this.onConnectionChange(state);
        }
    }
}

// Export singleton instance
export const gameAPI = new GameAPI();

// Also export the class for testing
export { GameAPI };
