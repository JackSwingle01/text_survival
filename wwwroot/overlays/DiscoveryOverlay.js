// modules/overlays/DiscoveryOverlay.js
import { OverlayManager } from '../core/OverlayManager.js';

export class DiscoveryOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('discoveryOverlay', inputHandler);

        // Get element references
        this.continueBtn = document.getElementById('discoveryContinueBtn');

        // Bind continue button
        this.continueBtn.onclick = () => this.respond('continue');
    }

    render(discoveryData, inputId) {
        this.show(inputId);

        // Set location name in title
        this.$('#discoveryLocationName').textContent = discoveryData.locationName;

        // Set discovery text in body
        this.$('#discoveryText').textContent = discoveryData.discoveryText;
    }

    cleanup() {
        // No cleanup needed (no progress bars or animations)
    }
}
