import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { CraftingComponents } from '../components/CraftingComponents.js';
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
            const shortName = this.getShortCategoryName(cat.categoryName);
            const icon = CRAFT_CATEGORY_ICONS[cat.categoryKey] || 'category';
            const count = cat.craftableRecipes ? cat.craftableRecipes.length : 0;
            const isActive = this.activeTab === cat.categoryKey;

            const tab = CraftingComponents.tab(
                shortName,
                icon,
                count,
                isActive,
                () => this.setTab(cat.categoryKey)
            );

            tab.dataset.category = cat.categoryKey;
            this.tabBar.appendChild(tab);
        });
    }

    getShortCategoryName(name) {
        return SHORT_CATEGORY_NAMES[name] || name;
    }

    setTab(categoryKey) {
        this.activeTab = categoryKey;

        // Update tab active states
        document.querySelectorAll('.tab').forEach(tab => {
            if (tab.dataset.category === categoryKey) {
                tab.classList.add('tab--active');
            } else {
                tab.classList.remove('tab--active');
            }
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
            const separator = DOMBuilder.div('section-header')
                .text('Needs materials:')
                .build();
            this.categoriesEl.appendChild(separator);

            category.uncraftableRecipes.forEach(recipe => {
                const row = this.createRecipeRow(recipe, false);
                this.categoriesEl.appendChild(row);
            });
        }

        // Empty state
        if ((!category.craftableRecipes || category.craftableRecipes.length === 0) &&
            (!category.uncraftableRecipes || category.uncraftableRecipes.length === 0)) {
            const empty = DOMBuilder.div('empty-state')
                .text('No recipes in this category.')
                .build();
            this.categoriesEl.appendChild(empty);
        }
    }

    createRecipeRow(recipe, isCraftable) {
        const classes = isCraftable ? 'list-item' : 'list-item list-item--disabled';

        // Build material requirements
        const requirementElements = recipe.requirements.map((req, i) => {
            const element = CraftingComponents.requirement(
                req.materialName,
                req.available,
                req.required,
                req.isMet
            );
            return element;
        });

        // Build tool requirements (if any)
        const toolElements = recipe.toolRequirements
            ? recipe.toolRequirements.map(tool => {
                return CraftingComponents.toolRequirement(
                    tool.toolName,
                    tool.isAvailable,
                    tool.isBroken,
                    tool.durability
                );
            })
            : [];

        // Build the row content
        const contentDiv = DOMBuilder.div('list-item__content')
            .append(
                DOMBuilder.div('section-header').text(recipe.name),
                // Requirements with comma separators
                DOMBuilder.div('flex gap-1')
                    .append(
                        ...requirementElements.flatMap((el, i) =>
                            i < requirementElements.length - 1
                                ? [el, document.createTextNode(', ')]
                                : [el]
                        )
                    )
                    .build()
            );

        // Add tool requirements if any
        if (toolElements.length > 0) {
            const toolContainer = DOMBuilder.div('flex gap-2')
                .append(...toolElements);
            contentDiv.append(toolContainer.build());
        }

        // Add time
        contentDiv.append(
            DOMBuilder.div('text-dim text-sm')
                .text(recipe.craftingTimeDisplay)
                .build()
        );

        // Build main row
        const row = DOMBuilder.div(classes)
            .append(contentDiv.build());

        // Add CRAFT button if craftable
        if (isCraftable && this.craftingInput && this.craftingInput.type === 'select') {
            // Find the matching choice by recipe name
            const matchingChoice = this.craftingInput.choices.find(choice =>
                choice.label.includes(recipe.name)
            );

            if (matchingChoice) {
                const craftBtn = DOMBuilder.button('btn btn--success')
                    .text('CRAFT')
                    .onClick(() => this.respond(matchingChoice.id))
                    .build();
                row.append(craftBtn);
            }
        }

        return row.build();
    }

    cleanup() {
        this.craftingData = null;
        this.craftingInput = null;
        this.activeTab = null;
        this.clear(this.tabBar);
        this.clear(this.categoriesEl);
    }
}
