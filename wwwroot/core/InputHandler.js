// modules/core/InputHandler.js
import { gameAPI } from '../modules/api.js';

export class InputHandler {
    constructor() {
        this.awaitingResponse = false;
        this.resumeBlockUntil = 0;
    }

    /**
     * Check if input is currently blocked
     */
    isBlocked() {
        if (this.awaitingResponse) return true;
        if (Date.now() < this.resumeBlockUntil) return true;
        return false;
    }

    /**
     * Block input briefly (e.g., after page resume)
     */
    blockBriefly(durationMs = 200) {
        this.resumeBlockUntil = Date.now() + durationMs;
    }

    /**
     * Reset state (e.g., after receiving response)
     */
    reset() {
        this.awaitingResponse = false;
    }

    /**
     * Send an action to the server via REST API
     * @param {object} action - Action object with 'type' property
     * @returns {Promise<boolean>} True if sent successfully
     */
    async send(action) {
        if (this.isBlocked()) return false;

        this.awaitingResponse = true;
        this.disableAllButtons();

        try {
            await gameAPI.sendAction(action);
            return true;
        } catch (error) {
            console.error('[InputHandler] Error sending action:', error);
            this.awaitingResponse = false;
            this.enableAllButtons();
            return false;
        }
    }

    /**
     * Send a choice response via direct REST API calls
     * Replaces polymorphic action conversion with endpoint routing
     */
    async respond(choiceId, inputId = null) {
        // Validate choiceId
        if (!choiceId || choiceId === '') {
            console.error('[InputHandler] INVALID choiceId rejected:', { choiceId });
            return false;
        }

        // Call respondToChoice with proper error handling
        try {
            await this.respondToChoice(choiceId);
            return true;
        } catch (error) {
            console.error('[InputHandler] Error responding to choice:', error);
            return false;
        }
    }

    /**
     * Send a typed action directly (legacy compatibility)
     */
    sendAction(type, data = {}) {
        return this.send({ type, ...data });
    }

