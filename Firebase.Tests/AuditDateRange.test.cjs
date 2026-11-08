const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function harness(start, end) {
    const requests = [], messages = [], listeners = {};
    const form = { dataset: {}, addEventListener() {}, entries: [['fechaInicio', start], ['fechaFin', end]] };
    const table = { dataset: { currentPage: '2' }, innerHTML: 'original' };
    const ids = { filterForm: form, 'auditoria-table-container': table };
    const document = {
        getElementById: id => ids[id] || null,
        addEventListener: (event, handler) => (listeners[event] ||= []).push(handler)
    };
    const Peticiones = {
        filtrar: async (_, apply) => { await apply(); return true; },
        texto: async url => { requests.push(url); return 'updated'; }
    };
    const window = { Peticiones, Notificaciones: { mostrar: (message, type) => messages.push({ message, type }) } };
    class FormData { constructor(value) { this.value = value; } entries() { return this.value.entries; } }
    vm.runInNewContext(fs.readFileSync('wwwroot/js/auditoria.js', 'utf8'), {
        window, document, Peticiones, FormData, URLSearchParams, console, setTimeout, clearTimeout
    });
    return { window, table, requests, messages };
}

test('inverted dates reject reload without a request or replacing the table', async () => {
    const h = harness('2026-11-02', '2026-10-06');
    h.window.reloadAuditoriaTable(1);
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(h.requests, []);
    assert.deepEqual(h.messages, [{ message: 'La fecha Desde no puede ser posterior a la fecha Hasta.', type: 'error' }]);
    assert.equal(h.table.innerHTML, 'original');
    assert.equal(h.table.dataset.currentPage, '2');
});

test('equal, ordered, open-ended and empty ranges keep normal reload behavior', async () => {
    for (const [start, end] of [['2026-11-02', '2026-11-02'], ['2026-10-06', '2026-11-02'], ['2026-11-02', ''], ['', '2026-11-02'], ['', '']]) {
        const h = harness(start, end);
        h.window.reloadAuditoriaTable(3);
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(h.requests.length, 1);
        const url = new URL(h.requests[0], 'http://localhost');
        assert.equal(url.pathname, '/Auditoria/TablePartial');
        assert.equal(url.searchParams.get('fechaInicio'), start);
        assert.equal(url.searchParams.get('fechaFin'), end);
        assert.equal(url.searchParams.get('pageNumber'), '3');
        assert.deepEqual(h.messages, []);
        assert.equal(h.table.innerHTML, 'updated');
    }
});
