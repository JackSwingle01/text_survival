export class NotificationRenderer {
    constructor() {
        this.toastContainer = this.ensureContainer('toast-container');
        this.toastQueue = []; // Track toasts in order
        this.isTimerRunning = false;
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
        const durations = { normal: 3000, success: 4000, warning: 5000, danger: 6000 };
        const duration = durations[level] || 5000;

        const toast = document.createElement('div');
        toast.className = `notification-toast ${level}`;
        toast.textContent = message;
        toast.onclick = () => this.dismissToast(toast);

        // Add to bottom of container
        this.toastContainer.appendChild(toast);

        // Track in queue with its duration
        this.toastQueue.push({ element: toast, duration });

        // Start timer for first toast if not already running
        this.startNextTimer();
    }

    startNextTimer() {
        if (this.isTimerRunning || this.toastQueue.length === 0) return;

        this.isTimerRunning = true;
        const first = this.toastQueue[0];

        setTimeout(() => {
            this.dismissToast(first.element);
        }, first.duration);
    }

    dismissToast(toast) {
        if (!toast.parentElement) return;

        // Remove from queue
        const index = this.toastQueue.findIndex(t => t.element === toast);
        if (index !== -1) {
            this.toastQueue.splice(index, 1);
        }

        // Animate out
        toast.classList.add('fade-out');
        setTimeout(() => {
            if (toast.parentElement) toast.remove();

            // If this was the timed toast (first in queue), start next timer
            if (index === 0) {
                this.isTimerRunning = false;
                this.startNextTimer();
            }
        }, 300);
    }
}
