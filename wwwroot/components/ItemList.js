import { ICON_CLASS } from '../modules/utils.js';

/**
 * ItemList - Generic component for rendering categorized item lists
 * Uses existing generic CSS classes for consistent styling
 */
export class ItemList {
    constructor(config) {
        this.container = config.container;
        this.onItemClick = config.onItemClick;
        this.rowBuilder = config.rowBuilder;
    }

    /**
     * Render sections with categorized items
     * @param {Array} sections - Array of { header?, items, emptyMessage? }
     */
    render(sections) {
        this.clear();

        for (const section of sections) {
            // Render section header if provided
            if (section.header) {
                this.container.appendChild(this.buildHeader(section.header));
            }

            // Render items or empty state
            if (section.items && section.items.length > 0) {
                for (const item of section.items) {
                    this.container.appendChild(this.buildRow(item));
                }
            } else if (section.emptyMessage) {
                this.container.appendChild(this.buildEmptyState(section.emptyMessage));
            }
        }
    }

    /**
     * Build a section header
     */
    buildHeader(headerConfig) {
        const header = document.createElement('div');
        header.className = 'section-header';

        if (headerConfig.icon) {
            const icon = document.createElement('span');
            icon.className = ICON_CLASS;
            icon.textContent = headerConfig.icon;
            header.appendChild(icon);
        }

        header.appendChild(document.createTextNode(headerConfig.text));
        return header;
    }

    /**
     * Build an item row based on builder type
     */
    buildRow(item) {
        const builder = this.rowBuilder;

        if (builder.type === 'radio') {
            return this.buildRadioRow(item, builder);
        } else if (builder.type === 'action') {
            return this.buildActionRow(item, builder);
        }

        throw new Error(`Unknown row builder type: ${builder.type}`);
    }

    /**
     * Build a radio-button style row (for selection)
     */
    buildRadioRow(item, builder) {
        const isSelected = item[builder.selectedKey];

        const row = document.createElement('div');
        row.className = 'list-item list-item--clickable';
        if (isSelected) {
            row.classList.add('list-item--selected');
        }
        row.onclick = () => this.onItemClick(item);

        // Radio indicator
        const radio = document.createElement('span');
        radio.className = `list-item__radio ${ICON_CLASS}`;
        radio.textContent = isSelected ? 'radio_button_checked' : 'radio_button_unchecked';
        row.appendChild(radio);

        // Content container
        const content = document.createElement('div');
        content.className = 'list-item__content';

        // Fields
        for (const field of builder.fields) {
            const value = item[field.key];
            if (value === undefined) continue;

            const formatted = field.format ? field.format(value) : value;
            const span = document.createElement('span');
            span.className = `list-item__${field.element}`;
            span.textContent = formatted;
            content.appendChild(span);
        }

        row.appendChild(content);
        return row;
    }

    /**
     * Build an action-style row (clickable item with icon/arrow)
     */
    buildActionRow(item, builder) {
        const isDisabled = builder.disabled ?
            (builder.disabled.invert ? !item[builder.disabled.key] : item[builder.disabled.key]) :
            false;

        const row = document.createElement('div');
        row.className = 'list-item';
        if (!isDisabled) {
            row.classList.add('list-item--clickable');
            row.onclick = () => this.onItemClick(item);
        } else {
            row.classList.add('list-item--disabled');
        }

        // Icon (if configured)
        if (builder.icon) {
            const iconName = item[builder.icon.key] || builder.icon.default;
            const icon = document.createElement('span');
            icon.className = `list-item__icon ${ICON_CLASS}`;
            icon.textContent = iconName;
            row.appendChild(icon);
        }

        // Content container
        const content = document.createElement('div');
        content.className = 'list-item__content';

        // Fields
        for (const field of builder.fields) {
            const value = item[field.key];
            if (value === undefined) continue;

            const formatted = field.format ? field.format(value) : value;
            const span = document.createElement('span');
            span.className = `list-item__${field.element}`;
            span.textContent = formatted;
            content.appendChild(span);
        }

        row.appendChild(content);

        // Arrow or disabled reason
        if (isDisabled && builder.disabled?.reasonKey && item[builder.disabled.reasonKey]) {
            const reason = document.createElement('span');
            reason.className = 'list-item__reason';
            reason.textContent = item[builder.disabled.reasonKey];
            row.appendChild(reason);
        } else if (!isDisabled && builder.arrow) {
            const arrow = document.createElement('span');
            arrow.className = `list-item__arrow ${ICON_CLASS}`;
            arrow.textContent = builder.arrow.icon;
            row.appendChild(arrow);
        }

        return row;
    }

    /**
     * Build empty state message
     */
    buildEmptyState(message) {
        const empty = document.createElement('div');
        empty.className = 'list-empty';
        empty.textContent = message;
        return empty;
    }

    /**
     * Clear container
     */
    clear() {
        while (this.container.firstChild) {
            this.container.removeChild(this.container.firstChild);
        }
    }
}
