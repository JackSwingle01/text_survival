// lib/components/Bar.js
import { div } from '../helpers.js';

/**
 * Create a progress/stat bar
 * @param {Object} props
 * @param {number} props.value - Current value
 * @param {number} props.max - Maximum value
 * @param {string} [props.className=''] - Additional CSS classes for the bar
 * @param {number} [props.dangerThreshold=30] - Percentage below which 'danger' class is added
 * @param {number} [props.warningThreshold=60] - Percentage below which 'warning' class is added
 * @returns {HTMLElement}
 */
export function Bar({ value, max, className = '', dangerThreshold = 30, warningThreshold = 60 }) {
    const pct = max > 0 ? Math.round((value / max) * 100) : 0;
    const clampedPct = Math.max(0, Math.min(100, pct));

    let fillClass = 'bar__fill';
    if (pct < dangerThreshold) {
        fillClass += ' danger';
    } else if (pct < warningThreshold) {
        fillClass += ' warning';
    }

    return div({ className: `bar ${className}`.trim() },
        div({
            className: fillClass,
            style: { width: `${clampedPct}%` }
        })
    );
}

/**
 * Create a bar with a label showing the percentage or value
 * @param {Object} props - Same as Bar, plus:
 * @param {boolean} [props.showPercent=true] - Show percentage instead of value
 * @returns {HTMLElement}
 */
export function LabeledBar({ value, max, className = '', dangerThreshold = 30, warningThreshold = 60, showPercent = true }) {
    const pct = max > 0 ? Math.round((value / max) * 100) : 0;
    const clampedPct = Math.max(0, Math.min(100, pct));

    let fillClass = 'bar__fill';
    if (pct < dangerThreshold) {
        fillClass += ' danger';
    } else if (pct < warningThreshold) {
        fillClass += ' warning';
    }

    const labelText = showPercent ? `${clampedPct}%` : `${value}/${max}`;

    return div({ className: `bar bar--labeled ${className}`.trim() },
        div({
            className: fillClass,
            style: { width: `${clampedPct}%` }
        }),
        div({ className: 'bar__label' }, labelText)
    );
}
