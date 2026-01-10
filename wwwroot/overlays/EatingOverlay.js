// overlays/EatingOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear, show, hide } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';
import { ItemList } from '../components/ItemList.js';
import { ConsumableRowBuilder } from '../components/rowBuilders.js';
import { Animator } from '../core/Animator.js';

/**
 * EatingOverlay - Eating and drinking UI with progress animations
 */
export class EatingOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('eatingOverlay', inputHandler);
        this.pendingConsumption = false;
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        const progressEl = this.$('#eatingProgress');
        const progressBar = this.$('#eatingProgressBar');
        const progressResult = this.$('#eatingProgressResult');
        const doneBtn = this.$('#eatingDoneBtn');

        // Handle progress completion
        if (this.pendingConsumption) {
            this.pendingConsumption = false;
            progressResult.textContent = 'Consumed!';
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
            doneBtn.disabled = false;
        }

        // Render stats
        this.renderStats(data);

        // Render items
        this.renderItems(data, progressEl, progressBar, this.$('#eatingProgressText'), doneBtn);

        // Done button
        doneBtn.onclick = () => this.respond('done');
    }

    renderStats(data) {
        const statsEl = this.$('#eatingStats');
        clear(statsEl);

        statsEl.appendChild(
            div({ className: 'stat-row stat-row--bg' },
                div({ className: 'stat-row__label' },
                    Icon('restaurant'),
                    'Calories'
                ),
                div({ className: 'stat-row__value' }, `${data.caloriesPercent}%`)
            )
        );

        statsEl.appendChild(
            div({ className: 'stat-row stat-row--bg' },
                div({ className: 'stat-row__label' },
                    Icon('water_drop'),
                    'Hydration'
                ),
                div({ className: 'stat-row__value' }, `${data.hydrationPercent}%`)
            )
        );
    }

    renderItems(data, progressEl, progressBar, progressText, doneBtn) {
        const itemsEl = this.$('#eatingItems');
        clear(itemsEl);

        const sections = [];

        // Food section
        if (data.foods && data.foods.length > 0) {
            sections.push({
                header: { text: 'Food' },
                items: data.foods.map(food => this.mapFoodItem(food))
            });
        }

        // Drinks section
        if (data.drinks && data.drinks.length > 0) {
            sections.push({
                header: { text: 'Drinks' },
                items: data.drinks.map(drink => this.mapDrinkItem(drink))
            });
        }

        // Special action section
        if (data.specialAction) {
            sections.push({
                header: { text: 'Special' },
                items: [{
                    id: data.specialAction.id,
                    label: data.specialAction.name,
                    amount: data.specialAction.amount,
                    isAvailable: true
                }]
            });
        }

        // Render using ItemList
        const itemList = new ItemList({
            container: itemsEl,
            onItemClick: (item) => this.consumeWithProgress(item.id, progressEl, progressBar, progressText, doneBtn, itemsEl),
            rowBuilder: ConsumableRowBuilder
        });

        itemList.render(sections);
    }

    mapFoodItem(food) {
        const item = {
            id: food.id,
            label: food.name,
            amount: food.amount,
            isAvailable: true
        };

        if (food.caloriesEstimate) {
            item.estimate = `~${food.caloriesEstimate} cal`;
        }
        if (food.hydrationEstimate) {
            item.hydrationNote = `${food.hydrationEstimate > 0 ? '+' : ''}${food.hydrationEstimate} ml`;
        }
        if (food.warning) {
            item.warning = food.warning;
        }

        return item;
    }

    mapDrinkItem(drink) {
        const item = {
            id: drink.id,
            label: drink.name,
            amount: drink.amount,
            isAvailable: true
        };

        if (drink.hydrationEstimate) {
            item.estimate = `+${drink.hydrationEstimate} ml`;
        }

        return item;
    }

    consumeWithProgress(itemId, progressEl, progressBar, progressText, doneBtn, itemsEl) {
        this.pendingConsumption = true;

        // Show progress bar
        show(progressEl);
        progressText.textContent = 'Eating...';

        // Disable buttons
        doneBtn.disabled = true;
        itemsEl.querySelectorAll('.list-item--clickable').forEach(el => {
            el.style.pointerEvents = 'none';
        });

        // Animate progress bar
        Animator.progressBar(progressBar, 1500, () => {
            this.respond(itemId);
        });
    }

    hide() {
        super.hide();
        const progressEl = this.$('#eatingProgress');
        const progressResult = this.$('#eatingProgressResult');
        if (progressEl) hide(progressEl);
        if (progressResult) hide(progressResult);
    }
}
