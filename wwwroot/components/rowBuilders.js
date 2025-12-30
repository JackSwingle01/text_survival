/**
 * Pre-configured row builders for common itemlist patterns
 */

// Helper function for formatting weight
function formatWeight(weightKg) {
    return weightKg >= 1 ? `${weightKg.toFixed(1)}kg` : `${weightKg.toFixed(2)}kg`;
}

// Helper function for formatting burn time
function formatBurnTime(minutes) {
    if (minutes >= 60) {
        const hours = Math.floor(minutes / 60);
        const remaining = minutes % 60;
        return remaining > 0 ? `+${hours}h${remaining}m` : `+${hours}h`;
    }
    return `+${minutes}m`;
}

/**
 * Row builders for Fire overlay
 */
export const FireRowBuilders = {
    // Fire-starting tools (radio selection)
    tool: {
        type: 'radio',
        fields: [
            { key: 'displayName', element: 'label' },
            { key: 'successPercent', element: 'value', format: (v) => `${v}%` }
        ],
        selectedKey: 'isSelected'
    },

    // Tinder materials (radio selection)
    tinder: {
        type: 'radio',
        fields: [
            { key: 'displayName', element: 'label' },
            { key: 'count', element: 'meta', format: (v) => `(${v})` },
            { key: 'bonusPercent', element: 'value', format: (v) => `+${v}%` }
        ],
        selectedKey: 'isSelected'
    },

    // Fuel items (clickable with metadata)
    fuel: {
        type: 'action',
        icon: { key: 'icon', default: 'local_fire_department' },
        fields: [
            { key: 'displayName', element: 'label' },
            { key: 'count', element: 'meta', format: (v) => `x${v}` },
            { key: 'weightKg', element: 'meta', format: formatWeight },
            { key: 'burnTimeMinutes', element: 'meta', format: formatBurnTime }
        ],
        arrow: { icon: 'arrow_forward' },
        disabled: { key: 'canAdd', invert: true, reasonKey: 'disabledReason' }
    }
};

/**
 * Row builder for Transfer overlay
 * Note: arrow direction is set dynamically based on side (player vs storage)
 */
export const TransferRowBuilder = {
    type: 'action',
    icon: { key: 'icon' },
    fields: [
        { key: 'displayName', element: 'label' },
        { key: 'weightKg', element: 'meta', format: formatWeight }
    ],
    arrow: null  // Set dynamically when used
};
