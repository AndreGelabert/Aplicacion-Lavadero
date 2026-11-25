const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function pickerFixture() {
    const listeners = new Map();
    function element() {
        const events = {}; const classes = new Set();
        return { dataset: {}, children: [], value: '', disabled: false, hidden: false, events,
            classList: { add: x => classes.add(x), remove: x => classes.delete(x), contains: x => classes.has(x), toggle(x, force) { if (force) classes.add(x); else classes.delete(x); } },
            addEventListener: (event, fn) => { events[event] = fn; },
            fire(event, extra = {}) { return events[event]?.({ target: this, currentTarget: this, preventDefault() {}, ...extra }); },
            append(...items) { items.forEach(item => { item.parent = this; this.children.push(item); }); },
            remove() { this.parent.children = this.parent.children.filter(item => item !== this); },
            replaceChildren(...items) { this.children = []; this.append(...items); },
            setAttribute(name, value) { this[name] = value; }, focus() { this.focused = true; },
            contains(target) { return target === this || this.children.some(child => child.contains?.(target)); },
            querySelectorAll() { return []; } };
    }
    const box = element(), search = element(), dropdown = element(), selected = element(), inputs = element(), add = element(), reset = element(), submit = element(), resetLabel = element();
    dropdown.classList.add('hidden');
    const option = element(); option.classList.add('block'); option.dataset = { id: 'p1', name: 'Proveedor Uno' }; dropdown.querySelectorAll = () => [option]; box.append(search, dropdown, add);
    const type = element(), unit = element(), hiddenType = element(), hiddenUnit = element();
    const fields = Object.fromEntries(['crear', 'Id', 'Nombre', 'StockMinimo', 'CantidadReabastecimiento', 'ReabastecimientoAutomatico', 'Tipo'].map(name => [name, element()]));
    const selectors = { '#insumo-proveedores': box, '#insumo-proveedor-search': search, '#insumo-proveedor-dropdown': dropdown, '#insumo-proveedor-selected': selected, '#hiddenProveedoresIds': inputs, '[data-add-provider]': add, '[data-reset-insumo]': reset, '[data-reset-label]': resetLabel, '[type="submit"]': submit, 'select[name="TipoId"]': type, 'select[name="UnidadMedida"]': unit, 'input[name="TipoId"]': hiddenType, 'input[name="UnidadMedida"]': hiddenUnit };
    const form = element(); form.dataset.canCreate = 'true'; form.elements = fields; form.querySelector = s => selectors[s] || null; form.reset = () => {}; form.scrollIntoView = () => {};
    const row = element(); row.dataset.record = JSON.stringify({ Id: 'x', Nombre: 'Insumo', TipoId: 't', UnidadMedida: 'l', ProveedoresIds: ['p1', 'ausente', 'p1'] });
    const title = element(), accordion = element(); accordion.classList.remove('hidden');
    const document = { getElementById: id => ({ 'insumo-form': form, 'form-title': title, 'accordion-flush-body-1': accordion })[id] || null,
        querySelector: () => null, querySelectorAll: s => s === '[data-edit-insumo]' ? [row] : [], createElement: element,
        addEventListener: (event, fn) => { const handlers = listeners.get(event) || []; handlers.push(fn); listeners.set(event, handlers); } };
    const window = { actualizarUnidadesInsumo() {}, etiquetaUnidadInsumo: x => x };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/insumos.js'), 'utf8'), { document, window, URL });
    return { search, dropdown, selected, inputs, option, add, reset, row, fields, listeners, box };
}

test('supplier picker public handlers add unique ids, remove, edit unavailable suppliers and reset', () => {
    const f = pickerFixture();
    f.search.fire('focus'); assert.equal(f.dropdown.classList.contains('hidden'), false);
    f.option.fire('click'); f.add.fire('click'); assert.deepEqual(f.inputs.children.map(x => x.value), ['p1']);
    f.option.fire('click'); f.add.fire('click'); assert.deepEqual(f.inputs.children.map(x => x.value), ['p1']);
    f.selected.children[0].children[1].fire('click'); assert.equal(f.inputs.children.length, 0);
    f.row.fire('click'); assert.deepEqual(f.inputs.children.map(x => x.value), ['p1', 'ausente']);
    assert.equal(f.selected.children[1].children[0].textContent, 'Proveedor no disponible (ausente)');
    assert.equal(f.fields.crear.value, 'false'); f.reset.fire('click'); assert.equal(f.inputs.children.length, 0); assert.equal(f.selected.children.length, 0); assert.equal(f.fields.crear.value, 'true');
});

