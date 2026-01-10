export class NotificationRenderer {
    constructor() {
        this.toastContainer = this.ensureContainer('toast-container');
    }

    ensureContainer(id) {
        let container = document.getElementById(id);
        if (!container) {
            container = document.createElement('div');
            container.id = id;
            document.body.appendChild(container);
        }
        return container;
    }

    render(logEntries) {
        if (!logEntries || logEntries.length === 0) return;
        logEntries.forEach(entry => {
            this.showToast(entry.text, entry.level);
        });
    }

    showToast(message, level) {
        const durations = { normal: 5000, success: 5000, warning: 7000, danger: 10000 };
        const duration = durations[level] || 5000;

        const toast = document.createElement('div');
        toast.className = `notification-toast ${level}`;
        toast.textContent = message;
        toast.onclick = () => this.dismissToast(toast);
        this.toastContainer.appendChild(toast);
        setTimeout(() => this.dismissToast(toast), duration);
    }

    dismissToast(toast) {
        if (!toast.parentElement) return;
        toast.classList.add('fade-out');
        setTimeout(() => { if (toast.parentElement) toast.remove(); }, 300);
    }
}
