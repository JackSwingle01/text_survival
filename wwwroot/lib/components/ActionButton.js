// lib/components/ActionButton.js
import { button, span } from '../helpers.js';

/**
 * Create an action/choice button with optional description and meta text
 * @param {Object} action - Action configuration
 * @param {string} action.id - Action identifier (used for response)
 * @param {string} action.label - Button label text
 * @param {string} [action.description] - Optional description text
 * @param {string} [action.meta] - Optional meta text (e.g., time cost, hit chance)
 * @param {boolean} [action.isAvailable=true] - Whether the action is available
 * @param {boolean} [action.disabled] - Alias for !isAvailable
 * @param {string} [action.disabledReason] - Reason shown when disabled
 * @param {Function} onClick - Click handler
 * @returns {HTMLElement}
 */
export function ActionButton(action, onClick) {
    const isDisabled = action.disabled || action.isAvailable === false;

    const children = [
        span({ className: 'option-btn__label' }, action.label)
    ];

    // Description or disabled reason
    if (isDisabled && action.disabledReason) {
        children.push(
            span({ className: 'choice-disabled-reason' }, action.disabledReason)
        );
    } else if (action.description) {
        children.push(
            span({ className: 'option-btn__desc' }, action.description)
        );
    }

    // Meta text (time, cost, etc.)
    if (action.meta || action.hitChance || action.cost) {
        children.push(
            span({ className: 'option-btn__meta' }, action.meta || action.hitChance || action.cost)
        );
    }

    return button(
        {
            className: `option-btn ${isDisabled ? 'disabled' : ''}`.trim(),
            disabled: isDisabled,
            onClick: isDisabled ? null : onClick
        },
        ...children
    );
}

/**
 * Create a simple button (not an option button)
 * @param {string} label - Button text
 * @param {Function} onClick - Click handler
 * @param {Object} [options={}] - Additional options
 * @param {string} [options.className='btn'] - CSS class
 * @param {boolean} [options.primary=false] - Use primary styling
 * @param {boolean} [options.disabled=false] - Disabled state
 * @returns {HTMLElement}
 */
export function SimpleButton(label, onClick, options = {}) {
    const { className = 'btn', primary = false, disabled = false } = options;
    const fullClass = `${className} ${primary ? 'btn--primary' : ''}`.trim();

    return button(
        {
            className: fullClass,
            disabled,
            onClick: disabled ? null : onClick
        },
        label
    );
}

/**
 * Create a continue/confirm button (commonly used in outcomes)
 * @param {Function} onClick - Click handler
 * @param {string} [label='Continue'] - Button text
 * @returns {HTMLElement}
 */
export function ContinueButton(onClick, label = 'Continue') {
    return button(
        { className: 'event-continue-btn', onClick },
        label
    );
}
