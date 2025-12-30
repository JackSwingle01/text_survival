import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';

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
        const header = document.createElement('div');
        header.className = 'cooking-pane-header';
        const h3 = document.createElement('h3');

        const icon = document.createElement('span');
        icon.className = ICON_CLASS;
        icon.textContent = 'inventory_2';
        h3.appendChild(icon);
        h3.appendChild(document.createTextNode('Supplies'));

        header.appendChild(h3);
        this.statusEl.appendChild(header);

        // Supply items
        const statusItems = [
            { icon: 'water_drop', label: 'Water', value: `${cookingData.waterLiters.toFixed(1)}L` },
            { icon: 'lunch_dining', label: 'Raw Meat', value: `${cookingData.rawMeatKg.toFixed(1)}kg` },
            { icon: 'outdoor_grill', label: 'Cooked Meat', value: `${cookingData.cookedMeatKg.toFixed(1)}kg` }
        ];

        for (const item of statusItems) {
            const row = document.createElement('div');
            row.className = 'stat-row stat-row--bg';

            const iconSpan = document.createElement('span');
            iconSpan.className = ICON_CLASS;
            iconSpan.textContent = item.icon;
            row.appendChild(iconSpan);

            const label = document.createElement('span');
            label.className = 'stat-row__label';
            label.textContent = item.label;
            row.appendChild(label);

            const value = document.createElement('span');
            value.className = 'stat-row__value';
            value.textContent = item.value;
            row.appendChild(value);

            this.statusEl.appendChild(row);
        }
    }

    renderOptions(cookingData) {
        this.clear(this.optionsEl);

        // Header
        const header = document.createElement('div');
        header.className = 'cooking-pane-header';
        const h3 = document.createElement('h3');

        const icon = document.createElement('span');
        icon.className = ICON_CLASS;
        icon.textContent = 'skillet';
        h3.appendChild(icon);
        h3.appendChild(document.createTextNode('Actions'));

        header.appendChild(h3);
        this.optionsEl.appendChild(header);

        // Action options
        for (const opt of cookingData.options) {
            const btn = document.createElement('button');
            btn.className = 'cooking-option-btn' + (opt.isAvailable ? '' : ' disabled');
            btn.disabled = !opt.isAvailable;

            const iconSpan = document.createElement('span');
            iconSpan.className = ICON_CLASS;
            iconSpan.textContent = opt.icon;
            btn.appendChild(iconSpan);

            const labelSpan = document.createElement('span');
            labelSpan.className = 'cooking-option-label';
            labelSpan.textContent = opt.label;
            btn.appendChild(labelSpan);

            const timeSpan = document.createElement('span');
            timeSpan.className = 'cooking-option-time';
            timeSpan.textContent = `${opt.timeMinutes} min`;
            btn.appendChild(timeSpan);

            // Show disabled reason if unavailable
            if (!opt.isAvailable && opt.disabledReason) {
                const reasonSpan = document.createElement('span');
                reasonSpan.className = 'cooking-option-disabled';
                reasonSpan.textContent = opt.disabledReason;
                btn.appendChild(reasonSpan);
            }

            btn.onclick = () => this.respond(opt.id);
            this.optionsEl.appendChild(btn);
        }
    }

    renderResult(lastResult) {
        if (lastResult) {
            this.clear(this.resultEl);

            const icon = document.createElement('span');
            icon.className = ICON_CLASS;
            icon.textContent = lastResult.icon;
            this.resultEl.appendChild(icon);

            const msg = document.createElement('span');
            msg.textContent = lastResult.message;
            this.resultEl.appendChild(msg);

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
