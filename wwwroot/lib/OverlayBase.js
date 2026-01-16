// lib/OverlayBase.js
// Simplified overlay base class - uses rebuild pattern instead of patching

import { clear, show, hide } from './helpers.js';

/**
 * Base class for overlays using the rebuild pattern.
 *
 * Key differences from old OverlayManager:
 * - No element caching in constructor (except container)
 * - Single build() method returns entire DOM structure
 * - render() clears container and rebuilds from data
 * - Integrates with existing InputHandler for responses
 */
export class OverlayBase {
    /**
     * @param {string} overlayId - DOM element ID for this overlay
     * @param {Object} inputHandler - InputHandler instance for sending responses
     */
    constructor(overlayId, inputHandler) {
        this.overlayId = overlayId;
        this.inputHandler = inputHandler;
        this._container = null;
    }

    /**
     * Get the overlay container element
     */
    get container() {
        if (!this._container) {
            this._container = document.getElementById(this.overlayId);
        }
        return this._container;
    }

    /**
     * Query within this overlay
     */
    $(selector) {
        return this.container?.querySelector(selector);
    }

    /**
     * Show the overlay
     */
    show() {
        show(this.container);
    }

    /**
     * Hide the overlay
     */
    hide() {
        hide(this.container);
    }

    /**
     * Main render method - clears and rebuilds
     * @param {Object|null} data - Data to render, null to hide
     * @param {number} inputId - Current input ID
     * @param {Object} input - Full input object (for choices)
     */
    render(data, inputId, input) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Find content container (overlay minus chrome like headers)
        const content = this.$('.overlay-content') || this.container;
        clear(content);

        // Build and append new content
        const built = this.build(data, input);
        if (built) {
            if (Array.isArray(built)) {
                built.forEach(el => el && content.appendChild(el));
            } else {
                content.appendChild(built);
            }
        }
    }

    /**
     * Safe render wrapper - catches errors
     */
    safeRender(data, inputId, input) {
        try {
            if (data === undefined || data === null) {
                this.hide();
                return;
            }
            this.render(data, inputId, input);
        } catch (error) {
            console.error(`[${this.overlayId}] Render error:`, error);
            console.error(`[${this.overlayId}] Data:`, JSON.stringify(data, null, 2));
        }
    }

    /**
     * Build the overlay content - override in subclasses
     * @param {Object} data - Data to render
     * @param {Object} input - Full input object
     * @returns {HTMLElement|HTMLElement[]} - Built DOM element(s)
     */
    build(data, input) {
        throw new Error('Subclass must implement build()');
    }

    /**
     * Send a response to the server
     * @param {string} choiceId - Choice identifier
     */
    respond(choiceId) {
        return this.inputHandler.respond(choiceId);
    }

    /**
     * Send an action to the server
     * @param {string} type - Action type
     * @param {Object} data - Action data
     */
    sendAction(type, data) {
        return this.inputHandler.sendAction(type, data);
    }

    /**
     * Create a click handler bound to respond
     * @param {string} choiceId - Choice to respond with
     */
    makeClickHandler(choiceId) {
        return () => this.respond(choiceId);
    }
}
