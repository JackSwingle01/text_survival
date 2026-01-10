/**
 * Animated Stat Registry - Single source of truth for all stats that interpolate during progress
 */

// Stat type definitions
const StatTypes = {
    // Simple percentage bar (0-100)
    PERCENTAGE: 'percentage',

    // Temperature with custom range conversion
    TEMPERATURE: 'temperature',

    // Time formatting (minutes → clock display)
    CLOCK_TIME: 'clock_time',

    // Fire time with phase label
    FIRE_TIME: 'fire_time',

    // Percentage with custom display logic
    CUSTOM_PERCENTAGE: 'custom_percentage'
};

/**
 * Format clock time helper (moved from progress.js)
 */
function formatClockTime(totalMinutes) {
    totalMinutes = ((totalMinutes % 1440) + 1440) % 1440;
    const hours24 = Math.floor(totalMinutes / 60);
    const mins = Math.floor(totalMinutes % 60);
    const hours12 = hours24 % 12 || 12;
    const ampm = hours24 < 12 ? 'AM' : 'PM';
    return `${hours12}:${mins.toString().padStart(2, '0')} ${ampm}`;
}

/**
 * Animated Stat Registry
 * Each entry defines how a stat is captured, interpolated, and displayed
 */
export const AnimatedStatRegistry = {

    // Survival percentage stats
    healthPercent: {
        type: StatTypes.PERCENTAGE,
        capture: (state) => state.healthPercent,
        elements: {
            bar: 'healthBar',
            text: 'healthPct'
        }
    },

    foodPercent: {
        type: StatTypes.PERCENTAGE,
        capture: (state) => state.foodPercent,
        elements: {
            bar: 'foodBar',
            text: 'foodPct'
        }
    },

    waterPercent: {
        type: StatTypes.PERCENTAGE,
        capture: (state) => state.waterPercent,
        elements: {
            bar: 'waterBar',
            text: 'waterPct'
        }
    },

    energyPercent: {
        type: StatTypes.PERCENTAGE,
        capture: (state) => state.energyPercent,
        elements: {
            bar: 'energyBar',
            text: 'energyPct'
        }
    },

    // Body temperature (custom range: 95-98.6°F - hypothermia threshold to normal body temp)
    bodyTemp: {
        type: StatTypes.TEMPERATURE,
        capture: (state) => state.bodyTemp,
        elements: {
            text: 'bodyTempDisplay',
            bar: 'tempBar'
        },
        config: {
            minTemp: 95,    // SurvivalProcessor.HypothermiaThreshold
            maxTemp: 98.6,  // Body.BASE_BODY_TEMP
            decimals: 1
        }
    },

    // Clothing warmth percentage
    clothingWarmthPercent: {
        type: StatTypes.CUSTOM_PERCENTAGE,
        capture: (state) => state.clothingWarmthPercent,
        elements: {
            bar: 'clothingWarmthBar'
        }
    },

    // Clock time badge
    clockTimeMinutes: {
        type: StatTypes.CLOCK_TIME,
        capture: (state) => state.clockTimeMinutes,
        elements: {
            text: 'badgeTime'
        }
    },

    // Air temperature badge
    airTemp: {
        type: StatTypes.TEMPERATURE,
        capture: (state) => state.airTemp,
        elements: {
            text: 'badgeFeelsLike'
        },
        config: {
            decimals: 0,
            suffix: '°F'
        }
    },

    // Fire time remaining
    fireMinutesRemaining: {
        type: StatTypes.FIRE_TIME,
        capture: (state) => ({
            minutes: state.fire?.minutesRemaining || 0,
            phase: state.fire?.phase || ''
        }),
        elements: {
            text: 'firePhaseText'
        }
    }
};

/**
 * Stat Renderer - Applies interpolated values to DOM
 */
export const StatRenderer = {

    [StatTypes.PERCENTAGE]: (value, def) => {
        const { bar, text } = def.elements;

        if (bar) {
            const barEl = document.getElementById(bar);
            if (barEl) barEl.style.width = `${Math.max(0, Math.min(100, value))}%`;
        }

        if (text) {
            const textEl = document.getElementById(text);
            if (textEl) textEl.textContent = `${Math.round(value)}%`;
        }
    },

    [StatTypes.TEMPERATURE]: (value, def) => {
        const { text, bar } = def.elements;
        const { minTemp, maxTemp, decimals = 1, suffix = '°F' } = def.config || {};

        if (text) {
            const textEl = document.getElementById(text);
            if (textEl) textEl.textContent = `${value.toFixed(decimals)}${suffix}`;
        }

        if (bar && minTemp !== undefined && maxTemp !== undefined) {
            const barEl = document.getElementById(bar);
            if (barEl) {
                const pct = Math.max(0, Math.min(100, ((value - minTemp) / (maxTemp - minTemp)) * 100));
                barEl.style.width = `${pct}%`;
            }
        }
    },

    [StatTypes.CUSTOM_PERCENTAGE]: (value, def) => {
        const { bar } = def.elements;

        if (bar) {
            const barEl = document.getElementById(bar);
            if (barEl) barEl.style.width = `${Math.max(0, Math.min(100, value))}%`;
        }
    },

    [StatTypes.CLOCK_TIME]: (value, def) => {
        const { text } = def.elements;

        if (text) {
            const textEl = document.getElementById(text);
            if (textEl) textEl.textContent = formatClockTime(value);
        }
    },

    [StatTypes.FIRE_TIME]: (value, def) => {
        const { text } = def.elements;
        const { minutes, phase } = value;

        if (text && minutes > 0) {
            const textEl = document.getElementById(text);
            if (textEl) {
                const timeDisplay = minutes >= 60
                    ? `${Math.floor(minutes / 60)}hrs`
                    : `${Math.round(minutes)}min`;
                textEl.textContent = `${phase} — ${timeDisplay}`;
            }
        }
    }
};
