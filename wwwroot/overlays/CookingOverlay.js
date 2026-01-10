// overlays/CookingOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear, show, hide } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';
import { ItemList } from '../components/ItemList.js';
import { CookingRowBuilder } from '../components/rowBuilders.js';
import { Animator } from '../core/Animator.js';

/**
 * CookingOverlay - Cooking interface with supplies and action options
 */
export class CookingOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('cookingOverlay', inputHandler);
        this.resultTimeout = null;
        this.pendingCooking = false;
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        const progressEl = this.$('#cookingProgress');
        const progressBar = this.$('#cookingProgressBar');
        const progressResult = this.$('#cookingProgressResult');
        const doneBtn = this.$('#cookingDoneBtn');

        // Handle cooking completion
        if (this.pendingCooking) {
            this.pendingCooking = false;
            progressResult.textContent = 'Complete!';
            progressResult.className = 'overlay-progress-result success';
            show(progressResult);

            setTimeout(() => {
                hide(progressEl);
                hide(progressResult);
                progressBar.style.width = '0%';
            }, 1500);
        } else {
            hide(progressEl);
            hide(progressResult);
            progressBar.style.width = '0%';
        }

        // Clear previous result timeout
        if (this.resultTimeout) {
            clearTimeout(this.resultTimeout);
            this.resultTimeout = null;
        }

        // Render supplies and options
        this.renderSupplies(data);
        this.renderOptions(data, progressEl, progressBar, doneBtn);
        this.renderResult(data.lastResult);

        // Done button
        doneBtn.onclick = () => this.respond('done');
    }

    renderSupplies(data) {
        const statusEl = this.$('#cookingStatus');
        clear(statusEl);

        // Header
        statusEl.appendChild(this.buildPaneHeader('Supplies', 'inventory_2'));

        // Supplies container
        const container = div({ className: 'cooking-supplies' });

        const supplyItems = [
            { icon: 'water_drop', displayName: 'Water', value: `${data.waterLiters.toFixed(1)}L` },
            { icon: 'lunch_dining', displayName: 'Raw Meat', value: `${data.rawMeatKg.toFixed(1)}kg` },
            { icon: 'outdoor_grill', displayName: 'Cooked Meat', value: `${data.cookedMeatKg.toFixed(1)}kg` }
        ];

        const supplyRowBuilder = {
            type: 'display',
            icon: { key: 'icon' },
            fields: [
                { key: 'displayName', element: 'label' },
                { key: 'value', element: 'value' }
            ]
        };

        const itemList = new ItemList({
            container,
            onItemClick: null,
            rowBuilder: supplyRowBuilder
        });

        itemList.render([{ items: supplyItems }]);
        statusEl.appendChild(container);
    }

    renderOptions(data, progressEl, progressBar, doneBtn) {
        const optionsEl = this.$('#cookingOptions');
        clear(optionsEl);

        // Header
        optionsEl.appendChild(this.buildPaneHeader('Actions', 'skillet'));

        // Items container
        const items = div({ className: 'cooking-items' });

        const actionList = new ItemList({
            container: items,
            onItemClick: (opt) => this.cookWithProgress(opt.id, opt.timeMinutes, progressEl, progressBar, doneBtn, optionsEl),
            rowBuilder: CookingRowBuilder
        });

        actionList.render([{ items: data.options }]);
        optionsEl.appendChild(items);
    }

    buildPaneHeader(title, iconName) {
        return div({ className: 'pane-header' },
            Icon(iconName, 'pane-header__icon'),
            span({ className: 'pane-header__title' }, title)
        );
    }

    cookWithProgress(choiceId, timeMinutes, progressEl, progressBar, doneBtn, optionsEl) {
        this.pendingCooking = true;
        show(progressEl);
        hide(this.$('#cookingProgressResult'));
        this.$('#cookingProgressText').textContent = 'Cooking...';

        // Disable buttons
        doneBtn.disabled = true;
        optionsEl.querySelectorAll('button').forEach(btn => btn.disabled = true);

        // Animation duration
        const durationMs = Math.max(1000, (timeMinutes / 7) * 1000);

        Animator.progressBar(progressBar, durationMs, () => {
            this.respond(choiceId);
        });
    }

    renderResult(lastResult) {
        const resultEl = this.$('#cookingResult');

        if (lastResult) {
            clear(resultEl);
            resultEl.appendChild(
                div({ className: 'cooking-result-content' },
                    Icon(lastResult.icon),
                    span({}, lastResult.message)
                )
            );

            resultEl.className = 'cooking-result ' + (lastResult.isSuccess ? 'success' : 'failure');
            show(resultEl);

            this.resultTimeout = setTimeout(() => {
                hide(resultEl);
                this.resultTimeout = null;
            }, 2000);
        } else {
            hide(resultEl);
        }
    }

    hide() {
        super.hide();
        if (this.resultTimeout) {
            clearTimeout(this.resultTimeout);
            this.resultTimeout = null;
        }
        const progressEl = this.$('#cookingProgress');
        const resultEl = this.$('#cookingResult');
        if (progressEl) hide(progressEl);
        if (resultEl) hide(resultEl);
    }
}
