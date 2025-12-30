// modules/core/DOMBuilder.js
export class DOMBuilder {
    constructor(tag) {
        this.el = document.createElement(tag);
    }

    static create(tag) {
        return new DOMBuilder(tag);
    }

    // Shorthand creators
    static div(className) { return DOMBuilder.create('div').class(className); }
    static span(className) { return DOMBuilder.create('span').class(className); }
    static button(className) { return DOMBuilder.create('button').class(className); }

    class(className) {
        if (className) {
            className.split(' ').filter(Boolean).forEach(c => this.el.classList.add(c));
        }
        return this;
    }

    addClass(className) {
        if (className) this.el.classList.add(className);
        return this;
    }

    toggleClass(className, condition) {
        this.el.classList.toggle(className, condition);
        return this;
    }

    text(content) {
        this.el.textContent = content;
        return this;
    }

    html(content) {
        this.el.innerHTML = content;
        return this;
    }

    attr(name, value) {
        this.el.setAttribute(name, value);
        return this;
    }

    data(key, value) {
        this.el.dataset[key] = value;
        return this;
    }

    style(prop, value) {
        this.el.style[prop] = value;
        return this;
    }

    disabled(isDisabled = true) {
        this.el.disabled = isDisabled;
        return this;
    }

    onClick(handler) {
        if (handler) this.el.onclick = handler;
        return this;
    }

    append(...children) {
        children.forEach(child => {
            if (child instanceof DOMBuilder) {
                this.el.appendChild(child.build());
            } else if (child instanceof Node) {
                this.el.appendChild(child);
            } else if (typeof child === 'string') {
                this.el.appendChild(document.createTextNode(child));
            }
        });
        return this;
    }

    appendTo(parent) {
        if (parent instanceof DOMBuilder) {
            parent.el.appendChild(this.el);
        } else {
            parent.appendChild(this.el);
        }
        return this;
    }

    build() {
        return this.el;
    }
}

// Icon helper
export function icon(name, className = '') {
    return DOMBuilder.span(`material-symbols-outlined ${className}`.trim()).text(name);
}

// Pane header helper (for overlay panes)
export function paneHeader(config) {
    const header = DOMBuilder.div('pane-header');

    // Title (h3 with optional icon)
    const title = DOMBuilder.create('h3').class('pane-header__title');
    if (config.icon) {
        title.append(icon(config.icon));
    }
    title.append(config.title);
    header.append(title);

    // Optional metadata (e.g., weight display)
    if (config.meta) {
        const meta = DOMBuilder.span('pane-header__meta').text(config.meta);
        header.append(meta);
    }

    return header;
}