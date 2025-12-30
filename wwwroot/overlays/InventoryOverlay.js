import { OverlayManager } from '../core/OverlayManager.js';
import { Utils, show, hide } from '../modules/utils.js';
import { StatRow } from '../components/StatRow.js';
import { DOMBuilder } from '../core/DOMBuilder.js';

/**
 * InventoryOverlay - Inventory display with gear, fuel, food, materials sections
 */
export class InventoryOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('inventoryOverlay', inputHandler);

        this.titleEl = document.getElementById('inventoryTitle');
        this.weightEl = document.getElementById('inventoryWeight');
        this.actionsEl = document.getElementById('inventoryActions');
    }

    render(inv, inputId, input) {
        this.show(inputId);

        this.titleEl.textContent = inv.title;
        this.weightEl.textContent = `${inv.currentWeightKg.toFixed(1)} / ${inv.maxWeightKg.toFixed(0)} kg`;

        // Render each section
        this.renderGear(inv);
        this.renderFuel(inv);
        this.renderFood(inv);
        this.renderWater(inv);
        this.renderMaterials(inv);
        this.renderMedicinals(inv);

        // Render action buttons
        this.clear(this.actionsEl);

        if (input && input.type === 'select' && input.choices && input.choices.length > 0) {
            input.choices.forEach((choice, index) => {
                // Validate choice.id - FAIL LOUDLY if missing
                if (!choice.id || choice.id === '') {
                    console.error('[InventoryOverlay] INVALID choice at index', index,
                        '- missing or empty id:', choice,
                        '\nAll choices:', input.choices);
                    // Don't create button for invalid choice - this is a bug
                    return;
                }

                const btn = document.createElement('button');
                btn.className = 'btn';
                btn.textContent = choice.label;
                btn.onclick = () => this.respond(choice.id);
                this.actionsEl.appendChild(btn);
            });

            // If NO valid buttons were created, log error
            if (this.actionsEl.children.length === 0) {
                console.error('[InventoryOverlay] ERROR: No valid action buttons created!',
                    '\nInput:', input);
            }
        } else {
            // Input is invalid or missing - LOG ERROR
            console.error('[InventoryOverlay] ERROR: Invalid or missing input data!',
                '\nInput:', input);

            // Create error button that logs when clicked
            const errorBtn = document.createElement('button');
            errorBtn.className = 'btn';
            errorBtn.textContent = 'ERROR: Invalid Input Data';
            errorBtn.onclick = () => {
                console.error('[InventoryOverlay] Error button clicked - this should never happen');
            };
            this.actionsEl.appendChild(errorBtn);
        }
    }

    renderGear(inv) {
        const content = document.querySelector('#invGear .inv-content');
        this.clear(content);

        // Weapon slot
        const weaponStat = inv.weapon ? `${inv.weaponDamage?.toFixed(0) || 0} dmg` : null;
        content.appendChild(StatRow.equipmentSlot('Weapon', inv.weapon, weaponStat));

        // Armor slots
        const armorSlots = ['Head', 'Chest', 'Hands', 'Legs', 'Feet'];
        armorSlots.forEach(slotName => {
            const equipped = inv.armor?.find(a => a.slot === slotName);
            const insulationStat = equipped ? `+${(equipped.insulation * 100).toFixed(0)}%` : null;
            content.appendChild(StatRow.equipmentSlot(slotName, equipped?.name, insulationStat, 'text-success'));
        });

        // Tools list
        if (inv.tools && inv.tools.length > 0) {
            inv.tools.forEach(t => {
                // Find durability warning for this tool
                const warning = inv.toolWarnings?.find(w => w.name === t.name);
                const durability = warning?.durabilityRemaining;
                const maxDurability = t.maxDurability || 100; // Fallback if not provided

                content.appendChild(
                    StatRow.tool(t.name, t.damage ? `${t.damage.toFixed(0)} dmg` : null, durability, maxDurability)
                );
            });
        }

        // Total insulation
        if (inv.totalInsulation > 0) {
            content.appendChild(
                StatRow.resource('Total Insulation', `+${(inv.totalInsulation * 100).toFixed(0)}%`)
            );
        }
    }

    renderFuel(inv) {
        const content = document.querySelector('#invFuel .inv-content');
        this.clear(content);

        if (inv.fuel && inv.fuel.length > 0) {
            inv.fuel.forEach(f => content.appendChild(StatRow.resource(f.name, f.display)));
        } else {
            content.appendChild(DOMBuilder.div('empty-state').text('No fuel').build());
        }
    }

    renderFood(inv) {
        const content = document.querySelector('#invFood .inv-content');
        this.clear(content);

        if (inv.food && inv.food.length > 0) {
            inv.food.forEach(f => content.appendChild(StatRow.resource(f.name, f.display)));
        } else {
            content.appendChild(DOMBuilder.div('empty-state').text('No food').build());
        }
    }

    renderWater(inv) {
        const content = document.querySelector('#invWater .inv-content');
        this.clear(content);

        if (inv.water && inv.water > 0) {
            content.appendChild(StatRow.resource('Water', `${inv.water.toFixed(1)}L`));
        } else {
            content.appendChild(DOMBuilder.div('empty-state').text('No water').build());
        }
    }

    renderMaterials(inv) {
        const content = document.querySelector('#invMaterials .inv-content');
        this.clear(content);

        if (inv.materials && inv.materials.length > 0) {
            inv.materials.forEach(m => content.appendChild(StatRow.resource(m.name, m.display)));
        } else {
            content.appendChild(DOMBuilder.div('empty-state').text('No materials').build());
        }
    }

    renderMedicinals(inv) {
        const content = document.querySelector('#invMedicinals .inv-content');
        this.clear(content);

        if (inv.medicinals && inv.medicinals.length > 0) {
            inv.medicinals.forEach(m => content.appendChild(StatRow.resource(m.name, m.display)));
        } else {
            content.appendChild(DOMBuilder.div('empty-state').text('No medicinals').build());
        }
    }

    cleanup() {
        this.clear(this.actionsEl);
    }
}
