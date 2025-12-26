import { Utils } from './utils.js';

export const LocationDisplay = {
    renderTags(tags) {
        const container = document.getElementById('locationDesc');
        Utils.clearElement(container);

        if (!tags || tags.length === 0) return;

        tags.forEach(tag => {
            const pill = document.createElement('span');
            pill.className = 'location-tag';
            pill.textContent = tag;
            container.appendChild(pill);
        });
    },

    renderFeatures(features) {
        const container = document.getElementById('locationFeatures');
        Utils.clearElement(container);

        if (!features || features.length === 0) return;

        features.forEach(f => {
            const span = document.createElement('span');
            span.className = 'feature-tag';
            span.textContent = f.label;
            if (f.detail) {
                span.textContent += ': ';
                const valueSpan = document.createElement('span');
                valueSpan.className = `feature-value ${f.type}`;
                valueSpan.textContent = f.detail;
                span.appendChild(valueSpan);
            }
            container.appendChild(span);
        });
    }
};
