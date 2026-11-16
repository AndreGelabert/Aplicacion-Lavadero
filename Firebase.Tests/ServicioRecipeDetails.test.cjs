'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('wwwroot/js/servicio.js', 'utf8');
const view = fs.readFileSync('Views/Servicio/_ServicioForm.cshtml', 'utf8');

function fixture(stages = []) {
    const nodes = {};
    const classes = () => { const set = new Set(); return { add: x => set.add(x), remove: x => set.delete(x), toggle: (x, force) => force ? set.add(x) : set.delete(x), contains: x => set.has(x) }; };
    const node = (value = '') => ({ value, classList: classes(), setAttribute() {}, hasAttribute() { return true; }, focus() {}, addEventListener() {}, innerHTML: '', textContent: '' });
    for (const id of ['servicio-receta-container', 'etapasModal', 'recetaEditorModal', 'etapas-list', 'etapas-count', 'nuevaEtapaNombre', 'recetaModalidad', 'recetaInsumo', 'recetaTipo', 'recetaUnidad', 'recetaDosis', 'recetaTipoGrupo', 'recetaInsumoGrupo', 'receta-editor-list', 'servicio-receta-list']) nodes[id] = node();
    nodes['etapas-json'] = node(JSON.stringify(stages)); nodes.RecetaJson = node('[]');
    nodes['unidades-receta-json'] = node(JSON.stringify({ Unidades: ['unidad'] }));
    nodes.recetaModalidad.value = 'Producto';
    nodes.recetaUnidad.replaceChildren = function () { this.value = ''; }; nodes.recetaUnidad.add = () => {};
    const product = { dataset: { nombre: 'Hidro', categoria: 'Unidades' } };
    const custom = { value: 'custom', dataset: { nombre: 'Herramientas artesanales', categoria: 'Unidades' } };
    nodes.recetaInsumo.options = [product]; nodes.recetaInsumo.selectedIndex = 0; nodes.recetaInsumo.selectedOptions = [product];
    nodes.recetaTipo.options = [custom]; nodes.recetaTipo.selectedIndex = 0; nodes.recetaTipo.selectedOptions = [custom];
    const messages = [];
    const events = {};
    nodes['servicio-form'] = { hasAttribute: () => false, setAttribute() {}, addEventListener: (name, handler) => { events[name] = handler; }, querySelector: () => null };
    const document = { getElementById: id => nodes[id], addEventListener() {}, querySelectorAll: () => [], querySelector: () => null, body: { classList: classes() }, createElement: () => ({ set textContent(x) { this.innerHTML = String(x); } }) };
    function Option(text, value) { this.text = text; this.value = value; }
    const timers = [];
    const window = { document, Option, setTimeout: f => timers.push(f), Notificaciones: { mostrar: (...x) => messages.push(x) } }; window.window = window;
    vm.runInNewContext(source, window);
    return { window, nodes, messages, events, flush: () => { while (timers.length) timers.shift()(); } };
}

test('stages hide general recipe on save and restore it on removal and cancel', () => {
    const { window: w, nodes: n } = fixture();
    w.openEtapasModal(); n.nuevaEtapaNombre.value = 'Lavado'; w.agregarEtapa(); w.saveEtapasModal();
    assert.equal(n['servicio-receta-container'].classList.contains('hidden'), true);
    const id = JSON.parse(n['etapas-json'].value)[0].Id;
    w.openEtapasModal(); w.eliminarEtapa(id); w.cancelEtapasModal();
    assert.equal(n['servicio-receta-container'].classList.contains('hidden'), true);
    w.openEtapasModal(); w.eliminarEtapa(id); w.saveEtapasModal();
    assert.equal(n['servicio-receta-container'].classList.contains('hidden'), false);
});

test('existing general recipe is not discarded when adding stages', () => {
    const { window: w, nodes: n, messages } = fixture();
    n.RecetaJson.value = '[{"InsumoId":"p"}]';
    w.openEtapasModal(); n.nuevaEtapaNombre.value = 'Lavado'; w.agregarEtapa(); w.saveEtapasModal();
    assert.equal(n.RecetaJson.value, '[{"InsumoId":"p"}]'); assert.equal(n['etapas-json'].value, '[]');
    assert.equal(messages.at(-1)[0], 'Quite la receta del servicio antes de agregar etapas.');
});

test('protected type is excluded and defensive handler rejects an injected option; custom types and products remain usable', () => {
    assert.match(view, /EsProtegido/);
    const { window: w, nodes: n } = fixture(); w.openServicioRecipeEditor();
    n.recetaModalidad.value = 'Tipo';
    n['tipos-herramienta-json'] = { value: '["tool"]' };
    n.recetaTipo.value = 'tool'; n.recetaTipo.options = [{ value: 'tool', dataset: { nombre: 'Herramienta', categoria: 'Unidades' } }]; n.recetaTipo.selectedIndex = 0;
    n.recetaUnidad.value = 'unidad'; n.recetaDosis.value = '1';
    assert.equal(w.addRecipeItem(), false); assert.equal(n.RecetaJson.value, '[]');
    assert.equal(n.recetaDosis.value, '1'); assert.equal(n.recetaTipo.value, 'tool');
    n.recetaTipo.value = 'custom'; n.recetaTipo.options = [{ value: 'custom', dataset: { nombre: 'Herramientas artesanales', categoria: 'Unidades' } }];
    assert.equal(w.addRecipeItem(), true);
    n.recetaModalidad.value = 'Producto'; n.recetaInsumo.value = 'hydro'; n.recetaUnidad.value = 'unidad'; n.recetaDosis.value = '2';
    assert.equal(w.addRecipeItem(), true); w.closeRecipeEditor(true);
    assert.deepEqual(JSON.parse(n.RecetaJson.value).map(x => x.Modalidad), ['Tipo', 'Producto']);
});

