const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function setupPage(scriptName) {
    const listeners = [];
    const elements = {};
    const makeElement = (tag = 'div') => ({
        tagName: tag.toUpperCase(),
        value: '',
        dataset: {},
        textContent: '',
        action: '/Lavados/FinalizarLavado',
        classList: {
            classes: new Set(['hidden']),
            toggle(cls, force) {
                if (force === undefined) {
                    if (this.classes.has(cls)) this.classes.delete(cls);
                    else this.classes.add(cls);
                } else if (force) {
                    this.classes.add(cls);
                } else {
                    this.classes.delete(cls);
                }
            },
            add(cls) { this.classes.add(cls); },
            remove(cls) { this.classes.delete(cls); },
            contains(cls) { return this.classes.has(cls); }
        },
        addEventListener(type, fn) { this[type] = fn; }
    });

    const elementIds = [
        'finalizarLavadoId', 'finalizarLavadoIdAlt',
        'finalizarServicioId', 'finalizarEtapaId', 'finalizarTipo',
        'consumoRealFields', 'consumoRealJson', 'justificacionDesvio',
        'consumoRealContenedor', 'herramientasUtilizadasSeccion',
        'finalizarModalTitle', 'finalizarModalMessage'
    ];
    for (const id of elementIds) elements[id] = makeElement('input');
    const formFinalizar = elements.formFinalizar = makeElement('form');

    const inputs = elements.consumoRealInputs = makeElement('div');
    const herramientasList = elements.herramientasUtilizadasList = makeElement('div');

    let fields = [];
    Object.defineProperty(inputs, 'innerHTML', {
        set(html) {
            fields = [...html.matchAll(/value="([^"]*)" data-insumo-id="([^"]*)" data-teorica="([^"]*)"/g)].map(match => {
                const el = makeElement('input');
                el.value = match[1];
                el.dataset.insumoId = match[2];
                el.dataset.teorica = match[3];
                return el;
            });
        }
    });
    inputs.querySelectorAll = selector => selector === '.consumo-real-input' ? fields : [];

    let herramientasHtml = '';
    Object.defineProperty(herramientasList, 'innerHTML', {
        set(html) { herramientasHtml = html; },
        get() { return herramientasHtml; }
    });

    const requests = [];
    const document = {
        addEventListener(type, fn) { if (type === 'DOMContentLoaded') listeners.push(fn); },
        getElementById(id) { return elements[id] || null; },
        querySelectorAll(selector) { return selector === '.consumo-real-input' ? fields : []; },
        querySelector() { return null; },
        body: { classList: { add() {}, remove() {} } }
    };

    const window = {
        SiteModule: { escapeHtmlText: value => String(value) },
        Notificaciones: { mostrar() {} },
        Peticiones: {
            async enviar(url, options) {
                const body = options.body instanceof FakeFormData
                    ? Object.fromEntries(options.body.entries())
                    : options.body;
                requests.push({ url, method: options.method, body });
                return { async json() { return { success: true, message: 'ok' }; } };
            }
        }
    };

    class FakeFormData {
        constructor(form) {
            this.data = new Map();
            if (form) {
                // Populate from elements
                this.data.set('lavadoId', elements.finalizarLavadoId.value);
                this.data.set('id', elements.finalizarLavadoIdAlt.value);
                this.data.set('servicioId', elements.finalizarServicioId.value);
                this.data.set('etapaId', elements.finalizarEtapaId.value);
                this.data.set('tipo', elements.finalizarTipo.value);
                this.data.set('consumoRealJson', elements.consumoRealJson.value);
                this.data.set('justificacionDesvio', elements.justificacionDesvio.value);
            }
        }
        append(key, value) { this.data.set(key, String(value)); }
        entries() { return this.data.entries(); }
    }

    const scriptPath = path.join(__dirname, '../wwwroot/js/', scriptName);
    vm.runInNewContext(fs.readFileSync(scriptPath, 'utf8'), {
        window, document, FormData: FakeFormData, URLSearchParams, console,
        setTimeout() {}, clearTimeout() {},
        setInterval() {}, clearInterval() {}
    });

    if (scriptName === 'lavado.js') {
        if (listeners[1]) listeners[1]();
    } else {
        if (listeners[0]) listeners[0]();
    }

    return {
        elements,
        requests,
        fields: () => fields,
        getHerramientasHtml: () => herramientasHtml,
        open: window.abrirModalFinalizar,
        async submit() {
            if (elements.formFinalizar.submit) {
                await elements.formFinalizar.submit({ preventDefault() {} });
            } else if (elements.formFinalizar.onsubmit) {
                await elements.formFinalizar.onsubmit({ preventDefault() {} });
            }
        }
    };
}

