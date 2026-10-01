const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const vm = require('node:vm');
const fs = require('node:fs');

function fixture(hasInput = true, primaryTouch = null) {
    const handlers = new Map();
    const attributes = new Map([['spellcheck', 'false'], ['autocapitalize', 'words']]);
    const input = {
        value: '',
        getAttribute(name) { return attributes.get(name) ?? null; },
        setAttribute(name, value) { attributes.set(name, value); },
        removeAttribute(name) { attributes.delete(name); },
        setSelectionRange(start, end) { assert.equal(start, 0); assert.equal(end, 0); },
        addEventListener(name, handler, capture) {
            assert.equal(capture, true);
            handlers.set(name, handler);
        },
        removeEventListener(name, handler, capture) {
            assert.equal(capture, true);
            assert.equal(handlers.get(name), handler);
            handlers.delete(name);
        }
    };
    const pointerHandlers = new Set();
    const pointer = {
        matches: primaryTouch,
        addEventListener(name, handler) {
            assert.equal(name, 'change');
            pointerHandlers.add(handler);
        },
        removeEventListener(name, handler) {
            assert.equal(name, 'change');
            pointerHandlers.delete(handler);
        }
    };
    const context = vm.createContext({
        matchMedia: primaryTouch === null ? undefined : query => {
            assert.equal(query, '(pointer: coarse)');
            return pointer;
        },
        document: { querySelector(selector) {
            assert.equal(selector, '#out input');
            return hasInput ? input : null;
        }}
    });
    const script = path.resolve(__dirname, '../../../src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser/wwwroot/native-keyboard.js');
    vm.runInContext(fs.readFileSync(script, 'utf8'), context, { filename: script });
    const text = [];
    let blurCount = 0;
    const api = context.dotnet6502NativeKeyboard;
    return {
        api, text, input, attributes, handlers, pointerHandlers,
        changePrimaryPointer(touch) {
            pointer.matches = touch;
            for (const handler of pointerHandlers) handler();
        },
        start: () => api.start(value => text.push(value), () => blurCount++),
        blurCount: () => blurCount,
        event(name, data = {}) {
            const event = {
                stopped: false, prevented: false,
                stopImmediatePropagation() { this.stopped = true; },
                preventDefault() { this.prevented = true; },
                ...data
            };
            handlers.get(name)(event);
            return event;
        }
    };
}

test('native typing uses beforeinput once, while named editing keys prevent native duplication', () => {
    const f = fixture();
    assert.equal(f.start(), true);
    assert.equal(f.attributes.get('autocorrect'), 'off');
    assert.equal(f.event('keydown', {key: 'a'}).prevented, false);
    assert.equal(f.event('beforeinput', {inputType: 'insertText', data: 'a'}).prevented, true);
    f.event('keyup', {key: 'a'});
    f.event('keydown', {key: 'Enter'});
    f.event('keydown', {key: 'Backspace'});
    f.event('keydown', {key: 'Escape'});
    f.event('beforeinput', {inputType: 'insertReplacementText', data: 'word'});
    f.event('beforeinput', {inputType: 'insertText', data: null});
    f.event('beforeinput', {inputType: 'historyUndo'});
    f.input.value = 'stale';
    f.event('input');
    assert.equal(f.input.value, '');
    assert.deepEqual(f.text, ['a', '\r', '\b', '\x1b', 'word']);
});

test('software keyboard sends Return and Backspace without named keydown events', () => {
    const f = fixture(); f.start();
    f.event('keydown', {key: 'Unidentified'});
    f.event('beforeinput', {inputType: 'deleteContentBackward'});
    f.event('beforeinput', {inputType: 'insertParagraph'});
    f.event('beforeinput', {inputType: 'insertLineBreak'});
    assert.deepEqual(f.text, ['\b', '\r', '\r']);
});

test('composition sends only committed text, preserves interim input, and ignores cancelled commits', () => {
    const f = fixture(); f.start();
    f.event('compositionstart');
    f.event('compositionupdate', {data: 'e'});
    f.input.value = 'e';
    f.event('keydown', {key: 'Enter'});
    assert.equal(f.event('beforeinput', {inputType: 'insertCompositionText', data: 'e'}).prevented, false);
    f.event('input');
    assert.equal(f.input.value, 'e');
    assert.deepEqual(f.text, []);
    f.event('compositionend', {data: 'é'});
    f.event('compositionend', {data: ''});
    f.event('beforeinput', {inputType: 'insertFromComposition', data: 'é'});
    f.event('beforeinput', {inputType: 'deleteByComposition'});
    f.event('beforeinput', {inputType: 'insertCompositionText'});
    f.event('beforeinput', {inputType: 'insertText', isComposing: true});
    f.event('keydown', {key: 'Enter', isComposing: true});
    assert.deepEqual(f.text, ['é']);
    assert.equal(f.input.value, '');
});

test('paste sends one chunk and suppresses the following beforeinput', () => {
    const f = fixture(); f.start();
    const e = f.event('paste', {clipboardData: {getData(type) {assert.equal(type, 'text/plain'); return 'a\nb';}}});
    assert.equal(e.prevented, true);
    assert.equal(e.stopped, true);
    f.event('beforeinput', {inputType: 'insertFromPaste', data: 'a\nb'});
    f.event('paste');
    assert.deepEqual(f.text, ['a\nb']);
});

test('blur cancels queued typing and a delayed composition commit, then focus restores typing', () => {
    const f = fixture(); f.start();
    f.event('compositionstart');
    f.event('blur');
    f.event('compositionend', {data: 'cancelled'});
    assert.equal(f.blurCount(), 1);
    assert.deepEqual(f.text, []);
    f.event('focus');
    f.event('beforeinput', {inputType: 'insertText', data: 'x'});
    assert.deepEqual(f.text, ['x']);
});

test('stop restores the runtime input and switching sessions drops old handlers', () => {
    const f = fixture();
    f.api.stop();
    f.start();
    f.start();
    f.event('beforeinput', {inputType: 'insertText', data: 'a'});
    assert.deepEqual(f.text, ['a']);
    f.api.stop();
    assert.equal(f.handlers.size, 0);
    assert.deepEqual([...f.attributes], [['spellcheck', 'false'], ['autocapitalize', 'words']]);
    assert.equal(f.input.value, '');
    const missing = fixture(false);
    assert.equal(missing.start(), false);
    missing.api.stop();
});


test('keyboard availability follows the primary pointer, including runtime changes', () => {
    for (const initial of [false, true]) {
        const f = fixture(true, initial);
        const availability = [];
        f.api.observeAvailability(value => availability.push(value));
        assert.deepEqual(availability, [initial]);
        f.changePrimaryPointer(!initial);
        f.changePrimaryPointer(initial);
        assert.deepEqual(availability, [initial, !initial, initial]);
        // Observing capabilities must not intercept normal desktop keyboard input.
        assert.equal(f.handlers.size, 0);
    }
});

test('availability defaults to hidden without matchMedia and replaces earlier observers', () => {
    const missing = fixture();
    const fallback = [];
    missing.api.observeAvailability(value => fallback.push(value));
    missing.api.observeAvailability(value => fallback.push(value));
    assert.deepEqual(fallback, [false, false]);
    const f = fixture(true, true);
    const oldValues = [];
    const values = [];
    f.api.observeAvailability(value => oldValues.push(value));
    f.api.observeAvailability(value => values.push(value));
    assert.equal(f.pointerHandlers.size, 1);
    f.changePrimaryPointer(false);
    assert.deepEqual(oldValues, [true]);
    assert.deepEqual(values, [true, false]);
});
