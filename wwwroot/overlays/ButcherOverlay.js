import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';

/**
 * ButcherOverlay - Butchering interface with mode selection
 */
export class ButcherOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('butcherOverlay', inputHandler);

        // Get DOM elements
        this.animalEl = document.getElementById('butcherAnimal');
        this.statusEl = document.getElementById('butcherStatus');
        this.warningsEl = document.getElementById('butcherWarnings');
        this.modeOptionsEl = document.getElementById('butcherModeOptions');
        this.confirmBtn = document.getElementById('butcherConfirmBtn');
        this.confirmDesc = document.getElementById('butcherConfirmDesc');
        this.cancelBtn = document.getElementById('butcherCancelBtn');
    }

    render(butcherData, inputId) {
        this.show(inputId);

        // Carcass info
        this.animalEl.textContent = `${butcherData.animalName} carcass`;

        // Status
        let statusParts = [butcherData.decayStatus];
        if (butcherData.remainingKg > 0) {
            statusParts.push(`~${butcherData.remainingKg.toFixed(1)}kg remaining`);
        }
        if (butcherData.isFrozen) {
            statusParts.push('frozen');
        }
        this.statusEl.textContent = statusParts.join(', ');

        // Warnings
        this.clear(this.warningsEl);
        if (butcherData.warnings && butcherData.warnings.length > 0) {
            butcherData.warnings.forEach(warning => {
                const warnEl = this.createWarning(warning);
                this.warningsEl.appendChild(warnEl);
            });
            show(this.warningsEl);
        } else {
            hide(this.warningsEl);
        }

        // Mode options
        this.clear(this.modeOptionsEl);
        butcherData.modeOptions.forEach(mode => {
            const btn = this.createModeButton(mode);
            this.modeOptionsEl.appendChild(btn);
        });

        // Create form with one radio group field
        this.form = this.createForm({
            confirmBtn: this.confirmBtn,
            confirmDesc: this.confirmDesc
        });

        this.form.addRadioGroup('modeId', this.modeOptionsEl, 'modeId');

        // Action buttons
        this.confirmBtn.onclick = () => {
            if (this.form.isComplete()) {
                this.respond(this.form.getChoiceId());
            }
        };
        this.cancelBtn.onclick = () => this.respond('cancel');
    }

    createWarning(warning) {
        return this.createIconText('warning', warning, 'butcher-warning');
    }

    createModeButton(mode) {
        return this.createOptionButton({
            datasetKey: 'modeId',
            datasetValue: mode.id,
            label: mode.label,
            description: mode.description,
            meta: `~${mode.estimatedMinutes} min`
        });
    }

    cleanup() {
        this.form?.cleanup();
        this.clear(this.warningsEl);
        this.clear(this.modeOptionsEl);
    }
}
