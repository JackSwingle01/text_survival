// overlays/CraftingOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, button, clear } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';

const CRAFT_CATEGORY_ICONS = {
    'FireStarting': 'local_fire_department',
    'CuttingTool': 'handyman',
    'HuntingWeapon': 'shield',
    'Trapping': 'trip_origin',
    'Processing': 'autorenew',
    'Treatment': 'healing',
    'Equipment': 'checkroom',
    'Lighting': 'light_mode',
    'Carrying': 'backpack',
    'CampInfrastructure': 'cabin',
    'Mending': 'home_repair_service'
};

const SHORT_CATEGORY_NAMES = {
    'Fire-Starting': 'Fire',
    'Cutting Tools': 'Cutting',
    'Hunting Weapons': 'Hunting',
    'Processing & Tools': 'Process',
    'Medical Treatments': 'Medical',
    'Clothing & Gear': 'Clothing',
    'Light Sources': 'Light',
    'Carrying Gear': 'Carry',
    'Camp Improvements': 'Camp',
    'Mend Equipment': 'Mend'
};

/**
 * CraftingOverlay - Tabbed crafting interface with recipes grouped by category
 */
export class CraftingOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('craftingOverlay', inputHandler);
        this.craftingData = null;
        this.craftingInput = null;
        this.activeTab = null;
    }

    render(data, inputId, input) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Store data for tab switching
        this.craftingData = data;
        this.craftingInput = input;

        // Set title
        const titleEl = this.$('#craftingTitle');
        if (titleEl) titleEl.textContent = data.title;

        // Build tab bar
        this.buildTabs(data.categories);

        // Select first tab with craftable recipes, or first tab
        const defaultTab = data.categories.find(c => c.craftableRecipes?.length > 0)
            || data.categories[0];

        if (defaultTab) {
            this.setTab(defaultTab.categoryKey);
        }

        // Handle close button
        const closeBtn = this.$('#craftingCloseBtn');
        if (closeBtn) {
            closeBtn.onclick = () => {
                if (input?.type === 'select') {
                    const cancelChoice = input.choices.find(c => c.label === 'Cancel');
                    this.respond(cancelChoice?.id || 'continue');
                } else {
                    this.respond('continue');
                }
            };
        }
    }

    buildTabs(categories) {
        const tabBar = this.$('#craftTabBar');
        if (!tabBar) return;
        clear(tabBar);

        categories.forEach(cat => {
            const shortName = SHORT_CATEGORY_NAMES[cat.categoryName] || cat.categoryName;
            const iconName = CRAFT_CATEGORY_ICONS[cat.categoryKey] || 'category';
            const count = cat.craftableRecipes?.length || 0;
            const isActive = this.activeTab === cat.categoryKey;

            const tab = this.buildTab(shortName, iconName, count, isActive, () => this.setTab(cat.categoryKey));
            tab.dataset.category = cat.categoryKey;
            tabBar.appendChild(tab);
        });
    }

    buildTab(name, iconName, count, isActive, onClick) {
        const children = [
            Icon(iconName),
            span({}, name)
        ];

        if (count > 0) {
            children.push(span({ className: 'badge badge--success' }, String(count)));
        }

        return button(
            {
                className: isActive ? 'tab tab--active' : 'tab',
                onClick
            },
            ...children
        );
    }

    setTab(categoryKey) {
        this.activeTab = categoryKey;

        // Update tab active states
        this.container.querySelectorAll('.tab').forEach(tab => {
            tab.classList.toggle('tab--active', tab.dataset.category === categoryKey);
        });

        // Find category data and render
        const category = this.craftingData.categories.find(c => c.categoryKey === categoryKey);
        if (category) {
            this.renderCategory(category);
        }
    }

    renderCategory(category) {
        const categoriesEl = this.$('#craftingCategories');
        if (!categoriesEl) return;
        clear(categoriesEl);

        // Craftable recipes first
        if (category.craftableRecipes?.length > 0) {
            category.craftableRecipes.forEach(recipe => {
                categoriesEl.appendChild(this.buildRecipeRow(recipe, true));
            });
        }

        // Uncraftable recipes
        if (category.uncraftableRecipes?.length > 0) {
            categoriesEl.appendChild(
                div({ className: 'section-header' }, 'Needs materials:')
            );

            category.uncraftableRecipes.forEach(recipe => {
                categoriesEl.appendChild(this.buildRecipeRow(recipe, false));
            });
        }

        // Empty state
        if (!category.craftableRecipes?.length && !category.uncraftableRecipes?.length) {
            categoriesEl.appendChild(
                div({ className: 'empty-state' }, 'No recipes in this category.')
            );
        }
    }

    buildRecipeRow(recipe, isCraftable) {
        const requirementBadges = recipe.requirements.map(req =>
            this.buildRequirementBadge(req.materialName, req.available, req.required, req.isMet)
        );

        const toolBadges = recipe.toolRequirements?.map(tool =>
            this.buildToolBadge(tool.toolName, tool.isAvailable, tool.isBroken, tool.durability)
        ) || [];

        const content = div({ className: 'list-item__content' },
            div({ className: 'section-header' }, recipe.name),
            div({ className: 'flex gap-2 flex-wrap' }, ...requirementBadges),
            toolBadges.length > 0 ? div({ className: 'flex gap-2' }, ...toolBadges) : null,
            div({ className: 'text-dim text-sm' }, recipe.craftingTimeDisplay)
        );

        const rowChildren = [content];

        // Add CRAFT button if craftable
        if (isCraftable && this.craftingInput?.type === 'select') {
            const matchingChoice = this.craftingInput.choices.find(choice =>
                choice.label.includes(recipe.name)
            );

            if (matchingChoice) {
                rowChildren.push(
                    button(
                        {
                            className: 'btn btn--success',
                            onClick: () => this.respond(matchingChoice.id)
                        },
                        'CRAFT'
                    )
                );
            }
        }

        return div(
            { className: isCraftable ? 'list-item' : 'list-item list-item--disabled' },
            ...rowChildren
        );
    }

    buildRequirementBadge(materialName, available, required, isMet) {
        const text = `${materialName}: ${available}/${required}`;
        const colorClass = isMet ? 'badge--success' : 'badge--danger';

        return span({ className: `badge badge--sm badge--requirement ${colorClass}` }, text);
    }

    buildToolBadge(toolName, isAvailable, isBroken, durability = null) {
        let badgeClass, iconName, text;

        if (isBroken) {
            badgeClass = 'badge badge--danger';
            iconName = 'close';
            text = `${toolName} (broken)`;
        } else if (!isAvailable) {
            badgeClass = 'badge badge--danger';
            iconName = 'close';
            text = `${toolName} (missing)`;
        } else {
            badgeClass = 'badge badge--success';
            iconName = 'check';
            text = durability !== null ? `${toolName} (${durability} uses left)` : toolName;
        }

        return span({ className: badgeClass },
            Icon(iconName),
            span({}, text)
        );
    }

    hide() {
        super.hide();
        this.craftingData = null;
        this.craftingInput = null;
        this.activeTab = null;
    }
}
