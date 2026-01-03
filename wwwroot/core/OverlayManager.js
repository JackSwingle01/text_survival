// modules/overlays/OverlayManager.js
import { Utils, show, hide, ICON_CLASS } from '../modules/utils.js';
import { Animator } from './Animator.js';

/**
 * Base class for overlay management
 * Handles common patterns: show/hide, input tracking, button creation
 */
export class OverlayManager {
    constructor(overlayId, inputHandler) {
        this.overlayId = overlayId;
        this.inputHandler = inputHandler;
    }

    get overlay() {
        return document.getElementById(this.overlayId);
    }

    /**
     * Show the overlay
     */
    show(inputId) {
        // Don't store inputId - always use current from InputHandler
        show(this.overlay);
    }

    /**
     * Hide the overlay and clean up
     */
    hide() {
        hide(this.overlay);
        this.cleanup();
    }

    /**
     * Override in subclasses for specific cleanup
     */
    cleanup() {}

    /**
     * Wrapper for render() that catches errors centrally.
     * app.js calls this instead of render() directly.
     */
    safeRender(data, inputId, input) {
        try {
            if (data === undefined || data === null) {
                console.error(`[${this.overlayId}] safeRender called with null/undefined data`);
                return;
            }
            this.render(data, inputId, input);
        } catch (error) {
            console.error(`[${this.overlayId}] Render error:`, error);
            console.error(`[${this.overlayId}] Data:`, JSON.stringify(data, null, 2));
            // Don't re-throw - prevent one overlay from breaking entire frame
        }
    }

    /**
     * Safe document.getElementById with logging. Use in constructors.
     */
    getElement(id) {
        const el = document.getElementById(id);
        if (!el) {
            console.warn(`[${this.overlayId}] Element '${id}' not found`);
        }
        return el;
    }

    /**
     * Get an element within this overlay
     */
    $(selector) {
        return this.overlay?.querySelector(selector);
    }

    /**
     * Clear an element's contents
     */
    clear(element) {
        if (typeof element === 'string') {
            element = this.$(element);
        }
        if (element) Utils.clearElement(element);
    }

    /**
     * Send a response using the current inputId from InputHandler
     */
    respond(choiceId) {
        const currentInputId = this.inputHandler.getCurrentInputId();
        return this.inputHandler.respond(choiceId, currentInputId);
    }

    /**
     * Send an action using the current inputId from InputHandler
     */
    sendAction(type, data) {
        const currentInputId = this.inputHandler.getCurrentInputId();
        return this.inputHandler.sendAction(type, data, currentInputId);
    }

    /**
     * Create a bound click handler that uses current inputId
     */
    makeClickHandler(choiceId) {
        return () => this.respond(choiceId);
    }

    /**
     * Create a standardized option button
     * @param {Object} config - Button configuration
     * @param {string} config.datasetKey - Dataset key (e.g., 'focusId', 'timeId', 'modeId')
     * @param {string} config.datasetValue - Dataset value
     * @param {string} config.label - Button label text
     * @param {string} [config.description] - Optional description text
     * @param {string} [config.meta] - Optional meta text (e.g., "~30 min")
     * @param {boolean} [config.disabled] - Whether button is disabled
     * @param {string} [config.disabledReason] - Reason shown when disabled
     * @param {Function} [config.onClick] - Optional click handler
     * @param {string} [config.className='option-btn'] - Custom CSS class name
     * @returns {HTMLButtonElement} The created button element
     */
    createOptionButton(config) {
        const btn = document.createElement('button');
        btn.className = config.className || 'option-btn';

        if (config.datasetKey && config.datasetValue) {
            btn.dataset[config.datasetKey] = config.datasetValue;
        }

        if (config.disabled) {
            btn.disabled = true;
        }

        // Label (required)
        const label = document.createElement('span');
        label.className = 'option-btn__label';
        label.textContent = config.label;
        btn.appendChild(label);

        // Description (optional)
        if (config.description) {
            const desc = document.createElement('span');
            desc.className = 'option-btn__desc';
            desc.textContent = config.description;
            btn.appendChild(desc);
        }

        // Meta (optional)
        if (config.meta) {
            const meta = document.createElement('span');
            meta.className = 'option-btn__meta';
            meta.textContent = config.meta;
            btn.appendChild(meta);
        }

        // Disabled reason (shown instead of description when disabled)
        if (config.disabled && config.disabledReason) {
            const reason = document.createElement('span');
            reason.className = 'choice-disabled-reason';
            reason.textContent = config.disabledReason;
            btn.appendChild(reason);
        }

        // Click handler (optional)
        if (config.onClick) {
            btn.onclick = config.onClick;
        }

        return btn;
    }

