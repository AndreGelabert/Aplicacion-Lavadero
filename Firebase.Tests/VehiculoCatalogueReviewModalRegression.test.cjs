const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('vehiculo-api.js manual catalogue selection and visual feedback', async () => {
    // Setup simulated DOM for quick vehicle modal
    const elements = {};
    const createElement = (tag, id = '') => {
        const el = {
            tagName: tag.toUpperCase(),
            id,
            dataset: {},
            value: '',
            options: [],
            classList: {
                _classes: new Set(),
                add(c) { this._classes.add(c); },
                remove(c) { this._classes.delete(c); },
                contains(c) { return this._classes.has(c); }
            },
            style: {},
            children: [],
            appendChild(child) { this.children.push(child); child.parentElement = this; },
            addEventListener(evt, fn) { this['on' + evt] = fn; },
            setAttribute(k, v) { this[k] = v; },
            getAttribute(k) { return this[k] || null; },
            removeAttribute(k) { delete this[k]; },
            hasAttribute(k) { return k in this; },
            focus() { this.focused = true; },
            setCustomValidity(msg) { this._customValidity = msg; },
            reportValidity() { return !this._customValidity; },
            getBoundingClientRect() { return { top: 0, bottom: 30, left: 0, width: 200 }; },
            closest(selector) { if (selector === 'form') return elements['vehiculo-form']; return null; }
        };
        Object.defineProperty(el, 'textContent', {
            get() { return this._text !== undefined ? this._text : (this.children.map(c => c.textContent).join(' ')); },
            set(t) { this._text = t; }
        });
        if (id) elements[id] = el;
        return el;
    };

    createElement('form', 'vehiculo-form');
    createElement('input', 'Marca');
    createElement('div', 'marca-dropdown');
    createElement('div', 'marca-manual-badge-container');
    createElement('input', 'Modelo');
    createElement('div', 'modelo-dropdown');
    createElement('div', 'modelo-manual-badge-container');
    createElement('select', 'TipoVehiculo');
    createElement('select', 'Color');
    createElement('input', 'ColorCustom');
    createElement('input', 'color-custom-toggle');

    const documentMock = {
        readyState: 'complete',
        body: createElement('body'),
        getElementById(id) { return elements[id] || null; },
        querySelector(sel) {
            if (sel === '#quick-create-modal #vehiculo-form') return elements['vehiculo-form'];
            if (sel.includes('script')) return null;
            return null;
        },
        querySelectorAll() { return []; },
        addEventListener() {},
        createElement(tag) { return createElement(tag); }
    };

    const windowMock = {
        Peticiones: {
            async enviar(url) {
                if (url.includes('GetMarcas')) {
                    return { ok: true, json: async () => [{ id: '1', nombre: 'Toyota' }, { id: '2', nombre: 'Ford' }] };
                }
                if (url.includes('GetColores')) {
                    return { ok: true, json: async () => ['Blanco', 'Negro', 'Rojo'] };
                }
                return { ok: true, json: async () => [] };
            }
        },
        addEventListener() {}
    };

    const scriptPath = path.join(__dirname, '../wwwroot/js/vehiculo-api.js');
    const scriptCode = fs.readFileSync(scriptPath, 'utf8');

    vm.runInNewContext(scriptCode, {
        window: windowMock,
        document: documentMock,
        console,
        setTimeout(fn) { fn(); },
        clearTimeout() {},
        setInterval() {},
        clearInterval() {},
        MutationObserver: class { observe() {} }
    });

    // 1. Initial state
    assert.strictEqual(typeof windowMock.initVehiculoApiSelects, 'function');
    windowMock.initVehiculoApiSelects();
    await new Promise(r => setTimeout(r, 10));

    // 2. Select manual Brand: enter "Pagani"
    const marcaInput = elements['Marca'];
    const marcaDropdown = elements['marca-dropdown'];
    marcaInput.value = 'Pagani';
    marcaInput.oninput();

    // Verify manual button was added to dropdown
    const manualBtn = marcaDropdown.children.find(c => c.tagName === 'BUTTON');
    assert.ok(manualBtn, 'Dropdown should contain manual option button');
    assert.match(manualBtn.textContent, /usar “Pagani” como marca nueva/);

    // Verify mousedown triggers selection and prevents blur race
    let defaultPrevented = false;
    manualBtn.onmousedown({
        preventDefault() { defaultPrevented = true; },
        stopPropagation() {}
    });
    assert.ok(defaultPrevented, 'mousedown should call preventDefault() to avoid blur race');

    // Verify state after selecting manual brand
    assert.strictEqual(marcaInput.dataset.catalogueManual, 'true');
    assert.strictEqual(marcaInput._customValidity, '');
    assert.match(elements['marca-manual-badge-container'].innerHTML, /Marca nueva.*Pagani.*pendiente de revisión/);
    assert.strictEqual(elements['Modelo'].disabled, false);
    assert.ok(elements['Modelo'].focused, 'Focus should move automatically to Modelo');

    // 3. Select manual Model: enter "Utopia"
    const modeloInput = elements['Modelo'];
    const modeloDropdown = elements['modelo-dropdown'];
    modeloInput.value = 'Utopia';
    modeloInput.oninput();

    const manualModelBtn = modeloDropdown.children.find(c => c.tagName === 'BUTTON');
    assert.ok(manualModelBtn, 'Dropdown should contain manual model button');
    assert.match(manualModelBtn.textContent, /usar “Utopia” como modelo nueva/);

    manualModelBtn.onmousedown({
        preventDefault() {},
        stopPropagation() {}
    });

    assert.strictEqual(modeloInput.dataset.catalogueManual, 'true');
    assert.strictEqual(modeloInput._customValidity, '');
    assert.match(elements['modelo-manual-badge-container'].innerHTML, /Modelo nuevo.*Utopia.*pendiente de revisión/);
    assert.ok(elements['Color'].focused, 'Focus should move automatically to Color');
});

