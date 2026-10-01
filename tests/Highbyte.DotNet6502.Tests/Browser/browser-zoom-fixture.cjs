const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');

// Execute the production script against a deterministic DOM/viewport model.
// This checks coordinates and platform API behavior, not browser rendering.
const sourcePath = path.resolve(__dirname,
    '../../../src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser/wwwroot/browser-zoom.js');
const source = fs.readFileSync(sourcePath, 'utf8');

function createElement() {
    const element = {
        style: {}, textContent: '', hidden: false, attributes: {},
        classes: new Set(), handlers: {}, options: {},
        setAttribute(name, value) { this.attributes[name] = value; },
        addEventListener(event, callback, options) {
            this.handlers[event] = callback;
            this.options[event] = options;
        },
        getBoundingClientRect() { return { height: 48 }; }
    };
    element.classList = {
        toggle(name, on) {
            if (on) element.classes.add(name);
            else element.classes.delete(name);
        },
        contains(name) { return element.classes.has(name); }
    };
    return element;
}

function fixture(width, height, setup = () => {}) {
    const elements = new Map();
    for (const id of [
        'out', 'browser-app-viewport', 'browser-zoom-controls', 'browser-zoom-out',
        'browser-zoom-in', 'browser-zoom-fit', 'browser-zoom-reset', 'browser-zoom-level',
        'browser-zoom-toggle', 'browser-zoom-options', 'browser-orientation-toggle',
        'browser-orientation-status'
    ]) {
        elements.set(id, createElement());
    }
    const document = {
        documentElement: { clientWidth: width, clientHeight: height },
        body: createElement(), handlers: {},
        getElementById: id => elements.get(id),
        addEventListener(event, callback) { this.handlers[event] = callback; }
    };
    const window = {
        innerHeight: height, scrollX: 0, scrollY: 0, handlers: {},
        clearTimeout() {}, setTimeout() { return 1; },
        requestAnimationFrame: callback => callback(),
        getComputedStyle: () => ({ bottom: '12px' }),
        addEventListener(event, callback) { this.handlers[event] = callback; },
        scrollTo(x, y) { this.scrollX = x; this.scrollY = y; }
    };
    const app = elements.get('out');
    const viewport = elements.get('browser-app-viewport');
    app.getBoundingClientRect = () => {
        const scale = Number(app.style.transform.match(/[\d.]+/)[0]);
        const left = Math.max(0, (document.documentElement.clientWidth
            - parseFloat(viewport.style.width)) / 2) - window.scrollX;
        const top = parseFloat(document.body.style.paddingTop) - window.scrollY;
        return {
            left, top,
            right: parseFloat(app.style.width) * scale + left,
            bottom: parseFloat(app.style.height) * scale + top
        };
    };
    setup({ document, window, elements });
    const context = { document, window, console };
    vm.runInNewContext(source, context, { filename: sourcePath });
    const zoomApi = context.dotnet6502BrowserZoom;
    return {
        elements, document, window,
        size: (w, h) => zoomApi.setContentSize(w, h),
        click: id => elements.get('browser-zoom-' + id).handlers.click(),
        register: callback => zoomApi.setViewportChangedCallback(callback),
        clear: () => zoomApi.clearViewportChangedCallback(),
        modal: open => zoomApi.setModalOpen(open),
        zoom: () => Number(app.style.transform.match(/[\d.]+/)[0])
    };
}

module.exports = { fixture };
