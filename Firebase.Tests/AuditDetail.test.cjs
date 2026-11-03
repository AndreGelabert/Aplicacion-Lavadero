const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function harness(fetch) {
    const listeners = {};
    class Element {
        constructor() { this.children = []; this.textContent = ''; this.dataset = {}; this.open = false; this.handlers = {}; this.focused = false; }
        addEventListener(type, fn) { this.handlers[type] = fn; }
        appendChild(child) { this.children.push(child); }
        replaceChildren() { this.children = []; }
        showModal() { this.open = true; }
        close() { this.open = false; this.handlers.close?.(); }
        focus() { this.focused = true; }
    }
    const ids = {};
    for (const id of ['audit-detail-dialog', 'audit-detail-status', 'audit-detail-meta', 'audit-detail-rows', 'audit-detail-close', 'auditoria-table-container']) ids[id] = new Element();
    const document = { addEventListener(type, fn) { (listeners[type] ||= []).push(fn); }, getElementById(id) { return ids[id] || null; }, createElement() { return new Element(); } };
    const window = {};
    vm.runInNewContext(fs.readFileSync('wwwroot/js/auditoria.js', 'utf8'), { window, document, fetch, AbortController, URLSearchParams, console, setTimeout, clearTimeout });
    listeners.DOMContentLoaded.forEach(fn => fn());
    return { ids, window, click(id) { const button = new Element(); button.dataset.auditDetailId = id; button.closest = () => button; listeners.click[0]({ target: button }); return button; } };
}
const tick = () => new Promise(resolve => setImmediate(resolve));
const ok = data => ({ ok: true, json: async () => data });
const deferred = () => { let resolve; const promise = new Promise(r => resolve = r); return { promise, resolve }; };

test('delegated click renders only changed fields and metadata as literal text', async () => {
    const urls = [];
    const h = harness(async url => { urls.push(url); return ok({ action: '<img>', nombreHistorico: '<script>', antes: { Igual: 'x', Nombre: '<b>viejo</b>' }, despues: { Igual: 'x', Nombre: 'nuevo' } }); });
    h.click('doc 1'); await tick();
    assert.match(urls[0], /doc%201/);
    assert.equal(h.ids['audit-detail-dialog'].open, true);
    assert.deepEqual(h.ids['audit-detail-rows'].children.map(r => r.children.map(c => c.textContent)), [['Nombre', '<b>viejo</b>', 'nuevo']]);
    assert.ok(h.ids['audit-detail-meta'].children.some(c => c.textContent === '<script>'));
});

test('legacy null history takes precedence over session and explicit marker; unchanged differs from session', async () => {
    let data = { action: 'Cerrar Sesión', comparacionNoAplica: true, antes: null, despues: null };
    const h = harness(async () => ok(data));
    h.click('legacy'); await tick();
    assert.equal(h.ids['audit-detail-status'].textContent, 'Detalle histórico no disponible para este registro.');
    data = { action: 'Editar', antes: {}, despues: {} };
    h.click('unchanged'); await tick();
    assert.equal(h.ids['audit-detail-status'].textContent, 'No hay cambios registrados.');
    data = { action: 'Cerrar Sesión', comparacionNoAplica: true, antes: {}, despues: {} };
    h.click('session'); await tick();
    assert.equal(h.ids['audit-detail-status'].textContent, 'La comparación no aplica a eventos de sesión.');
});

test('404, invalid id and backend/network errors produce generic error without stored HTML', async () => {
    for (const response of [{ ok: false, status: 404 }, { ok: false, status: 500 }, new Error('<secret>')]) {
        const h = harness(async () => { if (response instanceof Error) throw response; return response; });
        h.click('../invalid'); await tick();
        assert.equal(h.ids['audit-detail-status'].textContent, 'No se pudo cargar el detalle de auditoría.');
        assert.equal(h.ids['audit-detail-rows'].children.length, 0);
    }
});

test('close (including native Escape) returns focus and ignores stale response; newer click wins', async () => {
    const calls = [];
    const h = harness((url, options) => { const d = deferred(); calls.push({ ...d, options }); return d.promise; });
    const first = h.click('first');
    h.ids['audit-detail-dialog'].close(); // Native dialog closes on Escape and emits close.
    assert.equal(first.focused, true);
    assert.equal(calls[0].options.signal.aborted, true);
    calls[0].resolve(ok({ action: 'stale', antes: {}, despues: { x: 'old' } })); await tick();
    assert.equal(h.ids['audit-detail-rows'].children.length, 0);
    h.click('second'); h.click('third');
    assert.equal(calls[1].options.signal.aborted, true);
    calls[2].resolve(ok({ antes: {}, despues: { x: 'new' } })); await tick();
    calls[1].resolve(ok({ antes: {}, despues: { x: 'old' } })); await tick();
    assert.equal(h.ids['audit-detail-rows'].children[0].children[2].textContent, 'new');
});

test('existing pagination and sort still request the same table endpoint and update page', async () => {
    const urls = [];
    const h = harness(async url => { urls.push(url); return { text: async () => 'table result' }; });
    h.window.reloadAuditoriaTable(2); await tick();
    assert.match(urls[0], /^\/Auditoria\/TablePartial\?/);
    assert.match(urls[0], /pageNumber=2/);
    assert.match(urls[0], /sortBy=Timestamp/);
    assert.equal(h.ids['auditoria-table-container'].innerHTML, 'table result');
    h.window.sortTable('Action'); await tick();
    assert.match(urls[1], /sortBy=Timestamp/); // absent hidden inputs retain existing default behavior
});
