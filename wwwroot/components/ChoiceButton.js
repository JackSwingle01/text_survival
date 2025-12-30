// modules/components/ChoiceButton.js
import { DOMBuilder, icon } from '../core/DOMBuilder.js';

/**
 * Configuration for a choice button
 * @typedef {Object} ChoiceConfig
 * @property {string} id - Choice identifier
 * @property {string} label - Main button text
 * @property {string} [description] - Secondary description
 * @property {string} [cost] - Cost display (e.g., "5 min")
 * @property {string} [icon] - Material icon name
 * @property {boolean} [isAvailable=true] - Whether button is enabled
 * @property {string} [disabledReason] - Why it's disabled
 * @property {string} [hitChance] - For combat actions
 * @property {string} [category] - For grouping (offensive, defensive, etc.)
 */

export class ChoiceButton {
    /**
     * Create a standard option button (events, hunts, encounters)
     */
    static option(config, onClick) {
        const btn = DOMBuilder.button('option-btn')
            .disabled(!config.isAvailable)
            .onClick(() => onClick(config.id));

        // Icon (optional)
        if (config.icon) {
            btn.append(icon(config.icon));
        }

        // Label
        btn.append(
            DOMBuilder.span('option-btn__label').text(config.label)
        );

        // Description or disabled reason
        if (!config.isAvailable && config.disabledReason) {
            btn.append(
                DOMBuilder.span('choice-disabled-reason').text(config.disabledReason)
            );
        } else if (config.description) {
            btn.append(
                DOMBuilder.span('option-btn__desc').text(config.description)
            );
        }

        // Cost
        if (config.cost) {
            btn.append(
                DOMBuilder.span('choice-cost').text(config.cost)
            );
        }

        return btn.build();
    }

    /**
     * Create a simple action button
     */
    static action(label, onClick, variant = '') {
        return DOMBuilder.button(`btn ${variant}`.trim())
            .text(label)
            .onClick(onClick)
            .build();
    }

    /**
     * Create a full-width button
     */
    static full(label, onClick, variant = '') {
        return DOMBuilder.button(`btn btn--full ${variant}`.trim())
            .text(label)
            .onClick(onClick)
            .build();
    }

    /**
     * Create a continue button (for outcomes)
     */
    static continue(onClick, label = 'Continue') {
        return DOMBuilder.button('event-continue-btn')
            .text(label)
            .onClick(onClick)
            .build();
    }

    /**
     * Create a combat action button
     */
    static combat(config, onClick) {
        const btn = DOMBuilder.button(`combat-action ${config.category || 'special'}`)
            .disabled(!config.isAvailable)
            .onClick(() => onClick(config.id));

        btn.append(
            DOMBuilder.div('combat-action-name').text(config.label)
        );

        if (!config.isAvailable && config.disabledReason) {
            btn.append(
                DOMBuilder.div('combat-action-disabled').text(config.disabledReason)
            );
        } else {
            if (config.description) {
                btn.append(
                    DOMBuilder.div('combat-action-hint').text(config.description)
                );
            }
            if (config.hitChance) {
                btn.append(
                    DOMBuilder.div('combat-action-stat').text(config.hitChance)
                );
            }
        }

        return btn.build();
    }

    /**
     * Render a list of choices into a container
     */
    static renderChoices(container, choices, onClick, options = {}) {
        const { type = 'option', filter = null } = options;
        
        choices
            .filter(choice => !filter || filter(choice))
            .forEach(choice => {
                let btn;
                switch (type) {
                    case 'combat':
                        btn = ChoiceButton.combat(choice, onClick);
                        break;
                    case 'action':
                        btn = ChoiceButton.action(choice.label, () => onClick(choice.id));
                        break;
                    default:
                        btn = ChoiceButton.option(choice, onClick);
                }
                container.appendChild(btn);
            });
    }
}