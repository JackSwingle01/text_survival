export const Utils = {
    clearElement(el) {
        while (el.firstChild) {
            el.removeChild(el.firstChild);
        }
    }
};
