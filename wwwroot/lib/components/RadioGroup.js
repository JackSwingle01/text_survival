// lib/components/RadioGroup.js
import { div, span, button } from '../helpers.js';

/**
 * Create an option button for use in radio groups
 * @param {Object} option - Option data
 * @param {string} option.id - Option identifier
 * @param {string} option.label - Option label
 * @param {string} [option.description] - Optional description
 * @param {string} [option.meta] - Optional meta text
 * @param {boolean} [option.disabled] - Whether option is disabled
 * @param {string} [option.disabledReason] - Reason for disabled state
 * @returns {HTMLElement}
 */
export function RadioOption(option) {
    const children = [
        span({ className: 'option-btn__label' }, option.label)
    ];

    if (option.disabled && option.disabledReason) {
        children.push(span({ className: 'choice-disabled-reason' }, option.disabledReason));
    } else if (option.description) {
        children.push(span({ className: 'option-btn__desc' }, option.description));
    }

    if (option.meta) {
        children.push(span({ className: 'option-btn__meta' }, option.meta));
    }

    return button(
        {
            className: `option-btn ${option.disabled ? 'disabled' : ''}`.trim(),
            disabled: option.disabled,
            'data-option-id': option.id
        },
        ...children
    );
}

/**
 * RadioGroup - Manages single-selection from a group of options
 */
export class RadioGroup {
    /**
     * @param {HTMLElement} container - Container element for options
     * @param {Function} [onChange] - Callback when selection changes
     */
    constructor(container, onChange = null) {
        this.container = container;
        this.onChange = onChange;
        this.selectedId = null;
        this._boundClickHandler = this._handleClick.bind(this);
    }

    /**
     * Render options and bind event handlers
     * @param {Array} options - Array of option objects
     */
    render(options) {
        this.container.replaceChildren();
        this.selectedId = null;

        options.forEach(option => {
            const btn = RadioOption(option);
            btn.addEventListener('click', this._boundClickHandler);
            this.container.appendChild(btn);
        });
    }

    /**
     * Handle option click
     */
    _handleClick(e) {
        const btn = e.target.closest('.option-btn');
        if (!btn || btn.disabled) return;

        const id = btn.dataset.optionId;
        this.select(id);
    }

    /**
     * Select an option by ID
     */
    select(id) {
        this.selectedId = id;
        this._updateUI();
        if (this.onChange) this.onChange(id);
    }

    /**
     * Update visual selection state
     */
    _updateUI() {
        this.container.querySelectorAll('.option-btn').forEach(btn => {
            btn.classList.toggle('selected', btn.dataset.optionId === this.selectedId);
        });
    }

    /**
     * Get the selected option ID
     */
    getValue() {
        return this.selectedId;
    }

    /**
     * Check if an option is selected
     */
    hasSelection() {
        return this.selectedId !== null;
    }

    /**
     * Clean up event handlers
     */
    destroy() {
        this.container.querySelectorAll('.option-btn').forEach(btn => {
            btn.removeEventListener('click', this._boundClickHandler);
        });
    }
}

/**
 * MultiStepForm - Manages multiple radio groups with a confirm button
 */
export class MultiStepForm {
    /**
     * @param {Object} config
     * @param {HTMLButtonElement} config.confirmBtn - Confirm button
     * @param {HTMLElement} config.confirmDesc - Description element for confirm button
     */
    constructor(config) {
        this.confirmBtn = config.confirmBtn;
        this.confirmDesc = config.confirmDesc;
        this.groups = {};  // { fieldName: RadioGroup }
        this._updateConfirmState();
    }

    /**
     * Add a radio group field
     * @param {string} fieldName - Field name
     * @param {HTMLElement} container - Container for options
     * @param {Array} options - Options to render
     * @param {Function} [onChange] - Optional change callback
     * @returns {RadioGroup}
     */
    addField(fieldName, container, options, onChange = null) {
        const group = new RadioGroup(container, (value) => {
            this._updateConfirmState();
            if (onChange) onChange(value);
        });
        group.render(options);
        this.groups[fieldName] = group;
        return group;
    }

    /**
     * Update confirm button state based on selections
     */
    _updateConfirmState() {
        const allSelected = Object.values(this.groups).every(g => g.hasSelection());
        this.confirmBtn.disabled = !allSelected;

        if (allSelected) {
            const labels = Object.values(this.groups).map(g => {
                const btn = g.container.querySelector(`[data-option-id="${g.getValue()}"]`);
                return btn?.querySelector('.option-btn__label')?.textContent || '';
            });
            this.confirmDesc.textContent = labels.filter(Boolean).join(' - ');
        } else {
            const missing = Object.entries(this.groups)
                .filter(([_, g]) => !g.hasSelection())
                .map(([name]) => name.replace(/Id$/, ''));
            this.confirmDesc.textContent = `Select ${missing.join(', ')}`;
        }
    }

    /**
     * Get all field values
     * @returns {Object}
     */
    getValues() {
        const values = {};
        Object.entries(this.groups).forEach(([name, group]) => {
            values[name] = group.getValue();
        });
        return values;
    }

    /**
     * Get combined choice ID from all selections
     * @param {string} [separator='_']
     * @returns {string}
     */
    getChoiceId(separator = '_') {
        return Object.values(this.groups)
            .map(g => g.getValue())
            .filter(Boolean)
            .join(separator);
    }

    /**
     * Check if all fields have selections
     */
    isComplete() {
        return Object.values(this.groups).every(g => g.hasSelection());
    }

    /**
     * Clean up all radio groups
     */
    destroy() {
        Object.values(this.groups).forEach(g => g.destroy());
        this.groups = {};
    }
}