    /**
     * Route choiceId to appropriate REST API endpoint
     */
    async respondToChoice(choiceId) {
        if (this.awaitingResponse) return;
        this.awaitingResponse = true;
        this.disableAllButtons();

        try {
            const lowerChoice = choiceId.toLowerCase();

            // Navigation
            if (lowerChoice === 'continue' || lowerChoice === 'ok' || lowerChoice === 'dismiss') {
                return await gameAPI.continue();
            }

            // Fire management
            if (lowerChoice === 'manage_fire' || lowerChoice === 'fire') {
                return await gameAPI.campFire();
            }
            if (lowerChoice === 'close_fire') {
                return await gameAPI.fireClose();
            }
            if (lowerChoice === 'attempt_start') {
                return await gameAPI.fireStart();
            }
            if (lowerChoice === 'collect_charcoal') {
                return await gameAPI.fireCollectCharcoal();
            }
            if (lowerChoice.startsWith('fuel_')) {
                return await gameAPI.fireAddFuel(choiceId, 1);
            }
            if (lowerChoice.startsWith('tool_') && lowerChoice.includes('fire')) {
                return await gameAPI.fireSelectTool(choiceId);
            }
            if (lowerChoice.startsWith('tinder_')) {
                return await gameAPI.fireSelectTinder(choiceId);
            }
            if (lowerChoice.startsWith('ember_')) {
                return await gameAPI.fireLightCarrier(choiceId);
            }

            // Inventory
            if (lowerChoice === 'inventory') {
                return await gameAPI.campInventory();
            }
            if (lowerChoice === 'close_inventory') {
                return await gameAPI.inventoryClose();
            }
            if (lowerChoice.startsWith('equip_')) {
                return await gameAPI.inventoryEquip(choiceId.replace('equip_', ''));
            }
            if (lowerChoice === 'unequip') {
                return await gameAPI.inventoryUnequip();
            }

            // Storage
            if (lowerChoice === 'storage') {
                return await gameAPI.campStorage();
            }
            if (lowerChoice === 'close_storage') {
                return await gameAPI.storageClose();
            }
            if (lowerChoice.startsWith('to_storage_')) {
                return await gameAPI.storageToStorage(choiceId.replace('to_storage_', ''));
            }
            if (lowerChoice.startsWith('to_player_')) {
                return await gameAPI.storageToPlayer(choiceId.replace('to_player_', ''));
            }

            // Crafting
            if (lowerChoice === 'crafting') {
                return await gameAPI.campCrafting();
            }
            if (lowerChoice === 'close_crafting') {
                return await gameAPI.craftingClose();
            }
            if (lowerChoice.startsWith('craft_')) {
                return await gameAPI.craftingCraft(choiceId.substring(6));
            }
            if (lowerChoice.startsWith('category_')) {
                return await gameAPI.craftingCategory(choiceId.substring(9));
            }

            // Eating
            if (lowerChoice === 'eating' || lowerChoice === 'eat') {
                return await gameAPI.campEating();
            }
            if (lowerChoice === 'close_eating') {
                return await gameAPI.eatingClose();
            }
            if (lowerChoice === 'drink' || lowerChoice === 'drink_water' || lowerChoice === 'water') {
                return await gameAPI.eatingWater();
            }
            if (lowerChoice.startsWith('food_')) {
                return await gameAPI.eatingFood(choiceId);
            }

            // Cooking
            if (lowerChoice === 'cooking' || lowerChoice === 'cook') {
                return await gameAPI.cookingOpen();
            }
            if (lowerChoice === 'close_cooking') {
                return await gameAPI.cookingClose();
            }
            if (lowerChoice === 'cook_meat') {
                return await gameAPI.cookingCookMeat();
            }
            if (lowerChoice === 'melt_snow') {
                return await gameAPI.cookingMeltSnow();
            }

            // Sleep/Wait
            if (lowerChoice.startsWith('sleep_')) {
                const minutes = parseInt(choiceId.substring(6), 10) || 60;
                return await gameAPI.campSleep(minutes);
            }
            if (lowerChoice.startsWith('wait_')) {
                const minutes = parseInt(choiceId.substring(5), 10) || 10;
                return await gameAPI.campWait(minutes);
            }

            // Discovery Log
            if (lowerChoice === 'discovery_log') {
                return await gameAPI.discoveryOpen();
            }
            if (lowerChoice === 'close_discovery_log') {
                return await gameAPI.discoveryClose();
            }

            // Work actions
            if (lowerChoice === 'hunt' || lowerChoice === 'start_hunt') {
                return await gameAPI.workHunt();
            }
            if (lowerChoice === 'chop' || lowerChoice === 'start_chop') {
                return await gameAPI.workChop();
            }
            if (lowerChoice === 'check_snares') {
                return await gameAPI.workSnaresCheck();
            }
            if (lowerChoice === 'set_snare') {
                return await gameAPI.workSnaresSet();
            }
            if (lowerChoice === 'butcher' || lowerChoice === 'start_butcher') {
                return await gameAPI.workButcher();
            }

            // Hunt actions
            if (lowerChoice === 'approach' || lowerChoice === 'hunt_approach') {
                return await gameAPI.huntApproach();
            }
            if (lowerChoice === 'throw' || lowerChoice === 'hunt_throw') {
                return await gameAPI.huntThrow();
            }
            if (lowerChoice === 'strike' || lowerChoice === 'hunt_strike') {
                return await gameAPI.huntStrike();
            }
            if (lowerChoice === 'wait' || lowerChoice === 'hunt_wait') {
                return await gameAPI.huntWait();
            }
            if (lowerChoice === 'assess' || lowerChoice === 'hunt_assess') {
                return await gameAPI.huntAssess();
            }
            if (lowerChoice === 'abandon' || lowerChoice === 'hunt_abandon') {
                return await gameAPI.huntAbandon();
            }

            // Encounter actions
            if (lowerChoice === 'stand' || lowerChoice === 'stand_ground') {
                return await gameAPI.encounterStand();
            }
            if (lowerChoice === 'back' || lowerChoice === 'back_away') {
                return await gameAPI.encounterBack();
            }
            if (lowerChoice === 'drop_meat') {
                return await gameAPI.encounterDropMeat();
            }
            if (lowerChoice === 'attack' || lowerChoice === 'attack_predator') {
                return await gameAPI.encounterAttack();
            }
            if (lowerChoice === 'run' || lowerChoice === 'run_away') {
                return await gameAPI.encounterRun();
            }

            // Combat actions
            if (lowerChoice === 'combat_continue') {
                return await gameAPI.combatContinue();
            }
            if (lowerChoice.startsWith('combat_')) {
                return await gameAPI.combatAction(choiceId.substring(7));
            }

            // Butcher actions
            if (lowerChoice === 'cancel_butcher') {
                return await gameAPI.butcherCancel();
            }
            if (lowerChoice.startsWith('butcher_')) {
                return await gameAPI.butcherMode(choiceId.substring(8));
            }

            // Confirm actions
            if (lowerChoice === 'confirm' || lowerChoice === 'yes') {
                return await gameAPI.confirm(true);
            }
            if (lowerChoice === 'cancel' || lowerChoice === 'no') {
                return await gameAPI.confirm(false);
            }

            // Event choices (explicit patterns)
            if (lowerChoice.startsWith('event_') || lowerChoice.startsWith('choice_')) {
                const id = choiceId.replace('event_', '').replace('choice_', '');
                return await gameAPI.eventChoice(id);
            }

            // Default: treat as event choice
            console.log('[InputHandler] Treating as event choice:', choiceId);
            return await gameAPI.eventChoice(choiceId);

        } finally {
            this.awaitingResponse = false;
            this.enableAllButtons();
        }
    }

    /**
     * Disable all buttons to prevent double-clicks
     */
    disableAllButtons() {
        document.querySelectorAll('.btn, .option-btn, .combat-action').forEach(btn => {
            btn.disabled = true;
        });
    }

    /**
     * Re-enable all buttons
     */
    enableAllButtons() {
        document.querySelectorAll('.btn, .option-btn, .combat-action').forEach(btn => {
            btn.disabled = false;
        });
    }
}
