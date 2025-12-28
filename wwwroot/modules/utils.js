// CSS class for Material Symbols icons
export const ICON_CLASS = 'material-symbols-outlined';

/**
 * Create a Material Symbols icon element
 */
export function createIcon(name) {
    const el = document.createElement('span');
    el.className = ICON_CLASS;
    el.textContent = name;
    return el;
}

/**
 * Show an element by removing 'hidden' class
 */
export function show(el) {
    el?.classList.remove('hidden');
}

/**
 * Hide an element by adding 'hidden' class
 */
export function hide(el) {
    el?.classList.add('hidden');
}

export const Utils = {
    clearElement(el) {
        while (el.firstChild) {
            el.removeChild(el.firstChild);
        }
    }
};
