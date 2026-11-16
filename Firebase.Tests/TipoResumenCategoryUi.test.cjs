const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const read = p => fs.readFileSync(path.join(__dirname, '..', p), 'utf8');

function setup() {
    class Element {
        constructor() { this.listeners = {}; this.children = []; this.dataset = {}; this.classList = { add() {}, remove() {}, contains() { return false; } }; }
        addEventListener(event, fn) { this.listeners[event] = fn; }
        click() { return this.listeners.click?.({ currentTarget: this }); }
        append(...items) { this.children.push(...items); }
        replaceChildren(...items) { this.children = items; }
        setAttribute() {} focus() {}
    }
    class Option { constructor(text, value) { this.textContent = text; this.value = value; this.dataset = {}; } }
    class Select extends Element {
        constructor(options) { super(); this.options = options; this._value = options[0]?.value || ''; }
        get value() { return this._value; }
        set value(v) { this._value = this.options.some(o => o.value === v) ? v : ''; }
        get selectedOptions() { return this.options.filter(o => o.value === this.value); }
        add(option) { this.options.push(option); }
        replaceChildren(...options) { this.options = options; this._value = options[0]?.value || ''; }
    }
    const category = new Select(['Volumen', 'Masa', 'Unidades'].map(x => new Option(x, x)));
    const unit = new Select(['ml', 'l', 'mg', 'g', 'kg', 'unidad'].map(x => new Option(x, x)));
    const form = new Element();
    form.elements = { crear: { value: 'true' }, Id: { value: '' }, Nombre: { value: '', focus() {} }, Categoria: category, UnidadResumen: unit };
    form.reset = () => { category.value = 'Volumen'; unit.value = 'ml'; };
    const reset = new Element(), open = new Element(), close = new Element(), init = new Element();
    const modal = new Element(); modal.dataset = { canEdit: 'true', listUrl: '/list' };
    modal.querySelector = s => ({ '[data-reset-tipo]': reset, '[data-close-tipos]': close, '[data-init-tool]': init })[s];
    const selector = new Select([new Option('Seleccionar tipo', '')]);
    const inventory = new Element(); inventory.dataset.canCreate = 'true';
    inventory.querySelector = s => ({ '[data-open-tipos]': open, 'select[name="TipoId"]': selector })[s] || null;
    const table = new Element(), message = new Element();
    const dialog = new Element(); dialog.querySelector = () => new Element();
    const document = { getElementById: id => ({ 'insumo-form': inventory, tiposInsumoModal: modal, tipoInsumoForm: form, tiposInsumoMensaje: message, tiposInsumoTabla: table, confirmarEliminarTipoInsumo: dialog })[id] || null, querySelector: () => null, querySelectorAll: () => [], addEventListener() {}, createElement: () => new Element() };
    const records = [{ id: '1', nombre: 'Harina', categoria: 'Masa', unidadResumen: 'kg', estado: 'Activo' }];
    const window = {};
    const context = { document, window, Option, URL, fetch: async () => ({ ok: true, json: async () => records }) };
    vm.runInNewContext(read('wwwroot/js/tipos-insumo.js'), context);
    vm.runInNewContext(read('wwwroot/js/insumos.js'), context);
    return { category, unit, form, reset, open, table, records };
}
const choices = unit => unit.options.filter(o => o.value).map(o => o.value);

test('S1 actual modal handlers filter categories, preserve compatible edit and reset create', async () => {
    const { category, unit, form, reset, open, table, records } = setup();
    assert.deepEqual(choices(unit), ['ml', 'l']);
    unit.value = 'l'; category.value = 'Masa'; category.listeners.change();
    assert.deepEqual(choices(unit), ['mg', 'g', 'kg']); assert.notEqual(unit.value, 'l');
    unit.value = 'kg'; category.value = 'Volumen'; category.listeners.change();
    assert.deepEqual(choices(unit), ['ml', 'l']); assert.notEqual(unit.value, 'kg');
    category.value = 'Unidades'; category.listeners.change(); assert.deepEqual(choices(unit), ['unidad']);
    open.click(); await new Promise(resolve => setImmediate(resolve));
    table.children[0].children[4].children[0].click();
    assert.equal(form.elements.crear.value, 'false'); assert.equal(form.elements.Id.value, '1');
    assert.deepEqual(choices(unit), ['mg', 'g', 'kg']); assert.equal(unit.value, 'kg');
    reset.click(); assert.equal(form.elements.crear.value, 'true'); assert.equal(form.elements.Id.value, '');
    assert.equal(category.value, 'Volumen'); assert.deepEqual(choices(unit), ['ml', 'l']);
    records[0].categoria = 'Volumen'; records[0].unidadResumen = 'kg';
    open.click(); await new Promise(resolve => setImmediate(resolve)); table.children[0].children[4].children[0].click();
    assert.deepEqual(choices(unit), ['ml', 'l']); assert.notEqual(unit.value, 'kg');
});

test('existing inventory unit helper retains its category behavior', () => {
    const window = {}; class Option { constructor(text, value) { this.text = text; this.value = value; } }
    vm.runInNewContext(read('wwwroot/js/tipos-insumo.js'), { window, Option });
    const unit = { options: [], replaceChildren(...x) { this.options = x; }, add(x) { this.options.push(x); } };
    window.actualizarUnidadesInsumo({ selectedOptions: [{ dataset: { categoria: 'Masa' } }], form: { querySelector: () => unit } });
    assert.deepEqual(unit.options.map(x => x.value), ['', 'mg', 'g', 'kg']);
});
