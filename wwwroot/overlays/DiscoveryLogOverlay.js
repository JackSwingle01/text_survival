// overlays/DiscoveryLogOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear } from '../lib/helpers.js';

/**
 * DiscoveryLogOverlay - Shows player discoveries organized by category
 */
export class DiscoveryLogOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('discoveryLogOverlay', inputHandler);
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        const content = this.$('#discoveryLogContent');
        if (!content) return;
        clear(content);

        // Render each category
        if (data.categories) {
            data.categories.forEach(category => {
                content.appendChild(this.renderCategory(category));
            });
        }

        // Bind close button - respond with 'close' choice (same pattern as Inventory)
        const closeBtn = this.$('#discoveryLogCloseBtn');
        if (closeBtn) {
            closeBtn.onclick = () => this.respond('close');
        }
    }

    renderCategory(category) {
        const section = div({ className: 'discovery-category' });

        // Category header with count
        const header = div({ className: 'discovery-category__header' });
        header.appendChild(span({ className: 'discovery-category__name' }, category.name));
        header.appendChild(span({ className: 'discovery-category__count' }, category.countDisplay));
        section.appendChild(header);

        // Discovered items
        const items = div({ className: 'discovery-category__items' });

        if (category.discovered && category.discovered.length > 0) {
            category.discovered.forEach(item => {
                items.appendChild(
                    div({ className: 'discovery-item discovery-item--known' }, item)
                );
            });
        }

        // Unknown items (???)
        if (category.remainingCount > 0) {
            // Show up to 5 ??? entries, with a count if more
            const showCount = Math.min(category.remainingCount, 5);
            for (let i = 0; i < showCount; i++) {
                items.appendChild(
                    div({ className: 'discovery-item discovery-item--unknown' }, '???')
                );
            }
            if (category.remainingCount > 5) {
                items.appendChild(
                    div({ className: 'discovery-item discovery-item--more' },
                        `+${category.remainingCount - 5} more`)
                );
            }
        }

        section.appendChild(items);
        return section;
    }
}
