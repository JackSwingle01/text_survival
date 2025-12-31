import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder, paneHeader } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS } from '../modules/utils.js';
import { ItemList } from '../components/ItemList.js';
import { TransferRowBuilder } from '../components/rowBuilders.js';

/**
 * TransferOverlay - Bidirectional item transfer between player/storage
 */
export class TransferOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('transferOverlay', inputHandler);

        this.playerPane = document.getElementById('transferPlayerPane');
        this.storagePane = document.getElementById('transferStoragePane');
        this.doneBtn = document.getElementById('transferDoneBtn');
    }

    render(transfer, inputId) {
        this.show(inputId);

        // Render player pane
        this.renderPane(
            this.playerPane,
            transfer.playerTitle,
            transfer.playerCurrentWeightKg,
            transfer.playerMaxWeightKg,
            transfer.playerItems,
            'player'
        );

        // Render storage pane
        this.renderPane(
            this.storagePane,
            transfer.storageTitle,
            transfer.storageCurrentWeightKg,
            transfer.storageMaxWeightKg,
            transfer.storageItems,
            'storage'
        );

        // Done button
        this.doneBtn.onclick = () => this.respond('done');
    }

    renderPane(pane, title, currentWeight, maxWeight, items, side) {
        this.clear(pane);

        // Header using paneHeader helper
        const weightText = maxWeight > 0 && maxWeight < 500
            ? `${currentWeight.toFixed(1)} / ${maxWeight.toFixed(0)} kg`
            : `${currentWeight.toFixed(1)} kg`;

        const header = paneHeader({
            title: title,
            meta: weightText
        });
        pane.appendChild(header.build());

        // Items list
        const list = document.createElement('div');
        list.className = 'transfer-items';

        // Configure row builder with arrow direction based on side
        const builder = {
            ...TransferRowBuilder,
            arrow: { icon: side === 'player' ? 'arrow_forward' : 'arrow_back' }
        };

        // Use ItemList for rendering
        const itemList = new ItemList({
            container: list,
            onItemClick: (item) => this.sendTransfer(item.id, 1),
            rowBuilder: builder
        });

        // Build sections from categorized items
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
        this.inputHandler.sendAction(
            'transfer',
            {
                transferItemId: itemId,
                transferCount: count
            },
            this.inputId
        );
    }

    cleanup() {
        this.clear(this.playerPane);
        this.clear(this.storagePane);
    }
}
