export class NotificationRenderer {
    constructor() {
        this.createContainers();
    }

    createContainers() {
        this.toastContainer = this.ensureContainer('toast-container');
        this.alertContainer = this.ensureContainer('alert-container');
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
            switch (entry.level) {
                case 'success': this.showToast(entry.text, 'success'); break;
                case 'danger': this.showAlert(entry.text); break;
                case 'warning': this.showNotice(entry.text); break;
                case 'normal': this.showToast(entry.text, 'normal'); break;
            }
        });
    }

    showToast(message, level) {
        const toast = document.createElement('div');
        toast.className = `notification-toast ${level}`;
        toast.textContent = message;
        toast.onclick = () => this.dismissToast(toast);
        this.toastContainer.appendChild(toast);
        setTimeout(() => this.dismissToast(toast), 3500);
    }

    dismissToast(toast) {
        if (!toast.parentElement) return;
        toast.classList.add('fade-out');
        setTimeout(() => { if (toast.parentElement) toast.remove(); }, 300);
    }

    showAlert(message) {
        this.alertContainer.textContent = ''; // Clear existing
        const alert = document.createElement('div');
        alert.className = 'alert-banner';

        const messageSpan = document.createElement('span');
        messageSpan.textContent = message;

        const dismissBtn = document.createElement('button');
        dismissBtn.className = 'dismiss-btn';
        dismissBtn.textContent = '✕';
        dismissBtn.onclick = () => { if (alert.parentElement) alert.remove(); };

        alert.appendChild(messageSpan);
        alert.appendChild(dismissBtn);
        this.alertContainer.appendChild(alert);
    }

    showNotice(message) {
        const notice = document.createElement('div');
        notice.className = 'contextual-notice center';
        notice.textContent = message;
        document.body.appendChild(notice);
        setTimeout(() => {
            notice.classList.add('fade-out');
            setTimeout(() => { if (notice.parentElement) notice.remove(); }, 500);
        }, 8000);
    }
}
