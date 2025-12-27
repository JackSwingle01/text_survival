import { Utils } from './utils.js';

export const NarrativeLog = {
    render(log) {
        const container = document.getElementById('narrativeLog');
        Utils.clearElement(container);

        if (!log || log.length === 0) return;

        log.forEach(entry => {
            const div = document.createElement('div');
            div.className = `log-entry ${entry.level}`;
            if (entry.timestamp) {
                const ts = document.createElement('span');
                ts.className = 'timestamp';
                ts.textContent = entry.timestamp;
                div.appendChild(ts);
            }
            div.appendChild(document.createTextNode(entry.text));
            container.appendChild(div);
        });

        container.scrollTop = container.scrollHeight;
    }
};
