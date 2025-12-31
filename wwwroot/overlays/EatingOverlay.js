import { OverlayManager } from '../core/OverlayManager.js';
import { Animator } from '../core/Animator.js';
import { show, hide } from '../modules/utils.js';
import { ItemList } from '../components/ItemList.js';
import { ConsumableRowBuilder } from '../components/rowBuilders.js';

/**
 * EatingOverlay - Interactive eating and drinking UI with compact stats and organized sections
 */
export class EatingOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('eatingOverlay', inputHandler);

        this.statsEl = document.getElementById('eatingStats');
        this.itemsEl = document.getElementById('eatingItems');
        this.progressEl = document.getElementById('eatingProgress');
        this.progressBar = document.getElementById('eatingProgressBar');
        this.progressText = document.getElementById('eatingProgressText');
        this.progressResult = document.getElementById('eatingProgressResult');
        this.doneBtn = document.getElementById('eatingDoneBtn');

        this.pendingConsumption = false;
    }

    render(eatingData, inputId) {
        this.show(inputId);

        // Check if we're receiving result from a consumption
        if (this.pendingConsumption) {
            this.pendingConsumption = false;
            // Show success result
            this.progressResult.textContent = 'Consumed!';
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

        // Render compact stats row at top
        this.renderStats(eatingData);

        // Render consumable items in sections
        this.renderItems(eatingData);

        // Done button
        this.doneBtn.onclick = () => this.respond('done');
    }

    renderStats(eatingData) {
        this.clear(this.statsEl);

        // Compact horizontal stats row - no pane header needed
        const caloriesRow = this.createStatRow('Calories', `${eatingData.caloriesPercent}%`, {
            icon: 'restaurant',
            background: true
        });
        this.statsEl.appendChild(caloriesRow);

        const hydrationRow = this.createStatRow('Hydration', `${eatingData.hydrationPercent}%`, {
            icon: 'water_drop',
            background: true
        });
        this.statsEl.appendChild(hydrationRow);
    }

    renderItems(eatingData) {
        this.clear(this.itemsEl);

        // Build sections for foods, drinks, and special actions
        const sections = [];

        // Food section
        if (eatingData.foods && eatingData.foods.length > 0) {
            sections.push({
                header: { text: 'Food' },
                items: eatingData.foods.map(food => {
                    const item = {
                        id: food.id,
                        label: food.name,
                        amount: food.amount,
                        isAvailable: true
                    };

                    // Only add optional fields if they have values
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
                })
            });
        }

        // Drinks section
        if (eatingData.drinks && eatingData.drinks.length > 0) {
            sections.push({
                header: { text: 'Drinks' },
                items: eatingData.drinks.map(drink => {
                    const item = {
                        id: drink.id,
                        label: drink.name,
                        amount: drink.amount,
                        isAvailable: true
                    };

                    // Only add hydration estimate if present
                    if (drink.hydrationEstimate) {
                        item.estimate = `+${drink.hydrationEstimate} ml`;
                    }

                    return item;
                })
            });
        }

        // Special action section
        if (eatingData.specialAction) {
            sections.push({
                header: { text: 'Special' },
                items: [{
                    id: eatingData.specialAction.id,
                    label: eatingData.specialAction.name,
                    amount: eatingData.specialAction.amount,
                    isAvailable: true
                }]
            });
        }

        // Render using ItemList with progress animation on click
        const itemList = new ItemList({
            container: this.itemsEl,
            onItemClick: (item) => this.consumeWithProgress(item.id),
            rowBuilder: ConsumableRowBuilder
        });

        itemList.render(sections);
    }

    consumeWithProgress(itemId) {
        this.pendingConsumption = true;

        // Show progress bar
        show(this.progressEl);
        hide(this.progressResult);
        this.progressText.textContent = 'Eating...';

        // Disable buttons during animation
        this.doneBtn.disabled = true;
        const allButtons = this.overlay.querySelectorAll('.list-item--clickable');
        allButtons.forEach(btn => btn.style.pointerEvents = 'none');

        // Animate progress bar (~1.5 seconds)
        Animator.progressBar(this.progressBar, 1500, () => {
            // Animation complete - send response to backend
            this.respond(itemId);
        });
    }

    cleanup() {
        // Don't reset pendingConsumption here - it needs to survive hide/show cycle
        // Flag is reset in render() after showing the result
        this.clear(this.statsEl);
        this.clear(this.itemsEl);
        hide(this.progressEl);
        hide(this.progressResult);
    }
}
