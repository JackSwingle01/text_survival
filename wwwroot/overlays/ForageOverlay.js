import { OverlayManager } from '../core/OverlayManager.js';
import { DOMBuilder } from '../core/DOMBuilder.js';
import { Utils, ICON_CLASS, show, hide } from '../modules/utils.js';

/**
 * ForageOverlay - Multi-step foraging interface with focus/time selection
 */
export class ForageOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('forageOverlay', inputHandler);

        // Get DOM elements
        this.qualityEl = document.getElementById('forageQuality');
        this.cluesSection = document.getElementById('forageClues');
        this.cluesListEl = document.getElementById('forageCluesList');
        this.warningsEl = document.getElementById('forageWarnings');
        this.focusOptionsEl = document.getElementById('forageFocusOptions');
        this.timeOptionsEl = document.getElementById('forageTimeOptions');
        this.confirmBtn = document.getElementById('forageConfirmBtn');
        this.confirmDesc = document.getElementById('forageConfirmDesc');
        this.cancelBtn = document.getElementById('forageCancelBtn');
        this.keepWalkingBtn = document.getElementById('forageKeepWalkingBtn');

    }

    render(forageData, inputId) {
        this.show(inputId);

        // Quality indicator
        this.qualityEl.textContent = `Resources look ${forageData.locationQuality}.`;

        // Clues list
        this.clear(this.cluesListEl);

        if (forageData.clues && forageData.clues.length > 0) {
            show(this.cluesSection);
            forageData.clues.forEach(clue => {
                const clueEl = this.createClue(clue);
                this.cluesListEl.appendChild(clueEl);
            });
        } else {
            hide(this.cluesSection);
        }

        // Warnings
        this.clear(this.warningsEl);

        if (forageData.warnings && forageData.warnings.length > 0) {
            forageData.warnings.forEach(warning => {
                const warnEl = this.createWarning(warning);
                this.warningsEl.appendChild(warnEl);
            });
        }

        // Focus options
        this.clear(this.focusOptionsEl);
        forageData.focusOptions.forEach(focus => {
            const btn = this.createFocusButton(focus);
            this.focusOptionsEl.appendChild(btn);
        });

        // Time options
        this.clear(this.timeOptionsEl);
        forageData.timeOptions.forEach(time => {
            const btn = this.createTimeButton(time);
            this.timeOptionsEl.appendChild(btn);
        });

        // Create form with two radio group fields
        this.form = this.createForm({
            confirmBtn: this.confirmBtn,
            confirmDesc: this.confirmDesc
        });

        this.form.addRadioGroup('focusId', this.focusOptionsEl, 'focusId', (value) => {
            this.highlightClues(value);
        });

        this.form.addRadioGroup('timeId', this.timeOptionsEl, 'timeId');

        // Action buttons
        this.confirmBtn.onclick = () => {
            if (this.form.isComplete()) {
                this.respond(this.form.getChoiceId());
            }
        };

        this.cancelBtn.onclick = () => this.respond('cancel');
        this.keepWalkingBtn.onclick = () => this.respond('keep_walking');
    }

    createClue(clue) {
        const clueEl = document.createElement('div');
        clueEl.className = 'forage-clue';

        if (clue.suggestedFocusId) {
            clueEl.classList.add('clickable');
            clueEl.dataset.focusId = clue.suggestedFocusId;
            clueEl.onclick = () => this.focusGroup.select(clue.suggestedFocusId);
        }

        const bulletEl = document.createElement('span');
        bulletEl.className = 'clue-bullet';
        bulletEl.textContent = '•';
        clueEl.appendChild(bulletEl);

        const descEl = document.createElement('span');
        descEl.className = 'clue-desc';
        descEl.textContent = clue.description;
        clueEl.appendChild(descEl);

        return clueEl;
    }

    createWarning(warning) {
        const warnEl = document.createElement('div');
        warnEl.className = 'forage-warning';

        const iconEl = document.createElement('span');
        iconEl.className = ICON_CLASS;
        iconEl.textContent = warning.includes('dark') ? 'dark_mode' :
                            warning.includes('axe') ? 'carpenter' :
                            warning.includes('shovel') ? 'agriculture' : 'info';
        warnEl.appendChild(iconEl);

        const textEl = document.createElement('span');
        textEl.textContent = warning;
        warnEl.appendChild(textEl);

        return warnEl;
    }

    createFocusButton(focus) {
        return this.createOptionButton({
            datasetKey: 'focusId',
            datasetValue: focus.id,
            label: focus.label,
            description: focus.description
        });
    }

    createTimeButton(time) {
        return this.createOptionButton({
            datasetKey: 'timeId',
            datasetValue: time.id,
            label: time.label
        });
    }

    highlightClues(focusId) {
        document.querySelectorAll('.forage-clue').forEach(clue => {
            clue.classList.toggle('highlighted', clue.dataset.focusId === focusId);
        });
    }

    cleanup() {
        this.form?.cleanup();
        this.clear(this.cluesListEl);
        this.clear(this.warningsEl);
        this.clear(this.focusOptionsEl);
        this.clear(this.timeOptionsEl);
    }
}
