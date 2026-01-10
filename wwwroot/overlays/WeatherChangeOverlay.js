// overlays/WeatherChangeOverlay.js
import { OverlayBase } from '../lib/OverlayBase.js';

/**
 * WeatherChangeOverlay - Simple weather change notification
 */
export class WeatherChangeOverlay extends OverlayBase {
    constructor(inputHandler) {
        super('weatherChangeOverlay', inputHandler);

        // Bind OK button (static element)
        const okBtn = this.$('#weatherChangeOkBtn');
        if (okBtn) {
            okBtn.onclick = () => this.respond('continue');
        }
    }

    render(data, inputId) {
        if (!data) {
            this.hide();
            return;
        }

        this.show();

        // Build message from weather front and condition
        const message = data.weatherFront
            ? `${data.weatherFront}: ${data.weatherCondition}`
            : data.weatherCondition;

        const messageEl = this.$('#weatherChangeMessage');
        if (messageEl) messageEl.textContent = message;
    }
}
