// overlays/ButcherOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear, show, hide } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';
import { MultiStepForm } from '../lib/components/RadioGroup.js';

/**
 * ButcherOverlay - Butchering interface with mode selection
 */
export class ButcherOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('butcherOverlay', inputHandler);
        this.form = null;
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Carcass info
        const animalEl = this.$('#butcherAnimal');
        if (animalEl) animalEl.textContent = `${data.animalName} carcass`;

        // Status
        const statusEl = this.$('#butcherStatus');
        if (statusEl) {
            const parts = [data.decayStatus];
            if (data.remainingKg > 0) {
                parts.push(`~${data.remainingKg.toFixed(1)}kg remaining`);
            }
            if (data.isFrozen) {
                parts.push('frozen');
            }
            statusEl.textContent = parts.join(', ');
        }

        // Warnings
        this.renderWarnings(data.warnings);

        // Mode options
        const modeEl = this.$('#butcherModeOptions');
        const confirmBtn = this.$('#butcherConfirmBtn');
        const confirmDesc = this.$('#butcherConfirmDesc');

        // Clean up old form
        this.form?.destroy();

        // Create new form
        this.form = new MultiStepForm({
            confirmBtn,
            confirmDesc
        });

        // Add mode options
        this.form.addField(
            'mode',
            modeEl,
            data.modeOptions.map(m => ({
                id: m.id,
                label: m.label,
                description: m.description,
                meta: `~${m.estimatedMinutes} min`
            }))
        );

        // Action buttons
        confirmBtn.onclick = () => {
            if (this.form.isComplete()) {
                this.respond(this.form.getChoiceId());
            }
        };

        const cancelBtn = this.$('#butcherCancelBtn');
        if (cancelBtn) cancelBtn.onclick = () => this.respond('cancel');
    }

    renderWarnings(warnings) {
        const el = this.$('#butcherWarnings');
        clear(el);

        if (!warnings || warnings.length === 0) {
            hide(el);
            return;
        }

        show(el);
        warnings.forEach(warning => {
            el.appendChild(
                div({ className: 'butcher-warning' },
                    Icon('warning'),
                    span({}, warning)
                )
            );
        });
    }

    hide() {
        super.hide();
        this.form?.destroy();
        this.form = null;
    }
}
