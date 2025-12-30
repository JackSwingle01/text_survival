import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';

const CRAFT_CATEGORY_ICONS = {
    'FireStarting': 'local_fire_department',
    'CuttingTool': 'content_cut',
    'HuntingWeapon': 'gps_fixed',
    'Trapping': 'trip_origin',
    'Processing': 'build',
    'Treatment': 'healing',
    'Equipment': 'checkroom',
    'Lighting': 'flare',
    'Carrying': 'backpack',
    'CampInfrastructure': 'house',
    'Mending': 'build_circle'
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
export class CraftingOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('craftingOverlay', inputHandler);

        this.titleEl = document.getElementById('craftingTitle');
        this.tabBar = document.getElementById('craftTabBar');
        this.categoriesEl = document.getElementById('craftingCategories');
        this.closeBtn = document.getElementById('craftingCloseBtn');

        this.craftingData = null;
        this.craftingInput = null;
        this.activeTab = null;
    }

    render(crafting, inputId, input) {
        this.show(inputId);

        // Store data for tab switching
        this.craftingData = crafting;
        this.craftingInput = input;

        // Set title
        this.titleEl.textContent = crafting.title;

        // Build tab bar
        this.buildTabs(crafting.categories);

        // Select first tab with craftable recipes, or first tab
        const defaultTab = crafting.categories.find(c => c.craftableRecipes && c.craftableRecipes.length > 0)
            || crafting.categories[0];

        if (defaultTab) {
            this.setTab(defaultTab.categoryKey);
        }

        // Handle close button
        this.closeBtn.onclick = () => {
            if (input && input.type === 'select') {
                const cancelChoice = input.choices.find(c => c.label === 'Cancel');
                if (cancelChoice) {
                    this.respond(cancelChoice.id);
                } else {
                    this.respond('continue');
                }
            } else {
                this.respond('continue');
            }
        };
    }

    buildTabs(categories) {
        this.clear(this.tabBar);

        categories.forEach(cat => {
            const tab = document.createElement('button');
            tab.className = 'craft-tab';
            tab.dataset.category = cat.categoryKey;

            // Icon
            const icon = document.createElement('span');
            icon.className = ICON_CLASS;
            icon.textContent = CRAFT_CATEGORY_ICONS[cat.categoryKey] || 'category';
            tab.appendChild(icon);

            // Short label
            const label = document.createElement('span');
            label.textContent = this.getShortCategoryName(cat.categoryName);
            tab.appendChild(label);

            // Badge if has craftable recipes
            if (cat.craftableRecipes && cat.craftableRecipes.length > 0) {
                const badge = document.createElement('span');
                badge.className = 'craft-tab-badge';
                badge.textContent = cat.craftableRecipes.length;
                tab.appendChild(badge);
            }

            tab.onclick = () => this.setTab(cat.categoryKey);
            this.tabBar.appendChild(tab);
        });
    }

    getShortCategoryName(name) {
        return SHORT_CATEGORY_NAMES[name] || name;
    }

    setTab(categoryKey) {
        this.activeTab = categoryKey;

        // Update tab active states
        document.querySelectorAll('.craft-tab').forEach(tab => {
            tab.classList.toggle('active', tab.dataset.category === categoryKey);
        });

        // Find category data and render
        const category = this.craftingData.categories
            .find(c => c.categoryKey === categoryKey);

        if (category) {
            this.renderCategory(category);
        }
    }

    renderCategory(category) {
        this.clear(this.categoriesEl);

        // Craftable recipes first
        if (category.craftableRecipes && category.craftableRecipes.length > 0) {
            category.craftableRecipes.forEach(recipe => {
                const row = this.createRecipeRow(recipe, true);
                this.categoriesEl.appendChild(row);
            });
        }

        // Uncraftable recipes
        if (category.uncraftableRecipes && category.uncraftableRecipes.length > 0) {
            const separator = document.createElement('div');
            separator.className = 'uncraftable-separator';
            separator.textContent = 'Needs materials:';
            this.categoriesEl.appendChild(separator);

            category.uncraftableRecipes.forEach(recipe => {
                const row = this.createRecipeRow(recipe, false);
                this.categoriesEl.appendChild(row);
            });
        }

        // Empty state
        if ((!category.craftableRecipes || category.craftableRecipes.length === 0) &&
            (!category.uncraftableRecipes || category.uncraftableRecipes.length === 0)) {
            const empty = document.createElement('div');
            empty.className = 'empty-category';
            empty.textContent = 'No recipes in this category.';
            this.categoriesEl.appendChild(empty);
        }
    }

    createRecipeRow(recipe, isCraftable) {
        const row = document.createElement('div');
        row.className = `recipe-row ${isCraftable ? 'craftable' : 'uncraftable'}`;

        const info = document.createElement('div');
        info.className = 'recipe-info';

        // Name
        const name = document.createElement('div');
        name.className = 'recipe-name';
        name.textContent = recipe.name;
        info.appendChild(name);

        // Requirements
        const requirements = document.createElement('div');
        requirements.className = 'recipe-requirements';

        recipe.requirements.forEach((req, i) => {
            const reqSpan = document.createElement('span');
            reqSpan.className = `requirement ${req.isMet ? 'met' : 'unmet'}`;
            reqSpan.textContent = `${req.materialName}: ${req.available}/${req.required}`;
            requirements.appendChild(reqSpan);

            if (i < recipe.requirements.length - 1) {
                requirements.appendChild(document.createTextNode(', '));
            }
        });
        info.appendChild(requirements);

        // Tool requirements (if any)
        if (recipe.toolRequirements && recipe.toolRequirements.length > 0) {
            const toolReqs = document.createElement('div');
            toolReqs.className = 'recipe-tool-requirements';

            recipe.toolRequirements.forEach((tool, i) => {
                const toolSpan = document.createElement('span');
                const icon = document.createElement('span');
                icon.className = ICON_CLASS;

                if (!tool.isAvailable) {
                    toolSpan.className = 'tool-requirement missing';
                    icon.textContent = 'close';
                    toolSpan.appendChild(icon);
                    toolSpan.appendChild(document.createTextNode(`${tool.toolName} (required)`));
                } else if (tool.isBroken) {
                    toolSpan.className = 'tool-requirement broken';
                    icon.textContent = 'close';
                    toolSpan.appendChild(icon);
                    toolSpan.appendChild(document.createTextNode(`${tool.toolName} (broken)`));
                } else {
                    toolSpan.className = 'tool-requirement available';
                    icon.textContent = 'check';
                    toolSpan.appendChild(icon);
                    toolSpan.appendChild(document.createTextNode(`${tool.toolName} (${tool.durability} uses left)`));
                }

                toolReqs.appendChild(toolSpan);

                if (i < recipe.toolRequirements.length - 1) {
                    toolReqs.appendChild(document.createTextNode(', '));
                }
            });

            info.appendChild(toolReqs);
        }

        // Time
        const time = document.createElement('div');
        time.className = 'recipe-time';
        time.textContent = recipe.craftingTimeDisplay;
        info.appendChild(time);

        row.appendChild(info);

        // Add CRAFT button if craftable
        if (isCraftable && this.craftingInput && this.craftingInput.type === 'select') {
            const craftBtn = document.createElement('button');
            craftBtn.className = 'btn btn--success';
            craftBtn.textContent = 'CRAFT';

            // Find the matching choice by recipe name
            const matchingChoice = this.craftingInput.choices.find(choice =>
                choice.label.includes(recipe.name)
            );

            if (matchingChoice) {
                craftBtn.onclick = () => this.respond(matchingChoice.id);
            } else {
                craftBtn.disabled = true;
            }

            row.appendChild(craftBtn);
        }

        return row;
    }

    cleanup() {
        this.craftingData = null;
        this.craftingInput = null;
        this.activeTab = null;
        this.clear(this.tabBar);
        this.clear(this.categoriesEl);
    }
}
