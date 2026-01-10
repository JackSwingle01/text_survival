// lib/helpers.js
// Core DOM construction helpers - safe, readable, no dependencies

/**
 * Create an element with attributes and children.
 * @param {string} tag - HTML tag name
 * @param {Object} attrs - Attributes (className, style, onClick, etc.)
 * @param {...(Node|string|null)} children - Child elements or text
 * @returns {HTMLElement}
 */
export function el(tag, attrs = {}, ...children) {
    const element = document.createElement(tag);

    for (const [key, value] of Object.entries(attrs)) {
        if (value == null) continue;

        if (key === 'className') {
            element.className = value;
        } else if (key === 'style' && typeof value === 'object') {
            Object.assign(element.style, value);
        } else if (key.startsWith('on') && typeof value === 'function') {
            const eventName = key.slice(2).toLowerCase();
            element.addEventListener(eventName, value);
        } else if (key === 'disabled') {
            element.disabled = Boolean(value);
        } else if (key === 'checked') {
            element.checked = Boolean(value);
        } else if (key === 'value') {
            element.value = value;
        } else {
            element.setAttribute(key, value);
        }
    }

    for (const child of children.flat()) {
        if (child == null) continue;
        if (typeof child === 'string' || typeof child === 'number') {
            element.appendChild(document.createTextNode(String(child)));
        } else if (child instanceof Node) {
            element.appendChild(child);
        }
    }

    return element;
}

// Shorthand element creators
export const div = (attrs, ...children) => el('div', attrs, ...children);
export const span = (attrs, ...children) => el('span', attrs, ...children);
export const button = (attrs, ...children) => el('button', attrs, ...children);
export const p = (attrs, ...children) => el('p', attrs, ...children);
export const h2 = (attrs, ...children) => el('h2', attrs, ...children);
export const h3 = (attrs, ...children) => el('h3', attrs, ...children);
export const ul = (attrs, ...children) => el('ul', attrs, ...children);
export const li = (attrs, ...children) => el('li', attrs, ...children);
export const img = (attrs) => el('img', attrs);
export const input = (attrs) => el('input', attrs);
export const label = (attrs, ...children) => el('label', attrs, ...children);

/**
 * Show an element by removing 'hidden' class
 */
export function show(element) {
    element?.classList.remove('hidden');
}

/**
 * Hide an element by adding 'hidden' class
 */
export function hide(element) {
    element?.classList.add('hidden');
}

/**
 * Clear all children from an element
 */
export function clear(element) {
    if (element) element.replaceChildren();
}

/**
 * Toggle visibility based on condition
 */
export function showIf(element, condition) {
    if (condition) {
        show(element);
    } else {
        hide(element);
    }
}