test('vehiculo.js catalogoRevisionModal AJAX workflows', async () => {
    const elements = {};
    const domListeners = [];
    const createElement = (tag, id = '') => {
        const el = {
            tagName: tag.toUpperCase(),
            id,
            dataset: {},
            value: '',
            children: [],
            classList: {
                _classes: new Set(),
                add(c) { this._classes.add(c); },
                remove(c) { this._classes.delete(c); },
                contains(c) { return this._classes.has(c); }
            },
            appendChild(child) { this.children.push(child); child.parentElement = this; },
            querySelectorAll(sel) {
                const results = [];
                const collect = (node) => {
                    if (sel === 'button[data-decision]' && node.tagName === 'BUTTON' && node.dataset?.decision) {
                        results.push(node);
                    } else if (sel === 'button' && node.tagName === 'BUTTON') {
                        results.push(node);
                    } else if (sel.startsWith('input[data-field=')) {
                        const field = sel.match(/data-field="([^"]+)"/)[1];
                        if (node.tagName === 'INPUT' && node.dataset?.field === field) results.push(node);
                    }
                    for (const ch of node.children || []) collect(ch);
                };
                collect(this);
                return results;
            },
            querySelector(sel) {
                const list = this.querySelectorAll(sel);
                return list.length ? list[0] : null;
            },
            addEventListener(evt, fn) { this['on' + evt] = fn; }
        };
        Object.defineProperty(el, 'textContent', {
            get() { return this._text !== undefined ? this._text : (this.children.map(c => c.textContent).join(' ')); },
            set(t) { this._text = String(t); }
        });
        Object.defineProperty(el, 'innerHTML', {
            get() { return this._html || ''; },
            set(html) {
                this._html = html;
                this.children = [];
                // If html contains input[data-field="brand"], create child elements
                const brandMatch = html.match(/data-field="brand"[^>]*value="([^"]*)"/);
                if (brandMatch) {
                    const brandInput = createElement('input');
                    brandInput.dataset.field = 'brand';
                    brandInput.value = brandMatch[1];
                    this.appendChild(brandInput);
                }
                const modelMatch = html.match(/data-field="model"[^>]*value="([^"]*)"/);
                if (modelMatch) {
                    const modelInput = createElement('input');
                    modelInput.dataset.field = 'model';
                    modelInput.value = modelMatch[1];
                    this.appendChild(modelInput);
                }
                for (const dec of ['approve', 'link', 'discard']) {
                    if (html.includes(`data-decision="${dec}"`)) {
                        const btn = createElement('button');
                        btn.dataset.decision = dec;
                        this.appendChild(btn);
                    }
                }
            }
        });
        if (id) elements[id] = el;
        return el;
    };

    createElement('div', 'catalogoRevisionModal');
    elements['catalogoRevisionModal'].dataset.listUrl = '/VehicleCatalogueReview/ModalDatos';
    elements['catalogoRevisionModal'].dataset.resolveUrl = '/VehicleCatalogueReview/ResolveAjax';
    createElement('tbody', 'catalogoRevisionTabla');
    createElement('div', 'catalogoRevisionVacio');
    createElement('div', 'catalogoRevisionMensaje');
    createElement('span', 'badge-catalogo-revision');
    createElement('button', 'btn-catalogo-revision');

    const documentMock = {
        readyState: 'complete',
        getElementById(id) { return elements[id] || null; },
        querySelector(sel) {
            if (sel === '[data-open-catalogo-revision]' || sel === '#btn-catalogo-revision') return elements['btn-catalogo-revision'];
            if (sel === '[data-close-catalogo-revision]') return createElement('button');
            if (sel.includes('__RequestVerificationToken')) return { value: 'test-token' };
            return null;
        },
        querySelectorAll() { return []; },
        addEventListener(evt, fn) { if (evt === 'DOMContentLoaded') domListeners.push(fn); },
        createElement(tag) { return createElement(tag); }
    };

    const sentRequests = [];
    let pendingData = [
        { vehiculoId: 'v1', tipoVehiculo: 'Auto', marca: 'Tesla', marcaManual: true, modelo: 'Cybertruck', modeloManual: true }
    ];

    const windowMock = {
        Peticiones: {
            async enviar(url, options = {}) {
                sentRequests.push({ url, options });
                if (url.includes('ModalDatos')) {
                    return { ok: true, json: async () => pendingData };
                }
                if (url.includes('ResolveAjax')) {
                    pendingData = []; // resolved
                    return { ok: true, json: async () => ({ exito: true, mensaje: 'La propuesta fue revisada correctamente.' }) };
                }
                if (url.includes('GetTiposVehiculo')) {
                    return { ok: true, json: async () => [] };
                }
                return { ok: true, json: async () => ({}) };
            }
        },
        addEventListener() {},
        location: { search: '' },
        reloadVehiculoTable: () => {}
    };

    const scriptPath = path.join(__dirname, '../wwwroot/js/vehiculo.js');
    const scriptCode = fs.readFileSync(scriptPath, 'utf8');

    vm.runInNewContext(scriptCode, {
        window: windowMock,
        document: documentMock,
        console,
        setTimeout(fn) { fn(); },
        clearTimeout() {},
        setInterval() {},
        clearInterval() {},
        URLSearchParams: class { get() { return null; } },
        FormData: class {
            constructor() { this._data = {}; }
            append(k, v) { this._data[k] = v; }
        }
    });

    // Run DOMContentLoaded
    for (const listener of domListeners) listener();

    // Verify initial count fetch updated the badge
    await new Promise(r => setTimeout(r, 20));
    assert.strictEqual(elements['badge-catalogo-revision'].textContent, '1');
    assert.strictEqual(elements['badge-catalogo-revision'].classList.contains('hidden'), false);

    // Open modal via button
    elements['btn-catalogo-revision'].onclick({ preventDefault() {} });
    await new Promise(r => setTimeout(r, 20));

    // Verify row was rendered
    const tbody = elements['catalogoRevisionTabla'];
    assert.strictEqual(tbody.children.length, 1);
    const row = tbody.children[0];
    assert.strictEqual(row.dataset.proposalId, 'v1');

    // Simulate clicking "Aprobar"
    const approveBtn = row.querySelectorAll('button[data-decision]').find(c => c.dataset?.decision === 'approve');
    assert.ok(approveBtn, 'Aprobar button should exist');
    await approveBtn.onclick();
    await new Promise(r => setTimeout(r, 20));

    // Verify POST was sent with approve
    const resolveCall = sentRequests.find(r => r.url.includes('ResolveAjax'));
    assert.ok(resolveCall, 'ResolveAjax request should have been made');
    assert.strictEqual(resolveCall.options.body._data.id, 'v1');
    assert.strictEqual(resolveCall.options.body._data.decision, 'approve');

    // Verify badge updated to hidden when pendingData became empty
    assert.strictEqual(elements['badge-catalogo-revision'].textContent, '');
    assert.strictEqual(elements['badge-catalogo-revision'].classList.contains('hidden'), true);
});
