/**
 * Centralized icon mappings for the UI.
 * Single source of truth for all icon/label associations.
 */

// Weather condition icons
export const WEATHER_ICONS = {
    'Clear': 'wb_sunny',
    'Cloudy': 'cloud',
    'Overcast': 'cloud',
    'Light Snow': 'weather_snowy',
    'Snow': 'weather_snowy',
    'Heavy Snow': 'ac_unit',
    'Blizzard': 'ac_unit',
    'Fog': 'foggy',
    'Wind': 'air'
};

export function getWeatherIcon(condition) {
    return WEATHER_ICONS[condition] || 'partly_cloudy_day';
}

// Feature type to icon mapping (for popup detailed features)
// Note: 'herd' type uses emoji from details[0] instead of Material Icon
export const FEATURE_TYPE_ICONS = {
    'shelter': 'cabin',
    'forage': 'eco',
    'animal': 'cruelty_free',
    'cache': 'inventory_2',
    'water': 'water_drop',
    'wood': 'park',
    'snares': 'circle',
    'curing': 'timelapse'
};

export function getFeatureTypeIcon(type) {
    return FEATURE_TYPE_ICONS[type] || 'info';
}

// Feature icons to human-readable labels (for map tile popups)
export const FEATURE_ICON_LABELS = {
    // Fire states
    'local_fire_department': 'Active fire',
    'fireplace': 'Embers (relight possible)',

    // Resource features
    'eco': 'Foraging area',
    'nutrition': 'Harvestable resources',
    'cruelty_free': 'Wildlife territory',
    'pets': 'Predator territory',
    'water_drop': 'Water source',
    'park': 'Wooded area',

    // Structures
    'cabin': 'Shelter',
    'ac_unit': 'Snow shelter',
    'bed': 'Bedding',
    'inventory_2': 'Storage cache',

    // Trapping
    'circle': 'Snares set',
    'catching_pokemon': 'Snare catch ready!',

    // Work sites
    'search': 'Salvage site',
    'timelapse': 'Curing in progress',
    'done_all': 'Curing complete!',
    'construction': 'Construction project',

    // Environmental details (used by explored tile popups via featureDetails, not icon labels)
    'footprint': 'Animal tracks',
    'scatter_plot': 'Animal droppings',
    'call_split': 'Bent branches',
    'forest': 'Fallen log',
    'nature': 'Hollow tree',
    'skeleton': 'Scattered bones',
    'landscape': 'Stone pile'
};

export function getFeatureIconLabel(icon) {
    return FEATURE_ICON_LABELS[icon] || icon;
}

// Location display feature icons (for location panel)
export const LOCATION_FEATURE_ICONS = {
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

export function getLocationFeatureIcon(featureType) {
    return LOCATION_FEATURE_ICONS[featureType] || 'category';
}

// Fire phase labels (for fire display)
export const FIRE_PHASE_LABELS = {
    'Roaring': 'Roaring',
    'Steady': 'Steady',
    'Dying': 'Dying',
    'Embers': 'Embers',
    'Building': 'Building',
    'Igniting': 'Igniting',
    'Cold': 'No fire'
};

export function getFirePhaseLabel(phase) {
    return FIRE_PHASE_LABELS[phase] || phase;
}
