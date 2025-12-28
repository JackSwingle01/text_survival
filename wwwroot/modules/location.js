import { Utils, createIcon } from './utils.js';

const FEATURE_ICONS = {
    'Fire': 'local_fire_department',
    'Shelter': 'camping',
    'Cache': 'inventory_2',
    'Forage': 'eco',
    'Harvest': 'forest',
    'Animals': 'pets',
    'Water': 'water_drop',
    'Wood': 'carpenter',
    'Trap': 'trap',
    'Curing': 'dry_cleaning',
    'Project': 'construction',
    'Salvage': 'recycling',
    'Bedding': 'bed'
};

export const LocationDisplay = {
    renderFeatures(features) {
        const container = document.getElementById('locationFeatures');
        Utils.clearElement(container);

        if (!features || features.length === 0) return;

        features.forEach(f => {
            const tag = document.createElement('span');
            tag.className = 'feature-tag';

            // Add icon
            tag.appendChild(createIcon(FEATURE_ICONS[f.type] || 'category'));

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
