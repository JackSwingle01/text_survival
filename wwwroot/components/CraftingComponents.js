// wwwroot/components/CraftingComponents.js
import { DOMBuilder, icon } from '../core/DOMBuilder.js';

/**
 * Reusable crafting UI components
 * Uses general component library classes (no craft-* specific classes)
 */
export class CraftingComponents {
    /**
     * Create a tab button for category navigation
     * @param {string} name - Display name
     * @param {string} iconName - Material Symbols icon name
     * @param {number} count - Number of craftable recipes (0 = no badge)
     * @param {boolean} isActive - Whether this tab is currently active
     * @param {Function} onClick - Click handler
     */
    static tab(name, iconName, count, isActive, onClick) {
        const classes = isActive ? 'tab tab--active' : 'tab';

        const builder = DOMBuilder.button(classes)
            .onClick(onClick)
            .append(
                icon(iconName),
                DOMBuilder.span().text(name)
            );

        // Add count badge if there are craftable recipes
        if (count > 0) {
            builder.append(
                DOMBuilder.span('badge badge--success')
                    .text(count.toString())
            );
        }

        return builder.build();
    }

    /**
     * Create a material requirement badge
     * @param {string} materialName - Name of the material
     * @param {number} available - Amount available
     * @param {number} required - Amount required
     * @param {boolean} isMet - Whether requirement is satisfied
     */
    static requirement(materialName, available, required, isMet) {
        const text = `${materialName}: ${available}/${required}`;
        const colorClass = isMet ? 'badge--success' : 'badge--danger';

        return DOMBuilder.span(`badge badge--sm badge--requirement ${colorClass}`)
            .text(text)
            .build();
    }

    /**
     * Create a tool requirement badge
     * @param {string} toolName - Name of the tool
     * @param {boolean} isAvailable - Whether tool is available
     * @param {boolean} isBroken - Whether tool is broken
     * @param {number|null} durability - Remaining durability (if available)
     */
    static toolRequirement(toolName, isAvailable, isBroken, durability = null) {
        let badgeClass, iconName, text;

        if (isBroken) {
            badgeClass = 'badge badge--danger';
            iconName = 'close';
            text = `${toolName} (broken)`;
        } else if (!isAvailable) {
            badgeClass = 'badge badge--danger';
            iconName = 'close';
            text = `${toolName} (missing)`;
        } else {
            badgeClass = 'badge badge--success';
            iconName = 'check';
            text = durability !== null ? `${toolName} (${durability} uses left)` : toolName;
        }

        return DOMBuilder.span(badgeClass)
            .append(
                icon(iconName),
                DOMBuilder.span().text(text)
            )
            .build();
    }
}
