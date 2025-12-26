export const ConnectionOverlay = {
    show(message, isError = false) {
        const overlay = document.getElementById('connectionOverlay');
        const msgEl = document.getElementById('connectionMessage');
        overlay.classList.remove('hidden');
        msgEl.textContent = message;
        msgEl.classList.toggle('error', isError);
    },

    hide() {
        document.getElementById('connectionOverlay').classList.add('hidden');

        // Clear any stale button states from before disconnect
        document.querySelectorAll('.action-btn').forEach(btn => {
            btn.disabled = false;
        });
    }
};
