const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

function page() {
    const listeners = [];
    const elements = {};
    const element = () => ({ value: '', dataset: {}, classList: { toggle() {}, add() {}, remove() {} }, addEventListener(type, fn) { this[type] = fn; } });
    for (const id of ['formFinalizar', 'finalizarLavadoId', 'finalizarServicioId', 'finalizarEtapaId', 'finalizarTipo', 'consumoRealFields', 'consumoRealJson', 'justificacionDesvio', 'finalizarModalTitle', 'finalizarModalMessage']) elements[id] = element();
    const inputs = elements.consumoRealInputs = element();
    let fields = [];
    Object.defineProperty(inputs, 'innerHTML', { set(html) {
        fields = [...html.matchAll(/value="([^"]*)" data-insumo-id="([^"]*)" data-teorica="([^"]*)"/g)].map(match => ({ ...element(), value: match[1], dataset: { insumoId: match[2], teorica: match[3] } }));
    }});
    inputs.querySelectorAll = () => fields;
    const requests = [];
    const document = {
        addEventListener(type, fn) { if (type === 'DOMContentLoaded') listeners.push(fn); },
        getElementById(id) { return elements[id] || null; },
        querySelectorAll(selector) { return selector === '.consumo-real-input' ? fields : []; },
        querySelector() { return null; },
        body: { classList: { add() {}, remove() {} } }
    };
    const window = { SiteModule: { escapeHtmlText: value => String(value) }, Notificaciones: { mostrar() {} }, Peticiones: { async enviar(url, options) { requests.push({ url, method: options.method, body: Object.fromEntries(options.body.entries()) }); return { async json() { return { success: false, message: 'rechazado' }; } }; } } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/lavado.js'), 'utf8'), { window, document, FormData, URLSearchParams, console, setTimeout, clearTimeout });
    listeners[1]();
    return { elements, requests, fields: () => fields, open: window.abrirModalFinalizar, async submit() { await elements.formFinalizar.submit({ preventDefault() {} }); } };
}

function modalHarness(document, window, action, captured, duringOpen = () => {}) {
    const elements = {};
    const make = () => ({ dataset: {}, value: '', classList: { add() {}, remove() {} }, setAttribute() {}, focus() {} });
    for (const id of ['insumosInteraction', 'insumosInteractionTitle', 'insumosInteractionForm', 'insumosInteractionContent', 'insumosInteractionError', 'insumosInteractionClose', 'insumosInteractionCancel']) elements[id] = make();
    const content = elements.insumosInteractionContent;
    let html = '';
    Object.defineProperty(content, 'innerHTML', { set(value) { html = value; captured.push(value); } });
    content.querySelector = () => null;
    content.querySelectorAll = selector => selector === '[data-selection]' ? [...html.matchAll(/data-selection="(\d+)"/g)].map(m => ({ dataset: { selection: m[1] }, value: ['cancel', 'missing'].includes(action) ? '' : '0' })) : [...html.matchAll(/data-consumo="(\d+)"/g)].map(m => ({ dataset: { consumo: m[1] }, value: action === 'additional' ? '0.2' : action === 'negative' ? '-1' : action === 'nan' ? 'NaN' : '0' }));
    elements.insumosInteraction.querySelectorAll = () => [];
    const original = document.getElementById.bind(document);
    document.getElementById = id => elements[id] || original(id);
    const source = fs.readFileSync(path.join(__dirname, '../Views/Lavados/_InsumosInteractionModals.cshtml'), 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
    vm.runInNewContext(source, { window, document });
    return async promise => {
        await new Promise(resolve => setImmediate(resolve));
        duringOpen();
        if (action === 'escape') elements.insumosInteraction.onkeydown({ key: 'Escape', preventDefault() {} });
        else if (action === 'cancel') elements.insumosInteractionCancel.onclick();
        else {
            elements.insumosInteractionForm.onsubmit({ preventDefault() {} });
            if (['missing', 'negative', 'nan'].includes(action)) {
                assert.equal(elements.insumosInteractionError.textContent, action === 'missing' ? 'Seleccione un producto válido.' : 'Cantidad inválida.');
                elements.insumosInteractionCancel.onclick();
            }
        }
        await promise;
    };
}

function cancellationPage(script, { confirm = false, prompt = '0.2', consultaOk = true, postSuccess = false, invalid, inspect = false } = {}) {
    const listeners = [], requests = [], messages = [], prompts = [];
    const elements = {};
    const make = () => ({ value: '', dataset: {}, classList: { add() {}, remove() {}, toggle() {} }, addEventListener(type, fn) { this[type] = fn; }, setAttribute() {} });
    for (const id of ['formCancelar', 'cancelarLavadoId', 'cancelarLavadoIdAlt', 'cancelarServicioId', 'cancelarEtapaId', 'cancelarTipo', 'motivoCancelacion']) elements[id] = make();
    const classes = new Set();
    elements.cancelarModal = { style: {}, classList: { add(x) { classes.add(x); }, remove(x) { classes.delete(x); }, contains(x) { return classes.has(x); } }, setAttribute() {} };
    let backdrop = null;
    const document = { addEventListener(type, fn) { if (type === 'DOMContentLoaded') listeners.push(fn); }, getElementById(id) { return id === 'modal-backdrop' ? backdrop : elements[id] || null; }, createElement() { return { style: {}, remove() { backdrop = null; } }; }, querySelectorAll() { return []; }, querySelector() { return null; }, body: { classList: { add() {}, remove() {} }, appendChild(node) { backdrop = node; } } };
    const window = { confirm(text) { prompts.push(text); return confirm; }, prompt(text) { prompts.push(text); return prompt; }, Notificaciones: { mostrar(msg, type) { messages.push({ msg, type }); } }, Peticiones: { async enviar(url, options = {}) { requests.push({ url, method: options.method || 'GET', body: options.body ? Object.fromEntries(options.body.entries()) : null }); return url.includes('RecursosCancelacion') ? { ok: consultaOk, async json() { return { success: true, registrados: [{ nombre: 'Jabón', cantidad: 0.3, unidad: 'l' }], productos: [{ id: 'i', nombre: 'Jabón', unidad: 'l' }] }; } } : { ok: true, async json() { return { success: postSuccess, message: postSuccess ? 'ok' : 'rechazado' }; } }; } }, location: { reload() {} } };
    class FakeFormData extends FormData { constructor() { super(); } }
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/', script), 'utf8'), { window, document, FormData: FakeFormData, URLSearchParams, console, setTimeout() {}, clearTimeout });
    if (script === 'lavado.js') listeners[1](); else listeners[0]();
    elements.cancelarLavadoId.value = 'l'; elements.cancelarServicioId.value = 's'; elements.cancelarTipo.value = 'servicio'; elements.motivoCancelacion.value = 'motivo'; elements.formCancelar.action = '/Lavados/CancelarServicio';
    const interact = modalHarness(document, window, invalid || (prompt === null ? 'cancel' : confirm ? 'additional' : 'zero'), prompts, () => { if (inspect) { assert.equal(classes.has('hidden'), true); assert.equal(backdrop, null); } });
    return { requests, messages, prompts, elements, classes, async submit() { const pending = elements.formCancelar.submit({ preventDefault() {} }); if (consultaOk) await interact(pending); else await pending; } };
}

for (const script of ['lavado.js', 'lavado-detalle.js']) {
    for (const invalid of ['cancel', 'escape']) test(`${script}: ${invalid} restaura motivo sin POST`, async () => {
        const p = cancellationPage(script, { invalid, inspect: true });
        await p.submit();
        assert.equal(p.classes.has('hidden'), false);
        assert.equal(p.elements.motivoCancelacion.value, 'motivo');
        assert.equal(p.requests.length, 1);
    });
    test(`${script}: cancelación envía cero explícito y registrado separado`, async () => {
        const p = cancellationPage(script); await p.submit();
        assert.match(p.prompts[0], /Jabón<\/span><span>0.3 L/);
        assert.equal(p.requests[1].body.consumosNoRegistradosJson, '{}');
        assert.ok(!p.messages.some(m => m.type === 'success'));
    });
    test(`${script}: cancelación envía consumo adicional en unidad propia`, async () => {
        const p = cancellationPage(script, { confirm: true, prompt: '0.2' }); await p.submit();
        assert.match(p.prompts[0], /Jabón \(L\)/);
        assert.equal(p.requests[1].body.consumosNoRegistradosJson, '{"i":0.2}');
    });
    test(`${script}: cantidades negativas y NaN no envían POST`, async () => {
        for (const invalid of ['negative', 'nan']) {
            const p = cancellationPage(script, { invalid }); await p.submit();
            assert.equal(p.requests.length, 1);
            assert.ok(!p.messages.some(m => m.type === 'success'));
        }
    });
    test(`${script}: abortar cantidad o fallar consulta no envía POST`, async () => {
        for (const options of [{ confirm: true, prompt: null }, { consultaOk: false }]) {
            const p = cancellationPage(script, options); await p.submit();
            assert.equal(p.requests.length, 1);
            assert.ok(!p.messages.some(m => m.type === 'success'));
        }
    });
}

test('iniciar etapa exige selección explícita y no envía POST al cancelar', async () => {
    const listeners = [], requests = [], errors = [];
    const document = { addEventListener(t, f) { if (t === 'DOMContentLoaded') listeners.push(f); }, querySelector() { return null; }, querySelectorAll() { return []; }, getElementById() { return null; } };
    const window = { prompt() { return null; }, Notificaciones: { mostrar(m, t) { errors.push({m,t}); } }, Peticiones: { async enviar(url, options = {}) { requests.push({url, options}); return { ok: true, async json() { return { success: true, items: [{ indice: 0, nombre: 'Jabón', opciones: [{ id: 'i', nombre: 'Producto', unidad: 'l' }] }] }; } }; } } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/lavado-detalle.js'), 'utf8'), { window, document, FormData, URLSearchParams, console, setTimeout, clearTimeout });
    const interact = modalHarness(document, window, 'cancel', []);
    await interact(window.iniciarEtapaDesdeDetalle('l', 's', 'e'));
    assert.equal(requests.length, 1);
    assert.match(requests[0].url, /RecursosInicio/);
});

for (const script of ['lavado.js', 'lavado-detalle.js']) {
    test(`${script}: inicio transporta selección positiva por índice`, async () => {
        const requests = [], document = { addEventListener() {}, querySelector() { return null; }, querySelectorAll() { return []; }, getElementById() { return null; } };
        const window = { prompt() { return '1'; }, Notificaciones: { mostrar() {} }, Peticiones: { async enviar(url, options = {}) { requests.push({ url, body: options.body ? Object.fromEntries(options.body.entries()) : null }); return { ok: true, async json() { return url.includes('RecursosInicio') ? { success: true, items: [{ indice: 2, nombre: 'Jabón', opciones: [{ id: 'i', nombre: 'Producto', unidad: 'l' }] }] } : { success: false, message: 'rechazado' }; } }; } } };
        vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/', script), 'utf8'), { window, document, FormData, URLSearchParams, console, setTimeout() {}, clearTimeout });
        const interact = modalHarness(document, window, 'select', []);
        await interact(window.iniciarEtapaDesdeDetalle('l', 's', 'e'));
        assert.equal(requests[1].url, '/Lavados/IniciarEtapa');
        assert.equal(requests[1].body.seleccionJson, '{"2":"i"}');
        assert.equal(requests[1].body.etapaId, 'e');
    });
}

test('alta transporta selección positiva del primer servicio por vehículo', async () => {
    const listeners = [], requests = [], form = { addEventListener(t, f) { this[t] = f; } };
    const document = { addEventListener(t, f) { if (t === 'DOMContentLoaded') listeners.push(f); }, getElementById(id) { return id === 'lavado-form' ? form : null; }, querySelector() { return null; }, querySelectorAll() { return []; } };
    const window = { lavadoData: { clienteId: 'c', vehiculosSeleccionados: ['v'], serviciosPorVehiculo: { v: [{ id: 's' }] } }, prompt() { return '1'; }, Notificaciones: { mostrar() {} }, Peticiones: { async enviar(url, options = {}) { requests.push({ url, body: options.body }); return { ok: true, async json() { return url.includes('RecursosInicio') ? { success: true, items: [{ indice: 2, nombre: 'Jabón', opciones: [{ id: 'i', nombre: 'Producto', unidad: 'l' }] }] } : { success: false, message: 'rechazado' }; } }; } } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/lavado.js'), 'utf8'), { window, document, FormData, URLSearchParams, console, setTimeout() {}, clearTimeout });
    listeners[0]();
    const interact = modalHarness(document, window, 'select', []);
    await interact(form.submit({ preventDefault() {} }));
    assert.match(requests[0].url, /servicioId=s/);
    assert.equal(requests[1].url, '/Lavados/CrearLavadoAjax');
    assert.deepEqual(JSON.parse(requests[1].body).selecciones, { v: { '2': 'i' } });
});

const receta = { dataset: { receta: JSON.stringify([{ InsumoId: 'quimico', NombreInsumo: 'Jabón', UnidadMedida: 'l', DosisTeorica: 2 }]) } };

test('finalizar etapa exige justificación solo ante desvío', async () => {
    const p = page();
    p.open('l', 's', 'e', 'etapa', 'Etapa', receta);
    p.fields()[0].value = '3'; p.fields()[0].input();
    await p.submit();
    assert.equal(p.requests.length, 0);
    assert.equal(p.elements.justificacionDesvio.required, true);
    p.fields()[0].value = '2'; p.fields()[0].input();
    assert.equal(p.elements.justificacionDesvio.required, false);
    await p.submit();
    assert.equal(p.requests.length, 1);
});

test('selección vacía no envía POST', async () => {
    const requests = [], document = { addEventListener() {}, querySelector() { return null; }, querySelectorAll() { return []; }, getElementById() { return null; } };
    const window = { Notificaciones: { mostrar() {} }, Peticiones: { async enviar(url) { requests.push(url); return { ok: true, async json() { return { success: true, items: [{ indice: 0, nombre: 'Jabón', opciones: [{ id: 'i', nombre: 'Producto', unidad: 'l' }] }] }; } }; } } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/lavado-detalle.js'), 'utf8'), { window, document, FormData, URLSearchParams, console, setTimeout, clearTimeout });
    const interact = modalHarness(document, window, 'missing', []);
    await interact(window.iniciarEtapaDesdeDetalle('l', 's', 'e'));
    assert.equal(requests.length, 1);
});

test('finalizar etapa envía consumo editado y justificación', async () => {
    const p = page();
    p.open('lavado-1', 'servicio-1', 'etapa-1', 'etapa', 'Enjuague', receta);
    p.fields()[0].value = '3.5';
    p.fields()[0].input();
    p.elements.justificacionDesvio.value = 'Mayor suciedad';
    await p.submit();
    assert.deepEqual(p.requests, [{ url: '/Lavados/FinalizarEtapa', method: 'POST', body: { lavadoId: 'lavado-1', servicioId: 'servicio-1', etapaId: 'etapa-1', consumoRealJson: '{"quimico":3.5}', justificacionDesvio: 'Mayor suciedad' } }]);
});

test('finalizar etapa envía dosis inicial y receta vacía sin edición', async () => {
    for (const [source, expected] of [[receta, '{"quimico":2}'], [undefined, '{}']]) {
        const p = page();
        p.open('l', 's', 'e', 'etapa', 'Etapa', source);
        await p.submit();
        assert.equal(p.requests[0].body.consumoRealJson, expected);
        assert.equal(p.requests[0].body.justificacionDesvio, '');
    }
});

test('finalización de lavado y servicio conserva ruta y parámetros', async () => {
    for (const [tipo, url, body] of [
        ['lavado', '/Lavados/FinalizarLavado', { id: 'l' }],
        ['servicio', '/Lavados/FinalizarServicio', { lavadoId: 'l', servicioId: 's' }]
    ]) {
        const p = page();
        p.open('l', 's', '', tipo, 'Nombre');
        await p.submit();
        assert.deepEqual(p.requests, [{ url, method: 'POST', body }]);
    }
});
