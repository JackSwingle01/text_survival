import { show as showEl, hide as hideEl } from './utils.js';

export const ConnectionOverlay = {
    show(message, isError = false) {
        const overlay = document.getElementById('connectionOverlay');
        const msgEl = document.getElementById('connectionMessage');
        showEl(overlay);
        msgEl.textContent = message;
        msgEl.classList.toggle('error', isError);
    },

    hide() {
        hideEl(document.getElementById('connectionOverlay'));

        // Clear any stale button states from before disconnect
        document.querySelectorAll('.action-btn').forEach(btn => {
            btn.disabled = false;
        });
    }
};
