// overlays/InventoryOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, button, clear } from '../lib/helpers.js';
import {
    EquipmentSlot,
    ToolItem,
    ResourceItem
} from '../lib/components/StatRow.js';

/**
 * InventoryOverlay - Inventory display with gear, fuel, food, materials sections
 */
export class InventoryOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('inventoryOverlay', inputHandler);
    }

    render(data, inputId, input) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Title and weight
        const titleEl = this.$('#inventoryTitle');
        if (titleEl) titleEl.textContent = data.title;

        const weightEl = this.$('#inventoryWeight');
        if (weightEl) {
            weightEl.textContent = `${data.currentWeightKg.toFixed(1)} / ${data.maxWeightKg.toFixed(0)} kg`;
        }

        // Render sections
        this.renderGear(data);
        this.renderFuel(data);
        this.renderFood(data);
        this.renderWater(data);
        this.renderMaterials(data);
        this.renderMedicinals(data);

        // Render action buttons
        this.renderActions(input);
    }

    renderGear(data) {
        const content = this.container.querySelector('#invGear .inv-content');
        if (!content) return;
        clear(content);

        // Weapon slot
        const weaponStat = data.weapon ? `${data.weaponDamage?.toFixed(0) || 0} dmg` : null;
        content.appendChild(EquipmentSlot('Weapon', data.weapon, weaponStat));

        // Armor slots
        const armorSlots = ['Head', 'Chest', 'Hands', 'Legs', 'Feet'];
        armorSlots.forEach(slotName => {
            const equipped = data.armor?.find(a => a.slot === slotName);
            const insulationStat = equipped ? `+${(equipped.insulation * 100).toFixed(0)}%` : null;
            content.appendChild(EquipmentSlot(slotName, equipped?.name, insulationStat, 'text-success'));
        });

        // Tools list
        if (data.tools && data.tools.length > 0) {
            data.tools.forEach(t => {
                const warning = data.toolWarnings?.find(w => w.name === t.name);
                const durability = warning?.durabilityRemaining;
                const maxDurability = t.maxDurability || 100;

                content.appendChild(
                    ToolItem(t.name, t.damage ? `${t.damage.toFixed(0)} dmg` : null, durability, maxDurability)
                );
            });
        }

        // Total insulation
        if (data.totalInsulation > 0) {
            content.appendChild(
                ResourceItem('Total Insulation', `+${(data.totalInsulation * 100).toFixed(0)}%`)
            );
        }
    }

    renderFuel(data) {
        const content = this.container.querySelector('#invFuel .inv-content');
        if (!content) return;
        clear(content);

        if (data.fuel && data.fuel.length > 0) {
            data.fuel.forEach(f => content.appendChild(ResourceItem(f.name, f.display)));
        } else {
            content.appendChild(div({ className: 'empty-state' }, 'No fuel'));
        }
    }

    renderFood(data) {
        const content = this.container.querySelector('#invFood .inv-content');
        if (!content) return;
        clear(content);

        if (data.food && data.food.length > 0) {
            data.food.forEach(f => content.appendChild(ResourceItem(f.name, f.display)));
        } else {
            content.appendChild(div({ className: 'empty-state' }, 'No food'));
        }
    }

    renderWater(data) {
        const content = this.container.querySelector('#invWater .inv-content');
        if (!content) return;
        clear(content);

        if (data.water && data.water > 0) {
            content.appendChild(ResourceItem('Water', `${data.water.toFixed(1)}L`));
        } else {
            content.appendChild(div({ className: 'empty-state' }, 'No water'));
        }
    }

    renderMaterials(data) {
        const content = this.container.querySelector('#invMaterials .inv-content');
        if (!content) return;
        clear(content);

        if (data.materials && data.materials.length > 0) {
            data.materials.forEach(m => content.appendChild(ResourceItem(m.name, m.display)));
        } else {
            content.appendChild(div({ className: 'empty-state' }, 'No materials'));
        }
    }

    renderMedicinals(data) {
        const content = this.container.querySelector('#invMedicinals .inv-content');
        if (!content) return;
        clear(content);

        if (data.medicinals && data.medicinals.length > 0) {
            data.medicinals.forEach(m => content.appendChild(ResourceItem(m.name, m.display)));
        } else {
            content.appendChild(div({ className: 'empty-state' }, 'No medicinals'));
        }
    }

    renderActions(input) {
        const actionsEl = this.$('#inventoryActions');
        if (!actionsEl) return;
        clear(actionsEl);

        if (input?.type === 'select' && input.choices?.length > 0) {
            input.choices.forEach((choice, index) => {
                if (!choice.id || choice.id === '') {
                    console.error('[InventoryOverlay] Invalid choice at index', index, '- missing id:', choice);
                    return;
                }

                actionsEl.appendChild(
                    button(
                        { className: 'btn', onClick: () => this.respond(choice.id) },
                        choice.label
                    )
                );
            });

            if (actionsEl.children.length === 0) {
                console.error('[InventoryOverlay] No valid action buttons created!');
            }
        } else {
            console.error('[InventoryOverlay] Invalid or missing input data:', input);
            actionsEl.appendChild(
                button({ className: 'btn', disabled: true }, 'ERROR: Invalid Input Data')
            );
        }
    }
}