test('existing stage edit initializes hidden; cancel leaves serialized drafts unchanged', () => {
    const stages = [{ Id: 'existing', Nombre: 'Secado', Receta: [{ InsumoId: 'p', DosisTeorica: 2, UnidadMedida: 'ml' }] }];
    const { window: w, nodes: n } = fixture(stages);
    w.PageModules.servicios.init();
    assert.equal(n['servicio-receta-container'].classList.contains('hidden'), true);
    const before = n['etapas-json'].value;
    w.openEtapasModal(); n.nuevaEtapaNombre.value = 'Lavado'; w.agregarEtapa(); w.cancelEtapasModal();
    assert.equal(n['etapas-json'].value, before);
    assert.equal(n['servicio-receta-container'].classList.contains('hidden'), true);
});

test('create reset restores stage and recipe state and visibility', () => {
    const { window: w, nodes: n, events, flush } = fixture();
    w.PageModules.servicios.init();
    w.openEtapasModal(); n.nuevaEtapaNombre.value = 'Lavado'; w.agregarEtapa(); w.saveEtapasModal();
    assert.equal(n['servicio-receta-container'].classList.contains('hidden'), true);
    n.RecetaJson.value = '[{"InsumoId":"p"}]';
    events.reset(); flush();
    assert.equal(n['etapas-json'].value, '[]');
    assert.equal(n.RecetaJson.value, '[]');
    assert.equal(n['etapas-count'].textContent, 0);
    assert.equal(n['servicio-receta-container'].classList.contains('hidden'), false);
});

test('protected type option uses Razor conditional attributes rather than tag-helper attribute code', () => {
    const option = view.match(/<option value="@tipo\.Id" data-nombre="@tipo\.Nombre" data-categoria="@tipo\.Categoria"[^>]*>/)?.[0];
    assert.ok(option);
    assert.doesNotMatch(option, /@\(esHerramienta\s*\?/);
    assert.match(option, /disabled="@esHerramienta"/);
    assert.match(option, /hidden="@esHerramienta"/);
});

test('async edit and cancel synchronize Flowbite so ordinary clicks open then close', async () => {
    let active = false, hidden = true, aria = 'false';
    const body = { classList: { contains: () => hidden, add: () => { hidden = true; }, remove: () => { hidden = false; } } };
    const item = '#accordion-flush-body-1';
    const instance = { open(id) { assert.equal(id, item); active = true; hidden = false; aria = 'true'; }, close(id) { assert.equal(id, item); active = false; hidden = true; aria = 'false'; } };
    const button = { click() { active ? instance.close(item) : instance.open(item); }, setAttribute(_, value) { aria = value; }, querySelector: () => null };
    const id = { value: '' }, container = { set innerHTML(value) { id.value = value === 'edit' ? 'existing' : ''; } };
    const document = { getElementById: key => ({ 'Id': id, 'servicio-form-container': container, 'accordion-flush-body-1': body })[key], querySelector: () => null, querySelectorAll: () => [], addEventListener() {} };
    const window = { document, setTimeout() {}, FlowbiteInstances: { getInstance: (type, key) => type === 'Accordion' && key === 'accordion-flush' ? instance : null }, Peticiones: { enviar: url => Promise.resolve({ text: () => Promise.resolve(url.includes('?id=') ? 'edit' : 'create') }) } }; window.window = window;
    vm.runInNewContext(source, window);
    window.loadServicioForm('existing'); await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual([active, aria, hidden], [true, 'true', false]);
    window.loadServicioForm(null); await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual([active, aria, hidden], [false, 'false', true]);
    button.click(); assert.deepEqual([active, aria, hidden], [true, 'true', false]);
    button.click(); assert.deepEqual([active, aria, hidden], [false, 'false', true]);
});

test('recipe and stages use responsive two-column layout inside their shared cell', () => {
    const css = fs.readFileSync('wwwroot/css/app.min.css', 'utf8');
    assert.ok(css.includes('.sm\\:grid-cols-2{grid-template-columns:repeat(2,minmax(0,1fr))}'));
    assert.match(view, /class="min-w-0 grid grid-cols-1 sm:grid-cols-2 gap-4">\s*<div class="min-w-0">\s*<label[^>]*>Etapas del Servicio/);
});

test('recipe controls share the stages grid cell and original description span; footer uses standard ordered icons', () => {
    // A separate recipe grid item displaces Descripción rather than using the marked space beside Etapas.
    assert.match(view, /<label class="block mb-2 text-sm font-medium text-gray-900 dark:text-white">Etapas del Servicio<\/label>[\s\S]*?<input type="hidden" id="tipos-herramienta-json"[^\n]*\n\s*<\/div>\s*\n\s*@\{[\s\S]*?<div id="servicio-receta-container"[\s\S]*?\n\s*}\s*\n\s*<\/div>\s*\n\s*<div class="min-w-0">\s*\n\s*<label for="Descripcion"/);
    const footer = view.slice(view.lastIndexOf('<div class="flex justify-end gap-2">'));
    assert.ok(footer.indexOf('closeRecipeEditor(true)') < footer.indexOf('closeRecipeEditor(false)'));
    assert.match(footer, /M2\.25 12c0-5\.385/); assert.match(footer, /M12 2\.25c-5\.385/);
});
