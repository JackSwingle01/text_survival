// lib/components/Badge.js
import { span } from '../helpers.js';
import { Icon } from './Icon.js';

/**
 * Create a badge with icon and label
 * @param {Object} props
 * @param {string} [props.icon] - Material icon name (optional)
 * @param {string} props.label - Badge text
 * @param {string} [props.type='neutral'] - Badge type: 'neutral', 'danger', 'warning', 'success', 'advantage'
 * @returns {HTMLElement}
 */
export function Badge({ icon, label, type = 'neutral' }) {
    return span({ className: `badge badge--${type}` },
        icon ? Icon(icon) : null,
        span({}, label)
    );
}

/**
 * Create a simple text-only badge
 * @param {string} label - Badge text
 * @param {string} [type='neutral'] - Badge type
 * @returns {HTMLElement}
 */
export function TextBadge(label, type = 'neutral') {
    return span({ className: `badge badge--${type}` }, label);
}
