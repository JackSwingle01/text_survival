// modules/overlays/WeatherChangeOverlay.js
import { OverlayManager } from '../core/OverlayManager.js';

export class WeatherChangeOverlay extends OverlayManager {
    constructor(inputHandler) {
        super('weatherChangeOverlay', inputHandler);

        // Get element references
        this.okBtn = document.getElementById('weatherChangeOkBtn');

        // Bind OK button
        this.okBtn.onclick = () => this.respond('continue');
    }

    render(data, inputId) {
        this.show(inputId);

        // Build message from weather front and condition
        const message = data.weatherFront
            ? `${data.weatherFront}: ${data.weatherCondition}`
            : data.weatherCondition;

        // Set weather change message in body
        this.$('#weatherChangeMessage').textContent = message;
    }

    cleanup() {
        // No cleanup needed (no progress bars or animations)
    }
}
