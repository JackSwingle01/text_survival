// overlays/ForageOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';
import { div, span, clear, show, hide } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';
import { MultiStepForm } from '../lib/components/RadioGroup.js';

/**
 * ForageOverlay - Multi-step foraging interface with focus/time selection
 */
export class ForageOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('forageOverlay', inputHandler);
        this.form = null;
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Quality indicator
        const qualityEl = this.$('#forageQuality');
        if (qualityEl) qualityEl.textContent = `Resources look ${data.locationQuality}.`;

        // Clues
        this.renderClues(data.clues);

        // Warnings
        this.renderWarnings(data.warnings);

        // Focus options
        const focusEl = this.$('#forageFocusOptions');
        const timeEl = this.$('#forageTimeOptions');
        const confirmBtn = this.$('#forageConfirmBtn');
        const confirmDesc = this.$('#forageConfirmDesc');

        // Clean up old form
        this.form?.destroy();

        // Create new form
        this.form = new MultiStepForm({
            confirmBtn,
            confirmDesc
        });

        // Add focus options
        this.form.addField(
            'focus',
            focusEl,
            data.focusOptions.map(f => ({
                id: f.id,
                label: f.label,
                description: f.description
            })),
            (focusId) => this.highlightClues(focusId)
        );

        // Add time options
        this.form.addField(
            'time',
            timeEl,
            data.timeOptions.map(t => ({
                id: t.id,
                label: t.label
            }))
        );

        // Action buttons
        confirmBtn.onclick = () => {
            if (this.form.isComplete()) {
                this.respond(this.form.getChoiceId());
            }
        };

        const cancelBtn = this.$('#forageCancelBtn');
        if (cancelBtn) cancelBtn.onclick = () => this.respond('cancel');

        const keepWalkingBtn = this.$('#forageKeepWalkingBtn');
        if (keepWalkingBtn) keepWalkingBtn.onclick = () => this.respond('keep_walking');
    }

    renderClues(clues) {
        const section = this.$('#forageClues');
        const list = this.$('#forageCluesList');

        if (!clues || clues.length === 0) {
            hide(section);
            return;
        }

        show(section);
        clear(list);

        clues.forEach(clue => {
            const clueEl = div(
                {
                    className: `forage-clue ${clue.suggestedFocusId ? 'clickable' : ''}`.trim(),
                    'data-focus-id': clue.suggestedFocusId || '',
                    onClick: clue.suggestedFocusId
                        ? () => this.form?.groups.focus?.select(clue.suggestedFocusId)
                        : null
                },
                span({ className: 'clue-bullet' }, '•'),
                span({ className: 'clue-desc' }, clue.description)
            );
            list.appendChild(clueEl);
        });
    }

    renderWarnings(warnings) {
        const el = this.$('#forageWarnings');
        clear(el);

        if (!warnings || warnings.length === 0) return;

        warnings.forEach(warning => {
            const iconName = warning.includes('dark') ? 'dark_mode' :
                            warning.includes('axe') ? 'carpenter' :
                            warning.includes('shovel') ? 'agriculture' : 'info';
            el.appendChild(
                div({ className: 'forage-warning' },
                    Icon(iconName),
                    span({}, warning)
                )
            );
        });
    }

    highlightClues(focusId) {
        this.container.querySelectorAll('.forage-clue').forEach(clue => {
            clue.classList.toggle('highlighted', clue.dataset.focusId === focusId);
        });
    }

    hide() {
        super.hide();
        this.form?.destroy();
        this.form = null;
    }
}
