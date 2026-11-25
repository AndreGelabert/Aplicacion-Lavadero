const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const read = file => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');

test('El modal de tipos de insumo no ofrece el botón de borrado físico en sus filas', () => {
    const jsContent = read('wwwroot/js/insumos.js');

    // La función refresh no debe añadir el botón Eliminar a las acciones del modal
    assert.doesNotMatch(
        jsContent,
        /actions\.append\(button\(['"]Eliminar['"]/g,
        'No debe añadirse el botón de Eliminar en la tabla del modal de tipos de insumo'
    );

    // Debe conservar Editar y Desactivar / Reactivar
    assert.match(
        jsContent,
        /actions\.append\(button\(['"]Editar['"]/
    );
    assert.match(
        jsContent,
        /actions\.append\(button\(type\.estado === ['"]Activo['"] \? ['"]Desactivar['"] : ['"]Reactivar['"]/
    );
});

test('Ejecución DOM sintético: la tabla del modal solo genera Editar y Desactivar/Reactivar, sin Eliminar', async () => {
    class Element {
        constructor() {
            this.listeners = {};
            this.children = [];
            this.dataset = {};
            this.classList = { add() {}, remove() {}, contains() { return false; } };
            this.textContent = '';
        }
        addEventListener(event, fn) { this.listeners[event] = fn; }
        click() { return this.listeners.click?.({ currentTarget: this }); }
        append(...items) { this.children.push(...items); }
        replaceChildren(...items) { this.children = items; }
        setAttribute(k, v) { this[k] = v; }
        focus() {}
    }

    class Option {
        constructor(text, value) {
            this.textContent = text;
            this.value = value;
            this.dataset = {};
        }
    }

    class Select extends Element {
        constructor(options) {
            super();
            this.options = options;
            this._value = options[0]?.value || '';
        }
        get value() { return this._value; }
        set value(v) { this._value = this.options.some(o => o.value === v) ? v : ''; }
        get selectedOptions() { return this.options.filter(o => o.value === this.value); }
        add(option) { this.options.push(option); }
        replaceChildren(...options) { this.options = options; this._value = options[0]?.value || ''; }
    }

    const category = new Select(['Volumen', 'Masa', 'Unidades'].map(x => new Option(x, x)));
    const unit = new Select(['ml', 'l', 'mg', 'g', 'kg', 'unidad'].map(x => new Option(x, x)));
    const form = new Element();
    form.elements = {
        crear: { value: 'true' },
        Id: { value: '' },
        Nombre: { value: '', focus() {} },
        Categoria: category,
        UnidadResumen: unit
    };
    form.reset = () => { category.value = 'Volumen'; unit.value = 'ml'; };

    const reset = new Element(), open = new Element(), close = new Element(), init = new Element();
    const modal = new Element();
    modal.dataset = {
        canEdit: 'true',
        listUrl: '/TiposInsumo/ModalDatos',
        saveUrl: '/TiposInsumo/ModalGuardar',
        stateUrl: '/TiposInsumo/ModalEstado',
        toolUrl: '/TiposInsumo/ModalInicializarHerramienta'
    };
    modal.querySelector = s => ({ '[data-reset-tipo]': reset, '[data-close-tipos]': close, '[data-init-tool]': init })[s];

    const selector = new Select([new Option('Seleccionar tipo', '')]);
    const inventory = new Element();
    inventory.dataset.canCreate = 'true';
    const deleteBtn = new Element();
    inventory.querySelector = s => ({
        '[data-open-tipos]': open,
        'select[name="TipoId"]': selector,
        '[data-delete-tipo]': deleteBtn
    })[s] || null;

    const table = new Element(), message = new Element();
    const dialogMsg = new Element(), dialogErr = new Element(), cancelDel = new Element(), confirmDel = new Element();
    const dialog = new Element();
    dialog.querySelector = s => ({
        '[data-cancel-delete-tipo]': cancelDel,
        '[data-confirm-delete-tipo]': confirmDel
    })[s] || null;

    const mutations = [];
    const document = {
        getElementById: id => ({
            'insumo-form': inventory,
            tiposInsumoModal: modal,
            tipoInsumoForm: form,
            tiposInsumoMensaje: message,
            tiposInsumoTabla: table,
            confirmarEliminarTipoInsumo: dialog,
            confirmarEliminarTipoMensaje: dialogMsg,
            confirmarEliminarTipoError: dialogErr
        })[id] || null,
        querySelector: () => null,
        querySelectorAll: () => [],
        addEventListener() {},
        createElement: tag => new Element()
    };

    const records = [
        { id: 't1', nombre: 'Shampoo Activo', categoria: 'Volumen', unidadResumen: 'l', estado: 'Activo' },
        { id: 't2', nombre: 'Cera Inactiva', categoria: 'Volumen', unidadResumen: 'l', estado: 'Inactivo' },
        { id: 't3', nombre: 'Herramienta', categoria: 'Unidades', unidadResumen: 'unidad', estado: 'Activo', esProtegido: true }
    ];

    class FormData {
        constructor() { this.entries = {}; }
        set(k, v) { this.entries[k] = v; }
    }

    form.querySelector = s => ({ '[name="__RequestVerificationToken"]': { value: 'test-token' } })[s] || null;

    const window = {
        FormData
    };

    const context = {
        document,
        window,
        Option,
        URL,
        FormData,
        fetch: async (url, opts) => {
            if (opts && opts.method === 'POST') {
                mutations.push({ url, body: opts.body });
                return { ok: true, json: async () => ({ esExitoso: true, mensaje: 'OK' }) };
            }
            return { ok: true, json: async () => records };
        }
    };

    vm.runInNewContext(read('wwwroot/js/tipos-insumo.js'), context);
    vm.runInNewContext(read('wwwroot/js/insumos.js'), context);

    // Abrir modal y disparar refresh
    open.click();
    await new Promise(resolve => setImmediate(resolve));

    // Verificar filas renderizadas
    assert.equal(table.children.length, 3, 'Se deben renderizar las 3 filas');

    // Fila 1: Insumo Activo
    const row1Actions = table.children[0].children[4].children;
    const row1Labels = row1Actions.map(b => b.title || b.getAttribute?.('title'));
    assert.deepEqual(row1Labels, ['Editar', 'Desactivar'], 'Fila activa solo debe tener Editar y Desactivar');
    assert.ok(!row1Labels.includes('Eliminar'), 'No debe existir el botón Eliminar en fila activa');

    // Fila 2: Insumo Inactivo
    const row2Actions = table.children[1].children[4].children;
    const row2Labels = row2Actions.map(b => b.title || b.getAttribute?.('title'));
    assert.deepEqual(row2Labels, ['Editar', 'Reactivar'], 'Fila inactiva solo debe tener Editar y Reactivar');
    assert.ok(!row2Labels.includes('Eliminar'), 'No debe existir el botón Eliminar en fila inactiva');

    // Fila 3: Tipo Protegido (Herramienta)
    const row3Cell = table.children[2].children[4];
    assert.equal(row3Cell.textContent, 'Protegido', 'Tipo protegido debe indicar Protegido');
    assert.equal(row3Cell.children.length, 0, 'Tipo protegido no debe tener botones de acción');

    // Probar acción Desactivar desde la tabla del modal
    row1Actions[1].click();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(mutations.length, 1);
    assert.equal(mutations[0].url, '/TiposInsumo/ModalEstado');

    // Probar acción de eliminación rápida desde el selector del formulario
    selector.value = 't1';
    deleteBtn.click();
    assert.equal(dialogMsg.textContent, '¿Confirma desactivar el tipo de insumo Shampoo Activo?');

    // Confirmar la acción rápida: debe llamar a ModalEstado con estado Inactivo (borrado lógico)
    confirmDel.click();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(mutations.length, 2);
    assert.equal(mutations[1].url, '/TiposInsumo/ModalEstado');
    assert.equal(mutations[1].body.entries.id, 't1');
    assert.equal(mutations[1].body.entries.estado, 'Inactivo');
});

test('Eliminación física erradicada: endpoints de borrado físico eliminados del controlador y modal', () => {
    const controller = read('Controllers/TiposInsumoController.cs');
    assert.doesNotMatch(controller, /ModalEliminar/, 'ModalEliminar debe eliminarse de TiposInsumoController');
    assert.doesNotMatch(controller, /public async Task<IActionResult> Eliminar\(string id\)/, 'Eliminar físico debe eliminarse de TiposInsumoController');

    // Index() y vista independiente eliminados
    assert.doesNotMatch(controller, /IActionResult\s+Index\(/, 'Index() debe eliminarse de TiposInsumoController');
    assert.ok(!fs.existsSync(path.join(__dirname, '..', 'Views', 'TiposInsumo')), 'La carpeta Views/TiposInsumo debe haber sido eliminada');

    // Views/Insumos/_TiposInsumoModal.cshtml ya no expone data-delete-url
    const modalView = read('Views/Insumos/_TiposInsumoModal.cshtml');
    assert.doesNotMatch(modalView, /data-delete-url=/, 'Modal no debe exponer data-delete-url');

    // Diálogo de confirmación actualizado a Desactivar
    assert.match(modalView, /Desactivar tipo de insumo/);
    assert.match(modalView, />Desactivar<\/button>/);
});