    /**
     * Render a list of choices into a container
     * @param {HTMLElement} container - Target container
     * @param {Array} choices - Array of choice objects
     */
    renderChoices(container, choices) {
        choices.forEach(choice => {
            // Skip choices without IDs to prevent sending empty strings to server
            if (!choice.id) {
                console.error('[OverlayManager] Choice missing id, skipping:', choice);
                return;
            }
            const btn = this.createOptionButton({
                label: choice.label,
                description: choice.description,
                meta: choice.hitChance || choice.cost,
                disabled: !choice.isAvailable,
                disabledReason: choice.disabledReason,
                onClick: () => this.respond(choice.id)
            });
            container.appendChild(btn);
        });
    }

    /**
     * Set choices and auto-render them
     * @param {Array} choices - Choice objects
     * @param {string} containerSelector - CSS selector for container
     */
    setChoices(choices, containerSelector) {
        const container = this.$(containerSelector);

        if (!container) {
            console.warn(`Choices container not found: ${containerSelector}`);
            return;
        }

        this.clear(container);
        container.className = 'event-choices';
        this.renderChoices(container, choices);
    }

    /**
     * Create an icon + text element
     * @param {string} iconName - Material icon name
     * @param {string} text - Text content
     * @param {string} [className='icon-text'] - CSS class name
     * @returns {HTMLDivElement} The created element
     */
    createIconText(iconName, text, className = 'icon-text') {
        const container = document.createElement('div');
        container.className = className;

        const icon = document.createElement('span');
        icon.className = ICON_CLASS;
        icon.textContent = iconName;
        container.appendChild(icon);

        const textSpan = document.createElement('span');
        textSpan.textContent = text;
        container.appendChild(textSpan);

        return container;
    }

    /**
     * Create a warning element with icon
     * @param {string} text - Warning text
     * @param {string} [iconName='warning'] - Icon name
     * @returns {HTMLDivElement} The created warning element
     */
    createWarning(text, iconName = 'warning') {
        return this.createIconText(iconName, text, 'warning-item');
    }

    /**
     * Create an info panel wrapper
     * @param {Object} options - Panel configuration
     * @param {string} [options.title] - Optional title text
     * @param {string} [options.description] - Optional description text
     * @param {boolean} [options.center] - Center align content
     * @param {boolean} [options.danger] - Apply danger styling
     * @param {HTMLElement[]} [options.children] - Child elements to append
     * @returns {HTMLDivElement} The created info panel
     */
    createInfoPanel(options = {}) {
        const panel = document.createElement('div');
        panel.className = 'info-panel';
        if (options.center) panel.classList.add('info-panel--center');
        if (options.danger) panel.classList.add('info-panel--danger');

        if (options.title) {
            const title = document.createElement('div');
            title.className = 'info-panel__title';
            title.textContent = options.title;
            panel.appendChild(title);
        }

        if (options.description) {
            const desc = document.createElement('div');
            desc.className = 'info-panel__desc';
            desc.textContent = options.description;
            panel.appendChild(desc);
        }

        if (options.children) {
            options.children.forEach(child => panel.appendChild(child));
        }

        return panel;
    }

    /**
     * Create a stat row (label + value)
     * @param {string} label - Row label
     * @param {string} value - Row value
     * @param {Object} [options={}] - Additional options
     * @param {string} [options.icon] - Optional icon for label
     * @param {boolean} [options.background] - Add background styling
     * @param {string} [options.valueClass] - Additional CSS class for value
     * @param {string} [options.classPrefix='stat'] - Class prefix (e.g., 'fire-stat' for fire-stat-row)
     * @returns {HTMLDivElement} The created stat row
     */
    createStatRow(label, value, options = {}) {
        const prefix = options.classPrefix || 'stat-row';
        const row = document.createElement('div');
        row.className = `${prefix}` + (options.background ? ` ${prefix}--bg` : '');

        if (options.icon) {
            const labelContainer = this.createIconText(options.icon, label);
            labelContainer.className = `${prefix}__label`;
            row.appendChild(labelContainer);
        } else {
            const labelEl = document.createElement('div');
            labelEl.className = `${prefix}__label`;
            labelEl.textContent = label;
            row.appendChild(labelEl);
        }

        const valueEl = document.createElement('div');
        valueEl.className = `${prefix}__value`;
        if (options.valueClass) valueEl.classList.add(`${prefix}__value--${options.valueClass}`);
        valueEl.textContent = value;
        row.appendChild(valueEl);

        return row;
    }

