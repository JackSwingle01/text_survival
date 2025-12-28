import { Utils, createIcon } from './utils.js';
import { getLocationFeatureIcon } from './icons.js';

export const LocationDisplay = {
    renderFeatures(features) {
        const container = document.getElementById('locationFeatures');
        Utils.clearElement(container);

        if (!features || features.length === 0) return;

        features.forEach(f => {
            const tag = document.createElement('span');
            tag.className = 'feature-tag';

            // Add icon (using centralized icons module)
            tag.appendChild(createIcon(getLocationFeatureIcon(f.type)));

            // Add label text
            tag.appendChild(document.createTextNode(f.label));

            // Add detail if present
            if (f.detail) {
                const detail = document.createElement('span');
                detail.className = 'feature-detail';
                detail.textContent = f.detail;
                tag.appendChild(detail);
            }

            container.appendChild(tag);
        });
    }
};
