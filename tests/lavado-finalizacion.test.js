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
        fields = [...html.matchAll(/value="([^"]*)" data-insumo-id="([^"]*)"/g)].map(match => ({ ...element(), value: match[1], dataset: { insumoId: match[2] } }));
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
    const window = { Notificaciones: { mostrar() {} }, Peticiones: { async enviar(url, options) { requests.push({ url, method: options.method, body: Object.fromEntries(options.body.entries()) }); return { async json() { return { success: false, message: 'rechazado' }; } }; } } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/lavado.js'), 'utf8'), { window, document, FormData, URLSearchParams, console, setTimeout, clearTimeout });
    listeners[1]();
    return { elements, requests, fields: () => fields, open: window.abrirModalFinalizar, async submit() { await elements.formFinalizar.submit({ preventDefault() {} }); } };
}

function cancellationPage(script, { confirm = false, prompt = '0.2', consultaOk = true, postSuccess = false } = {}) {
    const listeners = [], requests = [], messages = [], prompts = [];
    const elements = {};
    const make = () => ({ value: '', dataset: {}, classList: { add() {}, remove() {}, toggle() {} }, addEventListener(type, fn) { this[type] = fn; }, setAttribute() {} });
    for (const id of ['formCancelar', 'cancelarLavadoId', 'cancelarLavadoIdAlt', 'cancelarServicioId', 'cancelarEtapaId', 'cancelarTipo', 'motivoCancelacion']) elements[id] = make();
    const document = { addEventListener(type, fn) { if (type === 'DOMContentLoaded') listeners.push(fn); }, getElementById(id) { return elements[id] || null; }, querySelectorAll() { return []; }, querySelector() { return null; }, body: { classList: { add() {}, remove() {} } } };
    const window = { confirm(text) { prompts.push(text); return confirm; }, prompt(text) { prompts.push(text); return prompt; }, Notificaciones: { mostrar(msg, type) { messages.push({ msg, type }); } }, Peticiones: { async enviar(url, options = {}) { requests.push({ url, method: options.method || 'GET', body: options.body ? Object.fromEntries(options.body.entries()) : null }); return url.includes('RecursosCancelacion') ? { ok: consultaOk, async json() { return { success: true, registrados: [{ nombre: 'Jabón', cantidad: 0.3, unidad: 'l' }], productos: [{ id: 'i', nombre: 'Jabón', unidad: 'l' }] }; } } : { ok: true, async json() { return { success: postSuccess, message: postSuccess ? 'ok' : 'rechazado' }; } }; } }, location: { reload() {} } };
    class FakeFormData extends FormData { constructor() { super(); } }
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/', script), 'utf8'), { window, document, FormData: FakeFormData, URLSearchParams, console, setTimeout() {}, clearTimeout });
    if (script === 'lavado.js') listeners[1](); else listeners[0]();
    elements.cancelarLavadoId.value = 'l'; elements.cancelarServicioId.value = 's'; elements.cancelarTipo.value = 'servicio'; elements.motivoCancelacion.value = 'motivo'; elements.formCancelar.action = '/Lavados/CancelarServicio';
    return { requests, messages, prompts, submit: () => elements.formCancelar.submit({ preventDefault() {} }) };
}

for (const script of ['lavado.js', 'lavado-detalle.js']) {
    test(`${script}: cancelación envía cero explícito y registrado separado`, async () => {
        const p = cancellationPage(script); await p.submit();
        assert.match(p.prompts[0], /Jabón: 0.3 l/);
        assert.equal(p.requests[1].body.consumosNoRegistradosJson, '{}');
        assert.ok(!p.messages.some(m => m.type === 'success'));
    });
    test(`${script}: cancelación envía consumo adicional en unidad propia`, async () => {
        const p = cancellationPage(script, { confirm: true, prompt: '0.2' }); await p.submit();
        assert.match(p.prompts[1], /Jabón \(l\)/);
        assert.equal(p.requests[1].body.consumosNoRegistradosJson, '{"i":0.2}');
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
    await window.iniciarEtapaDesdeDetalle('l', 's', 'e');
    assert.equal(requests.length, 1);
    assert.match(requests[0].url, /RecursosInicio/);
});

for (const script of ['lavado.js', 'lavado-detalle.js']) {
    test(`${script}: inicio transporta selección positiva por índice`, async () => {
        const requests = [], document = { addEventListener() {}, querySelector() { return null; }, querySelectorAll() { return []; }, getElementById() { return null; } };
        const window = { prompt() { return '1'; }, Notificaciones: { mostrar() {} }, Peticiones: { async enviar(url, options = {}) { requests.push({ url, body: options.body ? Object.fromEntries(options.body.entries()) : null }); return { ok: true, async json() { return url.includes('RecursosInicio') ? { success: true, items: [{ indice: 2, nombre: 'Jabón', opciones: [{ id: 'i', nombre: 'Producto', unidad: 'l' }] }] } : { success: false, message: 'rechazado' }; } }; } } };
        vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/', script), 'utf8'), { window, document, FormData, URLSearchParams, console, setTimeout() {}, clearTimeout });
        await window.iniciarEtapaDesdeDetalle('l', 's', 'e');
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
    await form.submit({ preventDefault() {} });
    assert.match(requests[0].url, /servicioId=s/);
    assert.equal(requests[1].url, '/Lavados/CrearLavadoAjax');
    assert.deepEqual(JSON.parse(requests[1].body).selecciones, { v: { '2': 'i' } });
});

const receta = { dataset: { receta: JSON.stringify([{ InsumoId: 'quimico', NombreInsumo: 'Jabón', DosisTeorica: 2 }]) } };

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