test('supplier options are hidden by compiled display utilities after add or nonmatching search', () => {
    const css = fs.readFileSync(path.join(__dirname, '../wwwroot/css/app.min.css'), 'utf8');
    const form = fs.readFileSync(path.join(__dirname, '../Views/Insumos/_InsumoForm.cshtml'), 'utf8');
    assert.match(form, /data-provider-option[^>]*class="[^"]*\bblock\b/);
    assert.match(css, /\.block\{display:block\}/);
    assert.match(css, /\.hidden\{display:none\}/);
    assert.ok(css.indexOf('.hidden{display:none}') > css.indexOf('.block{display:block}'));
    const f = pickerFixture();
    f.search.fire('focus'); assert.equal(f.option.classList.contains('hidden'), false);
    f.option.fire('click'); f.add.fire('click'); f.search.fire('focus');
    assert.equal(f.option.classList.contains('hidden'), true, 'selected option must be display:none despite block');
    f.selected.children[0].children[1].fire('click'); f.search.value = 'sin coincidencia'; f.search.fire('input');
    assert.equal(f.option.classList.contains('hidden'), true, 'nonmatching option must be display:none');
    f.search.value = ''; f.search.fire('input'); assert.equal(f.option.classList.contains('hidden'), false);
});

test('supplier picker closes outside and on Escape and ignores disabled option and add button', () => {
    const f = pickerFixture(); f.search.fire('focus');
    f.listeners.get('click')[0]({ target: {} }); assert.equal(f.dropdown.classList.contains('hidden'), true);
    f.search.fire('focus'); f.listeners.get('keydown')[0]({ key: 'Escape', preventDefault() {} }); assert.equal(f.dropdown.classList.contains('hidden'), true); assert.equal(f.search.focused, true);
    f.option.disabled = true; f.option.fire('click'); f.add.fire('click'); assert.equal(f.inputs.children.length, 0);
    f.option.disabled = false; f.add.disabled = true; f.add.fire('click'); assert.equal(f.inputs.children.length, 0);
    f.option.fire('click'); assert.equal(f.inputs.children.length, 1);
});

test('clear filters clears search and selected filters through the public click handler', () => {
    const checkboxes = [{ checked: true }, { checked: true }];
    const search = { value: 'jabón' };
    let clear;
    const document = {
        getElementById: id => id === 'insumos-search' ? search : null,
        querySelector: selector => selector === '[data-clear-insumos-filters]' ? { addEventListener: (_, handler) => { clear = handler; } } : null,
        querySelectorAll: selector => selector === '#insumosFiltros input[type="checkbox"]' ? checkboxes : []
    };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/insumos.js'), 'utf8'), { document, window: { etiquetaUnidadInsumo: x => x }, URL });
    clear();
    assert.equal(search.value, '');
    assert.deepEqual(checkboxes.map(input => input.checked), [false, false]);
});

test('stock status badge uses the Lavados badge dimensions', () => {
    const view = fs.readFileSync(path.join(__dirname, '../Views/Insumos/Index.cshtml'), 'utf8');
    const badge = view.match(/<span class="([^"]*)\s+@\(item\.Estado == "Activo"/);
    assert.ok(badge, 'stock status badge exists');
    for (const cls of ['rounded', 'px-2.5', 'py-0.5', 'text-xs', 'font-medium']) assert.ok(badge[1].split(/\s+/).includes(cls), cls);
    assert.ok(!badge[1].split(/\s+/).includes('rounded-full'));
});

test('stock toolbar and supplier picker expose the requested controls', () => {
    const view = fs.readFileSync(path.join(__dirname, '../Views/Insumos/Index.cshtml'), 'utf8');
    const form = fs.readFileSync(path.join(__dirname, '../Views/Insumos/_InsumoForm.cshtml'), 'utf8');
    assert.match(view, /data-clear-insumos-filters/);
    assert.match(view, /Aplicar Filtros/);
    assert.match(view, /aria-label="Ayuda"[^>]*>[\s\S]*?<svg/);
    assert.match(form, /id="insumo-proveedor-search"/);
    assert.match(form, /id="insumo-proveedor-dropdown"/);
    assert.match(form, /id="hiddenProveedoresIds"/);
    assert.match(form, /peer-checked:bg-blue-600/);
});

test('stock and type modal controls follow the standard form styling', () => {
    const view = fs.readFileSync(path.join(__dirname, '../Views/Insumos/Index.cshtml'), 'utf8');
    const modal = fs.readFileSync(path.join(__dirname, '../Views/Insumos/_TiposInsumoModal.cshtml'), 'utf8');
    assert.match(view, /id="insumoAjusteModal"[\s\S]*?bg-gray-50[\s\S]*?app-button--success/);
    assert.match(modal, /name="Nombre"[^>]*bg-gray-50/);
    assert.match(modal, /data-reset-tipo[^>]*|app-button--danger[^>]*data-reset-tipo/);
});
