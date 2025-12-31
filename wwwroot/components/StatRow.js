// modules/components/StatRow.js
import { DOMBuilder, icon } from '../core/DOMBuilder.js';

/**
 * Reusable stat/item row component
 * Consolidates: addOutcomeItem, addInvItem, addFireStatRow, addPopupItem
 */
export class StatRow {
    /**
     * Create a simple icon + label + value row
     */
    static simple(iconName, label, value, valueClass = '') {
        return DOMBuilder.div('stat-row')
            .append(
                DOMBuilder.span('stat-row__label')
                    .append(icon(iconName), label),
                DOMBuilder.span(`stat-row__value ${valueClass}`.trim())
                    .text(value)
            )
            .build();
    }

    /**
     * Create an outcome summary item
     */
    static outcome(iconName, text, styleClass = '') {
        // Support both old modifier format (--time) and new class format (time)
        const normalizedClass = styleClass.startsWith('--') ? styleClass.slice(2) : styleClass;

        return DOMBuilder.div(`outcome-item ${normalizedClass}`)
            .append(
                icon(iconName, 'outcome-item__icon'),
                DOMBuilder.span('outcome-item__content').text(text)
            )
            .build();
    }

    /**
     * Create an inventory item row
     */
    static inventory(label, quantity, styleClass = '') {
        return DOMBuilder.div(`inv-item ${styleClass}`.trim())
            .append(
                DOMBuilder.span().text(label),
                DOMBuilder.span('qty').text(quantity)
            )
            .build();
    }

    /**
     * Create a fire stat row
     */
    static fire(iconName, label, value, valueClass = '') {
        return DOMBuilder.div('fire-stat-row')
            .append(
                DOMBuilder.span('fire-stat-label')
                    .append(icon(iconName), label),
                DOMBuilder.span(`fire-stat-value ${valueClass}`.trim())
                    .text(value)
            )
            .build();
    }

    /**
     * Create a popup info item
     */
    static popup(iconName, text, typeClass = '') {
        return DOMBuilder.div(`popup-item ${typeClass}`)
            .append(
                icon(iconName),
                DOMBuilder.span().text(text)
            )
            .build();
    }

    /**
     * Create a transfer item row
     */
    static transfer(item, onClick, direction = 'forward') {
        const row = DOMBuilder.div('transfer-item')
            .onClick(onClick)
            .append(
                icon(item.icon, 'transfer-icon'),
                DOMBuilder.span('transfer-item-name').text(item.displayName),
                DOMBuilder.span('transfer-item-weight').text(
                    item.weightKg >= 1
                        ? `${item.weightKg.toFixed(1)}kg`
                        : `${item.weightKg.toFixed(2)}kg`
                ),
                icon(direction === 'forward' ? 'arrow_forward' : 'arrow_back', 'transfer-arrow')
            );
        return row.build();
    }

    /**
     * Create an equipment slot (weapon/armor) using .list-item structure
     * @param {string} label - Slot name (e.g., "Head", "Weapon")
     * @param {string|null} item - Item name or null if empty
     * @param {string|null} stat - Stat value (e.g., "12°C" for insulation)
     * @param {string} statClass - CSS class for stat (e.g., "text-success")
     */
    static equipmentSlot(label, item, stat = null, statClass = '') {
        const isEmpty = !item;
        const classes = isEmpty ? 'list-item list-item--disabled' : 'list-item';

        const builder = DOMBuilder.div(classes)
            .append(
                DOMBuilder.span('list-item__label').text(label),
                DOMBuilder.span(isEmpty ? 'list-item__content text-faint' : 'list-item__content')
                    .text(item || 'Empty')
            );

        // Add stat if provided and slot is not empty
        if (!isEmpty && stat) {
            builder.append(
                DOMBuilder.span(`list-item__value ${statClass}`.trim()).text(stat)
            );
        }

        return builder.build();
    }

    /**
     * Create a tool item using .list-item structure
     * @param {string} name - Tool name
     * @param {string|null} damage - Damage value (e.g., "2d6") or null
     * @param {number|null} durability - Current durability
     * @param {number|null} maxDurability - Max durability
     */
    static tool(name, damage = null, durability = null, maxDurability = null) {
        // Build content text (name + damage if applicable)
        const contentText = damage ? `${name} (${damage})` : name;

        const builder = DOMBuilder.div('list-item')
            .append(
                DOMBuilder.span('list-item__content').text(contentText)
            );

        // Add durability warning if low
        if (durability !== null && maxDurability !== null) {
            const pct = durability / maxDurability;
            if (pct < 0.3) {
                const warningClass = pct < 0.15 ? 'text-danger' : 'text-warning';
                const usesLeft = durability;
                builder.append(
                    DOMBuilder.span(`list-item__meta ${warningClass}`)
                        .text(`${usesLeft} uses left`)
                );
            }
        }

        return builder.build();
    }

    /**
     * Create a resource item using .stat-row structure
     * @param {string} label - Resource label
     * @param {string} quantity - Quantity/amount
     */
    static resource(label, quantity) {
        return DOMBuilder.div('stat-row')
            .append(
                DOMBuilder.span('stat-row__label').text(label),
                DOMBuilder.span('stat-row__value').text(quantity)
            )
            .build();
    }
}