    /**
     * Create a radio group for button selection
     * @param {HTMLElement} container - Container element with buttons
     * @param {string} datasetKey - Dataset key to use for selection (e.g., 'focusId', 'timeId')
     * @param {Function} onChange - Callback when selection changes
     * @returns {RadioGroup} The radio group instance
     */
    createRadioGroup(container, datasetKey, onChange) {
        return new RadioGroup(container, datasetKey, onChange);
    }

    /**
     * Create a form for multi-step selection
     * @param {Object} config - Form configuration
     * @param {HTMLButtonElement} config.confirmBtn - Confirm button element
     * @param {HTMLElement} config.confirmDesc - Confirm description element
     * @param {Function} [config.onSubmit] - Optional submit callback
     * @returns {Form} The form instance
     */
    createForm(config) {
        return new Form(config);
    }

    /**
     * Run an action with progress bar animation
     * @param {Object} config - Progress configuration
     * @param {HTMLElement} config.progressEl - Progress container element
     * @param {HTMLElement} config.progressBar - Progress bar element
     * @param {HTMLElement} config.progressText - Progress text element
     * @param {HTMLElement} config.progressResult - Progress result element
     * @param {HTMLButtonElement} config.doneBtn - Done button to disable
     * @param {string} config.progressMessage - Text to show during progress (e.g., "Cooking...")
     * @param {number} config.durationMs - Animation duration in milliseconds
     * @param {string} config.choiceId - Choice ID to send when complete
     * @param {string} config.pendingFlag - Name of the pending flag property (e.g., 'pendingCooking')
     * @param {HTMLElement} [config.buttonsContainer] - Optional container to disable buttons in
     * @param {string} [config.buttonSelector] - Optional selector for buttons to disable (default: 'button, .list-item--clickable')
     */
    runWithProgress(config) {
        // Set pending flag
        this[config.pendingFlag] = true;

        // Show progress bar
        show(config.progressEl);
        hide(config.progressResult);
        config.progressText.textContent = config.progressMessage;

        // Disable done button
        config.doneBtn.disabled = true;

        // Disable action buttons if container provided
        if (config.buttonsContainer) {
            const selector = config.buttonSelector || 'button, .list-item--clickable';
            const buttons = config.buttonsContainer.querySelectorAll(selector);
            buttons.forEach(btn => {
                if (btn.tagName === 'BUTTON') {
                    btn.disabled = true;
                } else {
                    btn.style.pointerEvents = 'none';
                }
            });
        }

        // Animate progress bar
        Animator.progressBar(config.progressBar, config.durationMs, () => {
            this.respond(config.choiceId);
        });
    }

    /**
     * Check if progress animation just completed and handle result display
     * Call this at the start of render() for overlays that use progress
     * @param {Object} config - Same config as runWithProgress
     * @param {string} config.successMessage - Message to show on success (e.g., "Consumed!")
     * @returns {boolean} - True if progress was pending (so caller knows to skip normal render)
     */
    handleProgressComplete(config) {
        if (this[config.pendingFlag]) {
            this[config.pendingFlag] = false;

            // Show success result
            config.progressResult.textContent = config.successMessage;
            config.progressResult.className = 'overlay-progress-result success';
            show(config.progressResult);

            // Hide after delay
            setTimeout(() => {
                hide(config.progressEl);
                hide(config.progressResult);
                config.progressBar.style.width = '0%';
            }, 1500);

            return true;
        }

        // Hide progress bar initially if not pending
        hide(config.progressEl);
        hide(config.progressResult);
        config.progressBar.style.width = '0%';

        // Re-enable done button
        config.doneBtn.disabled = false;

        return false;
    }
}

/**
 * Form - Manages multi-step selection with automatic confirm button state
 * Coordinates multiple RadioGroups and validates completion
 */
class Form {
    constructor(config) {
        this.confirmBtn = config.confirmBtn;
        this.confirmDesc = config.confirmDesc;
        this.onSubmit = config.onSubmit;
        this.fields = {}; // { fieldName: { group: RadioGroup, value: null } }
        // Initialize button state
        this.updateConfirmButton();
    }

