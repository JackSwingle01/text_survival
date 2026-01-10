// overlays/DiscoveryOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';

/**
 * DiscoveryOverlay - Location discovery notification
 */
export class DiscoveryOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('discoveryOverlay', inputHandler);

        // Bind continue button (static element)
        const continueBtn = this.$('#discoveryContinueBtn');
        if (continueBtn) {
            continueBtn.onclick = () => this.respond('continue');
        }
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Set location name
        const nameEl = this.$('#discoveryLocationName');
        if (nameEl) nameEl.textContent = data.locationName;

        // Set discovery text
        const textEl = this.$('#discoveryText');
        if (textEl) textEl.textContent = data.discoveryText;
    }
}
