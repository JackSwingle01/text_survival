// overlays/TransferOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';
import { ItemList } from '../components/ItemList.js';
import { TransferRowBuilder } from '../components/rowBuilders.js';

/**
 * TransferOverlay - Bidirectional item transfer between player/storage
 */
export class TransferOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('transferOverlay', inputHandler);
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Render player pane
        this.renderPane(
            this.$('#transferPlayerPane'),
            data.playerTitle,
            data.playerCurrentWeightKg,
            data.playerMaxWeightKg,
            data.playerItems,
            'player'
        );

        // Render storage pane
        this.renderPane(
            this.$('#transferStoragePane'),
            data.storageTitle,
            data.storageCurrentWeightKg,
            data.storageMaxWeightKg,
            data.storageItems,
            'storage'
        );

        // Done button
        const doneBtn = this.$('#transferDoneBtn');
        if (doneBtn) doneBtn.onclick = () => this.respond('done');
    }

    renderPane(pane, title, currentWeight, maxWeight, items, side) {
        if (!pane) return;
        clear(pane);

        // Header with weight
        const weightText = maxWeight > 0 && maxWeight < 500
            ? `${currentWeight.toFixed(1)} / ${maxWeight.toFixed(0)} kg`
            : `${currentWeight.toFixed(1)} kg`;

        pane.appendChild(
            div({ className: 'pane-header' },
                span({ className: 'pane-header__title' }, title),
                span({ className: 'pane-header__meta' }, weightText)
            )
        );

        // Items list
        const list = div({ className: 'transfer-items' });

        const builder = {
            ...TransferRowBuilder,
            arrow: { icon: side === 'player' ? 'arrow_forward' : 'arrow_back' }
        };

        const itemList = new ItemList({
            container: list,
            onItemClick: (item) => this.sendTransfer(item.id, 1),
            rowBuilder: builder
        });

        const sections = this.buildCategorySections(items);
        itemList.render(sections);

        pane.appendChild(list);
    }

    buildCategorySections(items) {
        if (!items || items.length === 0) {
            return [{ emptyMessage: 'Empty' }];
        }

        const byCategory = this.groupItemsByCategory(items);
        return Object.entries(byCategory).map(([category, categoryItems]) => ({
            header: { text: category },
            items: categoryItems
        }));
    }

    groupItemsByCategory(items) {
        const groups = {};
        const order = ['Fuel', 'Food', 'Water', 'Materials', 'Medicinals', 'Tools', 'Carrying'];

        for (const item of items) {
            if (!groups[item.category]) {
                groups[item.category] = [];
            }
            groups[item.category].push(item);
        }

        // Return in specified order
        const ordered = {};
        for (const cat of order) {
            if (groups[cat]) {
                ordered[cat] = groups[cat];
            }
        }
        return ordered;
    }

    sendTransfer(itemId, count) {
        this.sendAction('transfer', {
            transferItemId: itemId,
            transferCount: count
        });
    }
}
