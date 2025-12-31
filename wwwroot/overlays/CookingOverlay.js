import { OverlayManager } from '../core/OverlayManager.js';
import { paneHeader } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';
import { ItemList } from '../components/ItemList.js';
import { CookingRowBuilder } from '../components/rowBuilders.js';

/**
 * CookingOverlay - Cooking interface with supplies and action options
 */
export class CookingOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('cookingOverlay', inputHandler);

        this.statusEl = document.getElementById('cookingStatus');
        this.optionsEl = document.getElementById('cookingOptions');
        this.resultEl = document.getElementById('cookingResult');
        this.doneBtn = document.getElementById('cookingDoneBtn');
        this.resultTimeout = null;
    }

    render(cookingData, inputId) {
        this.show(inputId);

        // Clear previous result timeout
        if (this.resultTimeout) {
            clearTimeout(this.resultTimeout);
            this.resultTimeout = null;
        }

        // Render left pane: supplies
        this.renderSupplies(cookingData);

        // Render right pane: action options
        this.renderOptions(cookingData);

        // Show result feedback if present
        this.renderResult(cookingData.lastResult);

        // Done button
        this.doneBtn.onclick = () => this.respond('done');
    }

    renderSupplies(cookingData) {
        this.clear(this.statusEl);

        // Header
        const header = paneHeader({
            title: 'Supplies',
            icon: 'inventory_2'
        });
        this.statusEl.appendChild(header.build());

        // Supply items
        const statusItems = [
            { icon: 'water_drop', label: 'Water', value: `${cookingData.waterLiters.toFixed(1)}L` },
            { icon: 'lunch_dining', label: 'Raw Meat', value: `${cookingData.rawMeatKg.toFixed(1)}kg` },
            { icon: 'outdoor_grill', label: 'Cooked Meat', value: `${cookingData.cookedMeatKg.toFixed(1)}kg` }
        ];

        for (const item of statusItems) {
            const row = this.createStatRow(item.label, item.value, {
                icon: item.icon,
                background: true
            });
            this.statusEl.appendChild(row);
        }
    }

    renderOptions(cookingData) {
        this.clear(this.optionsEl);

        // Header
        const header = paneHeader({
            title: 'Actions',
            icon: 'skillet'
        });
        this.optionsEl.appendChild(header.build());

        // Action options using ItemList
        const actionList = new ItemList({
            container: this.optionsEl,
            onItemClick: (opt) => this.respond(opt.id),
            rowBuilder: CookingRowBuilder
        });

        actionList.render([{
            items: cookingData.options
        }]);
    }

    renderResult(lastResult) {
        if (lastResult) {
            this.clear(this.resultEl);

            const resultContent = this.createIconText(lastResult.icon, lastResult.message);
            this.resultEl.appendChild(resultContent);

            this.resultEl.className = 'cooking-result ' + (lastResult.isSuccess ? 'success' : 'failure');
            show(this.resultEl);

            // Auto-hide after delay
            this.resultTimeout = setTimeout(() => {
                hide(this.resultEl);
                this.resultTimeout = null;
            }, 2000);
        } else {
            hide(this.resultEl);
        }
    }

    cleanup() {
        if (this.resultTimeout) {
            clearTimeout(this.resultTimeout);
            this.resultTimeout = null;
        }
        this.clear(this.statusEl);
        this.clear(this.optionsEl);
        hide(this.resultEl);
    }
}
