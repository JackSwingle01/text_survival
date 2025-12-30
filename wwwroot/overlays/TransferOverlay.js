import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS } from '../modules/utils.js';

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

        // Header
        const header = document.createElement('div');
        header.className = 'transfer-pane-header';

        const titleEl = document.createElement('h3');
        titleEl.textContent = title;
        header.appendChild(titleEl);

        const weightEl = document.createElement('span');
        weightEl.className = 'transfer-weight';
        if (maxWeight > 0 && maxWeight < 500) {
            weightEl.textContent = `${currentWeight.toFixed(1)} / ${maxWeight.toFixed(0)} kg`;
        } else {
            weightEl.textContent = `${currentWeight.toFixed(1)} kg`;
        }
        header.appendChild(weightEl);
        pane.appendChild(header);

        // Items list
        const list = document.createElement('div');
        list.className = 'transfer-items';

        if (items.length > 0) {
            // Group by category
            const byCategory = this.groupItemsByCategory(items);

            for (const [category, categoryItems] of Object.entries(byCategory)) {
                const catHeader = document.createElement('div');
                catHeader.className = 'transfer-category-header';
                catHeader.textContent = category;
                list.appendChild(catHeader);

                for (const item of categoryItems) {
                    const row = this.createItemRow(item, side);
                    list.appendChild(row);
                }
            }
        } else {
            const empty = document.createElement('div');
            empty.className = 'transfer-empty';
            empty.textContent = 'Empty';
            list.appendChild(empty);
        }

        pane.appendChild(list);
    }

    createItemRow(item, side) {
        const row = document.createElement('div');
        row.className = 'transfer-item';
        row.onclick = () => this.sendTransfer(item.id, item.isAggregated ? item.count : 1);

        // Icon
        const icon = document.createElement('span');
        icon.className = `${ICON_CLASS} transfer-icon`;
        icon.textContent = item.icon;
        row.appendChild(icon);

        // Name
        const name = document.createElement('span');
        name.className = 'transfer-item-name';
        name.textContent = item.displayName;
        row.appendChild(name);

        // Weight
        const weight = document.createElement('span');
        weight.className = 'transfer-item-weight';
        weight.textContent = item.weightKg >= 1
            ? `${item.weightKg.toFixed(1)}kg`
            : `${item.weightKg.toFixed(2)}kg`;
        row.appendChild(weight);

        // Transfer arrow indicator
        const arrow = document.createElement('span');
        arrow.className = `transfer-arrow ${ICON_CLASS}`;
        arrow.textContent = side === 'player' ? 'arrow_forward' : 'arrow_back';
        row.appendChild(arrow);

        return row;
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
