import { clear } from '../lib/helpers.js';
import { Icon } from '../lib/components/Icon.js';
import { getLocationFeatureIcon } from './icons.js';

export const LocationDisplay = {
    renderFeatures(features) {
        const container = document.getElementById('locationFeatures');
        clear(container);

        if (!features || features.length === 0) return;

        features.forEach(f => {
            const tag = document.createElement('span');
            tag.className = 'feature-tag';

            // Add icon (using centralized icons module)
            tag.appendChild(Icon(getLocationFeatureIcon(f.type)));

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
