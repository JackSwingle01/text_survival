// lib/components/StatRow.js
import { div, span } from '../helpers.js';
import { Icon } from './Icon.js';

/**
 * Create a simple icon + label + value row
 * @param {string} iconName - Material icon name
 * @param {string} label - Row label
 * @param {string|number} value - Row value
 * @param {string} [valueClass=''] - Additional CSS class for value
 * @returns {HTMLElement}
 */
export function SimpleStatRow(iconName, label, value, valueClass = '') {
    return div({ className: 'stat-row' },
        span({ className: 'stat-row__label' },
            Icon(iconName),
            label
        ),
        span({ className: `stat-row__value ${valueClass}`.trim() }, String(value))
    );
}

/**
 * Create an outcome summary item (for event outcomes)
 * @param {string} iconName - Material icon name
 * @param {string} text - Outcome text
 * @param {string} [styleClass=''] - CSS modifier class (time, damage, effect, gain, loss, stat, tension-up, tension-down)
 * @returns {HTMLElement}
 */
export function OutcomeItem(iconName, text, styleClass = '') {
    const normalizedClass = styleClass.startsWith('--') ? styleClass.slice(2) : styleClass;

    return div({ className: `outcome-item ${normalizedClass}`.trim() },
        Icon(iconName, 'outcome-item__icon'),
        span({ className: 'outcome-item__content' }, text)
    );
}

/**
 * Create an inventory item row
 * @param {string} label - Item label
 * @param {string|number} quantity - Quantity
 * @param {string} [styleClass=''] - Additional CSS class
 * @returns {HTMLElement}
 */
export function InventoryItem(label, quantity, styleClass = '') {
    return div({ className: `inv-item ${styleClass}`.trim() },
        span({}, label),
        span({ className: 'qty' }, String(quantity))
    );
}

/**
 * Create a fire stat row
 * @param {string} iconName - Material icon name
 * @param {string} label - Stat label
 * @param {string|number} value - Stat value
 * @param {string} [valueClass=''] - Additional CSS class for value
 * @returns {HTMLElement}
 */
export function FireStatRow(iconName, label, value, valueClass = '') {
    return div({ className: 'stat-row' },
        span({ className: 'stat-row__label' },
            Icon(iconName),
            label
        ),
        span({ className: `stat-row__value ${valueClass}`.trim() }, String(value))
    );
}

/**
 * Create a popup info item
 * @param {string} iconName - Material icon name
 * @param {string} text - Info text
 * @param {string} [typeClass=''] - CSS type class
 * @returns {HTMLElement}
 */
export function PopupItem(iconName, text, typeClass = '') {
    return div({ className: `popup-item ${typeClass}`.trim() },
        Icon(iconName),
        span({}, text)
    );
}

/**
 * Create a transfer item row (for inventory transfers)
 * @param {Object} item - Item data
 * @param {string} item.icon - Material icon name
 * @param {string} item.displayName - Display name
 * @param {number} item.weightKg - Weight in kg
 * @param {Function} onClick - Click handler
 * @param {string} [direction='forward'] - Arrow direction: 'forward' or 'back'
 * @returns {HTMLElement}
 */
export function TransferItem(item, onClick, direction = 'forward') {
    const weightText = item.weightKg >= 1
        ? `${item.weightKg.toFixed(1)}kg`
        : `${item.weightKg.toFixed(2)}kg`;

    return div({ className: 'transfer-item', onClick },
        Icon(item.icon, 'transfer-icon'),
        span({ className: 'transfer-item-name' }, item.displayName),
        span({ className: 'transfer-item-weight' }, weightText),
        Icon(direction === 'forward' ? 'arrow_forward' : 'arrow_back', 'transfer-arrow')
    );
}

/**
 * Create an equipment slot row
 * @param {string} label - Slot name (e.g., "Head", "Weapon")
 * @param {string|null} item - Item name or null if empty
 * @param {string|null} [stat=null] - Stat value (e.g., "12°C" for insulation)
 * @param {string} [statClass=''] - CSS class for stat
 * @returns {HTMLElement}
 */
export function EquipmentSlot(label, item, stat = null, statClass = '') {
    const isEmpty = !item;
    const classes = isEmpty ? 'list-item list-item--disabled' : 'list-item';

    const children = [
        span({ className: 'list-item__label' }, label),
        span(
            { className: isEmpty ? 'list-item__content text-faint' : 'list-item__content' },
            item || 'Empty'
        )
    ];

    if (!isEmpty && stat) {
        children.push(
            span({ className: `list-item__value ${statClass}`.trim() }, stat)
        );
    }

    return div({ className: classes }, ...children);
}

/**
 * Create a tool item row
 * @param {string} name - Tool name
 * @param {string|null} [damage=null] - Damage value
 * @param {number|null} [durability=null] - Current durability
 * @param {number|null} [maxDurability=null] - Max durability
 * @returns {HTMLElement}
 */
export function ToolItem(name, damage = null, durability = null, maxDurability = null) {
    const contentText = damage ? `${name} (${damage})` : name;

    const children = [
        span({ className: 'list-item__content' }, contentText)
    ];

    if (durability !== null && maxDurability !== null) {
        const pct = durability / maxDurability;
        if (pct < 0.3) {
            const warningClass = pct < 0.15 ? 'text-danger' : 'text-warning';
            children.push(
                span({ className: `list-item__meta ${warningClass}` }, `${durability} uses left`)
            );
        }
    }

    return div({ className: 'list-item' }, ...children);
}

/**
 * Create a resource item row
 * @param {string} label - Resource label
 * @param {string|number} quantity - Quantity
 * @returns {HTMLElement}
 */
export function ResourceItem(label, quantity) {
    return div({ className: 'stat-row' },
        span({ className: 'stat-row__label' }, label),
        span({ className: 'stat-row__value' }, String(quantity))
    );
}
