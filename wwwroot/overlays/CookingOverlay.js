import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
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
        this.progressEl = document.getElementById('cookingProgress');
        this.progressBar = document.getElementById('cookingProgressBar');
        this.progressText = document.getElementById('cookingProgressText');
        this.progressResult = document.getElementById('cookingProgressResult');
        this.doneBtn = document.getElementById('cookingDoneBtn');
        this.resultTimeout = null;
        this.pendingCooking = false;
    }

    render(cookingData, inputId) {
        this.show(inputId);

        // Check if we're receiving result from a cooking action
        if (this.pendingCooking) {
            this.pendingCooking = false;
            // Show success result
            this.progressResult.textContent = 'Complete!';
            this.progressResult.className = 'overlay-progress-result success';
            show(this.progressResult);

            // Hide result after delay and reset progress bar
            setTimeout(() => {
                hide(this.progressEl);
                hide(this.progressResult);
                this.progressBar.style.width = '0%';
            }, 1500);
        } else {
            // Hide progress bar initially
            hide(this.progressEl);
            hide(this.progressResult);
            this.progressBar.style.width = '0%';
        }

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

        // Create wrapper for supplies
        const suppliesContainer = document.createElement('div');
        suppliesContainer.className = 'cooking-supplies';

        // Supply items as list items
        const supplyItems = [
            {
                icon: 'water_drop',
                displayName: 'Water',
                value: `${cookingData.waterLiters.toFixed(1)}L`
            },
            {
                icon: 'lunch_dining',
                displayName: 'Raw Meat',
                value: `${cookingData.rawMeatKg.toFixed(1)}kg`
            },
            {
                icon: 'outdoor_grill',
                displayName: 'Cooked Meat',
                value: `${cookingData.cookedMeatKg.toFixed(1)}kg`
            }
        ];

        // Row builder for supply display (non-clickable)
        const supplyRowBuilder = {
            type: 'display',
            icon: { key: 'icon' },
            fields: [
                { key: 'displayName', element: 'label' },
                { key: 'value', element: 'value' }
            ]
        };

        const itemList = new ItemList({
            container: suppliesContainer,
            onItemClick: null,  // Non-clickable
            rowBuilder: supplyRowBuilder
        });

        itemList.render([{ items: supplyItems }]);

        this.statusEl.appendChild(suppliesContainer);
    }

    renderOptions(cookingData) {
        this.clear(this.optionsEl);

        // Header
        const header = paneHeader({
            title: 'Actions',
            icon: 'skillet'
        });
        this.optionsEl.appendChild(header.build());

        // Create wrapper div for items
        const items = document.createElement('div');
        items.className = 'cooking-items';

        // Action options using ItemList
        const actionList = new ItemList({
            container: items,
            onItemClick: (opt) => this.cookWithProgress(opt.id, opt.timeMinutes),
            rowBuilder: CookingRowBuilder
        });

        actionList.render([{
            items: cookingData.options
        }]);

        // Append wrapper to pane
        this.optionsEl.appendChild(items);
    }

    cookWithProgress(choiceId, timeMinutes) {
        this.pendingCooking = true;
        show(this.progressEl);
        hide(this.progressResult);
        this.progressText.textContent = 'Cooking...';

        // Disable buttons during animation
        this.doneBtn.disabled = true;
        const allButtons = this.optionsEl.querySelectorAll('button');
        allButtons.forEach(btn => btn.disabled = true);

        // Calculate animation duration: game minutes / 7 = real seconds
        // Convert to milliseconds
        const durationMs = Math.max(1000, (timeMinutes / 7) * 1000);

        // Animate progress bar
        Animator.progressBar(this.progressBar, durationMs, () => {
            this.respond(choiceId);
        });
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
        hide(this.progressEl);
        hide(this.progressResult);
    }
}
