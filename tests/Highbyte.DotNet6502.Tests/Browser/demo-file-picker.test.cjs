const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function fixture(files) {
    const handlers = new Map();
    const input = {
        files, style: {}, oncancel: null, removed: false,
        addEventListener: (event, handler) => handlers.set(event, handler),
        removeEventListener: event => handlers.delete(event),
        remove() { this.removed = true; },
        click() {}
    };
    const context = vm.createContext({
        document: { createElement: () => input, body: { appendChild() {} } },
        window: { removeEventListener() {} },
        console,
        btoa: value => Buffer.from(value, 'binary').toString('base64'),
        Uint8Array
    });
    const source = fs.readFileSync(path.join(__dirname,
        '../../../src/apps/Avalonia/Highbyte.DotNet6502.App.Avalonia.Browser/BrowserScripting.js'), 'utf8');
    vm.runInContext(source.replace(/^export /gm, ''), context);
    return { input, handlers, pick: context.pickLocalFilesAsBase64 };
}

test('oversized demo selection is rejected before reading any selected file', async () => {
    let reads = 0;
    const arrayBuffer = async () => { reads++; return new ArrayBuffer(1); };
    const fixtureState = fixture([
        { name: 'small.d6502demo', size: 1, arrayBuffer },
        { name: 'large.d6502demo', size: 101, arrayBuffer }
    ]);
    const result = fixtureState.pick('.d6502demo', true, 100);
    await fixtureState.handlers.get('change')();
    assert.match(JSON.parse(await result).error, /100 byte limit/);
    assert.equal(reads, 0);
    assert.equal(fixtureState.input.removed, true);
    assert.equal(fixtureState.handlers.size, 0);
});

test('demo exactly at the size limit imports its bytes', async () => {
    const fixtureState = fixture([
        { name: 'session.d6502demo', size: 3, arrayBuffer: async () => Uint8Array.from([1, 2, 3]).buffer }
    ]);
    const result = fixtureState.pick('.d6502demo', false, 3);
    await fixtureState.handlers.get('change')();
    assert.deepEqual(JSON.parse(await result), [{ name: 'session.d6502demo', base64: 'AQID' }]);
    assert.equal(fixtureState.input.removed, true);
});

test('cancelled demo picker settles and removes its listeners', async () => {
    const fixtureState = fixture([]);
    const result = fixtureState.pick('.d6502demo', false, 100);
    fixtureState.handlers.get('cancel')();
    assert.equal(await result, '');
    assert.equal(fixtureState.input.removed, true);
    assert.equal(fixtureState.handlers.size, 0);
});