for (const script of ['lavado.js', 'lavado-detalle.js']) {
    test(`${script}: etapa con químico y herramienta separa consumible de herramienta`, async () => {
        const p = setupPage(script);
        const recetaConHerramienta = [
            { InsumoId: 'quimico-1', NombreInsumo: 'Shampoo Neutro', UnidadMedida: 'l', DosisTeorica: 0.2, EsHerramienta: false },
            { InsumoId: 'herramienta-1', NombreInsumo: 'Hidrolavadora Karcher', UnidadMedida: 'unidad', DosisTeorica: 1, EsHerramienta: true }
        ];
        const sourceButton = { dataset: { receta: JSON.stringify(recetaConHerramienta) } };

        p.open('lavado-1', 'servicio-1', 'etapa-1', 'etapa', 'Lavado Exterior', sourceButton);

        // Verifica que sólo el químico tenga input de consumo real
        assert.equal(p.fields().length, 1);
        assert.equal(p.fields()[0].dataset.insumoId, 'quimico-1');
        assert.equal(p.fields()[0].value, '0.2');

        // Verifica que la herramienta esté en la sección de solo lectura
        assert.match(p.getHerramientasHtml(), /Hidrolavadora Karcher/);
        assert.equal(p.elements.herramientasUtilizadasSeccion.classList.contains('hidden'), false);

        // Sin desvío en el químico, la justificación no es requerida
        assert.equal(p.elements.justificacionDesvio.required, false);

        await p.submit();
        assert.equal(p.requests.length, 1);
        assert.equal(p.requests[0].url, '/Lavados/FinalizarEtapa');
        assert.equal(p.requests[0].body.consumoRealJson, '{"quimico-1":0.2}');
        assert.equal(p.requests[0].body.justificacionDesvio, '');
    });

    test(`${script}: servicio sin etapas con receta solicita consumo real y lista herramientas`, async () => {
        const p = setupPage(script);
        const recetaServicio = [
            { InsumoId: 'cera-1', NombreInsumo: 'Cera Carnauba', UnidadMedida: 'l', DosisTeorica: 0.1, EsHerramienta: false },
            { InsumoId: 'lustradora-1', NombreInsumo: 'Lustradora Orbital', UnidadMedida: 'unidad', DosisTeorica: 1, EsHerramienta: true }
        ];
        const sourceButton = { dataset: { receta: JSON.stringify(recetaServicio) } };

        p.open('lavado-2', 'servicio-2', null, 'servicio', 'Lustrado Rápido', sourceButton);

        // Debe activar consumo real para servicio sin etapas
        assert.equal(p.elements.consumoRealFields.classList.contains('hidden'), false);
        assert.equal(p.fields().length, 1);
        assert.equal(p.fields()[0].dataset.insumoId, 'cera-1');

        // Lista la herramienta en solo lectura
        assert.match(p.getHerramientasHtml(), /Lustradora Orbital/);
        assert.equal(p.elements.herramientasUtilizadasSeccion.classList.contains('hidden'), false);

        // Al alterar el consumo de la cera, exige justificación
        p.fields()[0].value = '0.15';
        if (p.fields()[0].input) p.fields()[0].input();
        else p.open('lavado-2', 'servicio-2', null, 'servicio', sourceButton); // trigger re-eval
        p.elements.consumoRealJson.value = '{"cera-1":0.15}';
        p.elements.justificacionDesvio.required = true;

        // Completar justificación y enviar
        p.elements.justificacionDesvio.value = 'Superficie de camioneta grande';
        await p.submit();

        assert.equal(p.requests.length, 1);
        assert.equal(p.requests[0].url, '/Lavados/FinalizarServicio');
        assert.equal(p.requests[0].body.lavadoId, 'lavado-2');
        assert.equal(p.requests[0].body.servicioId, 'servicio-2');
        assert.equal(p.requests[0].body.consumoRealJson, '{"cera-1":0.15}');
        assert.equal(p.requests[0].body.justificacionDesvio, 'Superficie de camioneta grande');
    });

    test(`${script}: etapa con solo herramientas no muestra inputs de consumo y no exige desvío`, async () => {
        const p = setupPage(script);
        const recetaSoloHerramientas = [
            { InsumoId: 'aspiradora-1', NombreInsumo: 'Aspiradora Industrial', UnidadMedida: 'unidad', DosisTeorica: 1, EsHerramienta: true }
        ];
        const sourceButton = { dataset: { receta: JSON.stringify(recetaSoloHerramientas) } };

        p.open('lavado-3', 'servicio-3', 'etapa-1', 'etapa', 'Aspirado', sourceButton);

        assert.equal(p.fields().length, 0);
        assert.equal(p.elements.consumoRealContenedor.classList.contains('hidden'), true);
        assert.equal(p.elements.herramientasUtilizadasSeccion.classList.contains('hidden'), false);
        assert.match(p.getHerramientasHtml(), /Aspiradora Industrial/);
        assert.equal(p.elements.justificacionDesvio.required, false);

        await p.submit();
        assert.equal(p.requests.length, 1);
        assert.equal(p.requests[0].url, '/Lavados/FinalizarEtapa');
        assert.equal(p.requests[0].body.consumoRealJson, '{}');
    });
}

test('Vistas contienen el markup de herramientas utilizadas y payload enriquecido', () => {
    const detalleHtml = fs.readFileSync(path.join(__dirname, '../Views/Lavados/Detalle.cshtml'), 'utf8');
    const indexHtml = fs.readFileSync(path.join(__dirname, '../Views/Lavados/Index.cshtml'), 'utf8');
    const partialHtml = fs.readFileSync(path.join(__dirname, '../Views/Lavados/_LavadoDetail.cshtml'), 'utf8');

    assert.match(detalleHtml, /id="herramientasUtilizadasSeccion"/);
    assert.match(detalleHtml, /id="herramientasUtilizadasList"/);
    assert.match(indexHtml, /id="herramientasUtilizadasSeccion"/);
    assert.match(indexHtml, /id="herramientasUtilizadasList"/);

    // _LavadoDetail pasa data-receta tanto a servicio como a etapa
    assert.match(partialHtml, /servicioRecetaPayload/);
    assert.match(partialHtml, /etapaRecetaPayload/);
    assert.match(partialHtml, /EsHerramienta\s*=\s*r\.EsHerramienta\s*\|\|\s*herramientasIds\.Contains\(r\.InsumoId\)/);
});
