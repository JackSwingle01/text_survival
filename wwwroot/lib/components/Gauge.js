// lib/components/Gauge.js
import { div, span } from '../helpers.js';

/**
 * Create a gauge/meter display (e.g., boldness, aggression)
 * @param {Object} props
 * @param {number} props.value - Current value (0-1)
 * @param {string} props.descriptor - Text descriptor (e.g., "wary", "bold", "aggressive")
 * @param {string} [props.className=''] - Additional CSS classes
 * @param {string} [props.label=''] - Optional label
 * @returns {HTMLElement}
 */
export function Gauge({ value, descriptor, className = '', label = '' }) {
    const pct = Math.round(Math.max(0, Math.min(1, value)) * 100);
    const normalizedDescriptor = (descriptor || 'neutral').toLowerCase();

    return div({ className: `gauge-bar ${className}`.trim() },
        label ? span({ className: 'gauge-bar__label' }, label) : null,
        div({ className: 'gauge-bar__track' },
            div({
                className: `gauge-bar__fill gauge-bar__fill--${normalizedDescriptor}`,
                style: { width: `${pct}%` }
            })
        ),
        span({ className: `gauge-bar__descriptor gauge-bar__descriptor--${normalizedDescriptor}` }, descriptor)
    );
}

/**
 * Create a distance track with player marker (for combat)
 * @param {Object} props
 * @param {number} props.distance - Current distance in meters
 * @param {number} props.maxDistance - Maximum distance
 * @param {boolean} [props.reversed=false] - If true, closer = right side (default: closer = left)
 * @returns {HTMLElement}
 */
export function DistanceTrack({ distance, maxDistance, reversed = false }) {
    // Position: 0m = 100% (right), maxDistance = 0% (left) when not reversed
    let positionPct = reversed
        ? (distance / maxDistance * 100)
        : (100 - distance / maxDistance * 100);
    positionPct = Math.max(0, Math.min(100, positionPct));

    return div({ className: 'distance-track' },
        div({ className: 'distance-track__bar' },
            div({
                className: 'distance-track__marker',
                style: { left: `${positionPct}%` }
            })
        ),
        span({ className: 'distance-track__value' }, `${Math.round(distance)}m`)
    );
}

/**
 * Create a fuel gauge with burning/unburned segments (for fire)
 * @param {Object} props
 * @param {number} props.totalKg - Total fuel kg
 * @param {number} props.burningKg - Currently burning fuel kg
 * @param {number} props.maxCapacityKg - Maximum capacity
 * @returns {HTMLElement}
 */
export function FuelGauge({ totalKg, burningKg, maxCapacityKg }) {
    const fillPct = (totalKg / maxCapacityKg) * 100;
    const burningPct = (burningKg / maxCapacityKg) * 100;

    return div({ className: 'fire-fuel-gauge' },
        div({ className: 'fire-gauge-label' },
            span({}, 'Fuel'),
            span({}, `${totalKg.toFixed(1)} / ${maxCapacityKg.toFixed(0)} kg`)
        ),
        div({ className: 'fire-gauge-bar' },
            div({
                className: 'fire-gauge-fill unburned',
                style: { width: `${fillPct}%` }
            }),
            div({
                className: 'fire-gauge-fill burning',
                style: { width: `${burningPct}%` }
            })
        )
    );
}