    /**
     * Add a radio group field to the form
     * @param {string} fieldName - Field name (e.g., 'focusId', 'timeId')
     * @param {HTMLElement} container - Container with option buttons
     * @param {string} datasetKey - Dataset key for selection
     * @param {Function} [onChange] - Optional onChange callback
     * @returns {RadioGroup} The created radio group
     */
    addRadioGroup(fieldName, container, datasetKey, onChange) {
        const group = new RadioGroup(container, datasetKey, (value) => {
            this.setValue(fieldName, value);
            if (onChange) onChange(value);
        });
        group.bind();
        this.fields[fieldName] = { group, value: null };
        return group;
    }

    /**
     * Set a field value and update confirm button state
     */
    setValue(fieldName, value) {
        this.fields[fieldName].value = value;
        this.updateConfirmButton();
    }

    /**
     * Update confirm button enabled state and description text
     */
    updateConfirmButton() {
        const allSelected = Object.values(this.fields).every(f => f.value !== null);
        this.confirmBtn.disabled = !allSelected;

        if (allSelected) {
            // Build description from selected values
            const labels = Object.entries(this.fields).map(([_name, field]) => {
                const attrName = field.group.datasetKey.replace(/([A-Z])/g, '-$1').toLowerCase();
                const btn = document.querySelector(`[data-${attrName}="${field.value}"]`);
                return btn?.querySelector('.option-btn__label')?.textContent || '';
            });
            this.confirmDesc.textContent = labels.join(' - ');
        } else {
            // Show what's missing
            const missing = Object.keys(this.fields).filter(k => !this.fields[k].value);
            const missingLabels = missing.map(this.toReadable);
            this.confirmDesc.textContent = `Select ${missingLabels.join(', ')}`;
        }
    }

    /**
     * Get all field values as an object
     * @returns {Object} Field values (e.g., { focusId: 'general', timeId: 'quick' })
     */
    getValues() {
        const values = {};
        Object.entries(this.fields).forEach(([name, field]) => {
            values[name] = field.value;
        });
        return values;
    }

    /**
     * Build a choice ID from field values
     * @param {string} [separator='_'] - Separator for joining values
     * @returns {string} Combined choice ID (e.g., 'general_quick')
     */
    getChoiceId(separator = '_') {
        return Object.values(this.fields).map(f => f.value).join(separator);
    }

    /**
     * Check if all required fields are filled
     * @returns {boolean} True if form is complete
     */
    isComplete() {
        return Object.values(this.fields).every(f => f.value !== null);
    }

    /**
     * Clean up all radio groups
     */
    cleanup() {
        Object.values(this.fields).forEach(f => f.group.unbind());
    }

    /**
     * Convert camelCase field name to readable text
     */
    toReadable(fieldName) {
        // focusId -> focus, timeId -> time, modeId -> mode
        return fieldName.replace(/Id$/, '').replace(/([A-Z])/g, ' $1').toLowerCase();
    }
}

/**
 * RadioGroup - Manages selection state for a group of option buttons
 * Automatically handles click events and visual selection state
 */
class RadioGroup {
    constructor(container, datasetKey, onChange) {
        this.container = container;
        this.datasetKey = datasetKey;
        this.onChange = onChange;
        this.value = null;
        this.boundClickHandler = (e) => this.handleClick(e);
    }

    /**
     * Bind click handlers to all buttons in the container
     */
    bind() {
        const attrName = this.datasetKey.replace(/([A-Z])/g, '-$1').toLowerCase();
        this.container.querySelectorAll(`.option-btn[data-${attrName}]`).forEach(btn => {
            btn.addEventListener('click', this.boundClickHandler);
        });
    }

    /**
     * Handle button click events
     */
    handleClick(e) {
        const btn = e.target.closest('.option-btn');
        if (btn) this.select(btn.dataset[this.datasetKey]);
    }

    /**
     * Select a value and update UI
     */
    select(value) {
        this.value = value;
        this.updateUI();
        if (this.onChange) this.onChange(value);
    }

    /**
     * Update visual selection state
     */
    updateUI() {
        const attrName = this.datasetKey.replace(/([A-Z])/g, '-$1').toLowerCase();
        this.container.querySelectorAll(`.option-btn[data-${attrName}]`).forEach(btn => {
            btn.classList.toggle('selected', btn.dataset[this.datasetKey] === this.value);
        });
    }

    /**
     * Unbind click handlers
     */
    unbind() {
        const attrName = this.datasetKey.replace(/([A-Z])/g, '-$1').toLowerCase();
        this.container.querySelectorAll(`.option-btn[data-${attrName}]`).forEach(btn => {
            btn.removeEventListener('click', this.boundClickHandler);
        });
    }
}