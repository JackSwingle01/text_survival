// lib/components/Icon.js
import { span } from '../helpers.js';

export const ICON_CLASS = 'material-symbols-outlined';

/**
 * Create a Material Symbols icon
 * @param {string} name - Icon name (e.g., 'warning', 'bolt', 'water_drop')
 * @param {string} [className=''] - Additional CSS classes
 * @returns {HTMLElement}
 */
export function Icon(name, className = '') {
    return span(
        { className: `${ICON_CLASS} ${className}`.trim() },
        name
    );
}

