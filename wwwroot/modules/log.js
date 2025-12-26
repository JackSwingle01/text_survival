import { Utils } from './utils.js';

export const NarrativeLog = {
    render(log) {
        const container = document.getElementById('narrativeLog');
        Utils.clearElement(container);

        if (!log || log.length === 0) return;

        log.forEach(entry => {
            const div = document.createElement('div');
            div.className = `log-entry ${entry.level}`;
            div.textContent = entry.text;
            container.appendChild(div);
        });

        container.scrollTop = container.scrollHeight;
    }
};
