const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const view = read('Views/Insumos/Index.cshtml');

test('inventory uses a top accordion without native disclosures or inline row forms', () => {
    assert.match(view, /data-accordion-target="#accordion-flush-body-1"/);
    assert.match(view, /Registrando un Insumo/);
    assert.doesNotMatch(view, /<(?:details|summary)\b/i);
    assert.doesNotMatch(view, /crearInsumoModal/);
    assert.match(view, /data-edit-insumo/);
    assert.match(view, /data-ajustar-insumo/);
    assert.match(view, /insumoConfirmModal/);
});
test('inventory creation provides inline SVG icons', () => {
    assert.match(view, /<svg\b/);
});
test('type administration is opened from create, not stock navigation', () => {
    assert.match(read('Views/Insumos/_InsumoForm.cshtml'), /data-modal-target="tiposInsumoModal"/);
    assert.doesNotMatch(view, /asp-controller="TiposInsumo" asp-action="Index"/);
    assert.match(read('Views/Insumos/_TiposInsumoModal.cshtml'), /<table\b/);
});
test('type modal bounds its content and scrolls long catalogues within the viewport', () => {
    const modal = read('Views/Insumos/_TiposInsumoModal.cshtml');
    assert.match(modal, /max-height:\s*calc\(100vh\s*-\s*2rem\)/);
    assert.match(modal, /overflow-y:\s*auto/);
    assert.match(modal, /<form id="tipoInsumoForm"[\s\S]*?<tbody id="tiposInsumoTabla"/);
});
test('unit labels retain original submitted values', () => {
    const window = {};
    const options = [];
    function Option(text, value) { this.text = text; this.value = value; }
    vm.runInNewContext(read('wwwroot/js/tipos-insumo.js'), { window, Option });
    const select = { replaceChildren: (...args) => { options.length = 0; options.push(...args); }, add: o => options.push(o) };
    for (const [category, expected] of Object.entries({ Volumen: [['ml', 'Mililitro (mL)'], ['l', 'Litro (L)']], Masa: [['mg', 'Miligramo (mg)'], ['g', 'Gramo (g)'], ['kg', 'Kilogramo (kg)']], Unidades: [['unidad', 'Unidad (u)']] })) {
        window.actualizarUnidadesInsumo({ form: { querySelector: () => select }, selectedOptions: [{ dataset: { categoria: category } }] });
        assert.deepEqual(options.slice(1).map(o => [o.value, o.text]), expected);
    }
    assert.equal(window.etiquetaUnidadInsumo('l'), 'Litro (L)');
    assert.equal(window.etiquetaUnidadInsumo('desconocida'), 'desconocida');
});
test('edit loads the captured inventory record into the accordion with duplicate hidden unit fields', () => {
    const record = { Id: 'type-00', Nombre: 'Producto 00', TipoId: 'type-00', Tipo: 'Tipo 00', UnidadMedida: 'l', StockActual: 2, StockMinimo: 0, CantidadReabastecimiento: 0, ReabastecimientoAutomatico: false, ProveedoresIds: [], Estado: 'Activo' };
    const fields = Object.fromEntries(['crear', 'Id', 'Nombre', 'StockMinimo', 'CantidadReabastecimiento', 'ReabastecimientoAutomatico', 'Tipo'].map(name => [name, { value: '', focus() {} }]));
    const hiddenUnit = { disabled: true, replaceChildren() {} }, hiddenType = { disabled: true };
    const unit = { disabled: false, options: [], replaceChildren(...opts) { this.options = opts; }, add(option) { this.options.push(option); } };
    const type = { value: '', disabled: false, selectedOptions: [{ dataset: { categoria: 'Volumen' } }] };
    const title = { textContent: 'Registrando un Insumo' }, submit = { disabled: false }, providers = { querySelectorAll: () => [] };
    const selectors = { 'select[name="TipoId"]': type, 'select[name="UnidadMedida"]': unit, 'input[name="TipoId"]': hiddenType, 'input[name="UnidadMedida"]': hiddenUnit, '[name="UnidadMedida"]': hiddenUnit, '[type="submit"]': submit, '#insumo-proveedores': providers };
    const form = { elements: fields, dataset: { canCreate: 'true' }, reset() {}, querySelector: s => selectors[s] || null, querySelectorAll: () => [], scrollIntoView() {} };
    type.form = form;
    const row = { dataset: { record: JSON.stringify(record) }, addEventListener(event, callback) { this.click = callback; } };
    let expanded = false;
    const document = { getElementById: id => ({ 'insumo-form': form, 'form-title': title, 'accordion-flush-body-1': { classList: { contains: () => !expanded } } })[id] || null, querySelector: () => ({ click() { expanded = true; } }), querySelectorAll: s => s === '[data-edit-insumo]' ? [row] : [] };
    const window = { etiquetaUnidadInsumo: x => x };
    function Option(text, value) { this.text = text; this.value = value; }
    vm.runInNewContext(read('wwwroot/js/tipos-insumo.js'), { window, Option });
    vm.runInNewContext(read('wwwroot/js/insumos.js'), { window, document, URL, Option });
    row.click();
    assert.equal(title.textContent, 'Editando un Insumo');
    assert.equal(fields.Id.value, record.Id);
    assert.equal(fields.Nombre.value, record.Nombre);
    assert.equal(hiddenUnit.value, 'l');
    assert.equal(hiddenType.value, 'type-00');
    assert.equal(fields.crear.value, 'false');
    assert.equal(expanded, true);
});
test('type filters use checkboxes', () => {
    assert.match(view, /type="checkbox"\s+name="tipos"/);
    assert.doesNotMatch(view, /<select name="tipo"/);
});
test('compact filter toolbar keeps checkbox filters in a disclosure', () => {
    assert.match(view, /data-dropdown-toggle="insumosFiltros"/);
    assert.match(view, /id="insumosFiltros"/);
    assert.match(view, /name="search"/);
    assert.match(view, /name="soloBajoMinimo"/);
    assert.doesNotMatch(view, /class="grid md:grid-cols-4 gap-3 mb-4"/);
});
test('navigation and actions have icons and table has contained minimum width', () => {
    for (const action of ['Kardex', 'Proveedores', 'Ordenes', 'Cotizaciones'])
        assert.match(view, new RegExp(`<a asp-action="${action}"[^>]*>\\s*<svg`));
    assert.match(view, /overflow-x-auto[\s\S]*?<table class="[^"]*min-w-\[800px\]/);
    assert.match(view, /data-edit-insumo/);
    assert.match(view, /data-estado-insumo/);
});
test('toolbar navigation uses purple buttons beside search', () => {
    assert.match(view, /data-insumos-toolbar[\s\S]*?insumos-search[\s\S]*?asp-action="Kardex"[^>]*bg-purple-600/);
});
test('group sorting is explicitly scoped to products in each group', () => {
    assert.match(view, /data-sort-insumos="nombre"/);
    assert.match(view, /Ordenar productos dentro de este grupo/);
    assert.match(read('wwwroot/js/insumos.js'), /localeCompare/);
});
test('pagination provides truthful previous and next links', () => {
    assert.match(view, /aria-label="Página anterior"/);
    assert.match(view, /aria-label="Página siguiente"/);
});
test('selected type has a confirmation dialog linked to existing delete endpoint', () => {
    assert.match(read('Views/Insumos/_TiposInsumoModal.cshtml'), /id="confirmarEliminarTipoInsumo"/);
    assert.match(read('wwwroot/js/insumos.js'), /mutate\(typesModal\.dataset\.deleteUrl, \{ id:/);
});
test('group pagination displays numbers and preserves query parameters', () => {
    assert.match(view, /aria-current="page"/);
    assert.match(view, /@numero/);
    assert.match(view, /Context.Request.Query.Where/);
});
test('modal input contract requires editable fields but not a creation id', () => {
    const types = read('Controllers/TiposInsumoController.cs');
    assert.match(types, /ModalGuardar\(TipoInsumoModalInput input, bool crear\)/);
    assert.match(types, /public string\? Id \{ get; set; \}/);
    assert.match(types, /\[Required\(ErrorMessage = "El nombre del tipo de insumo es obligatorio"\), StringLength\(120\)\]/);
    assert.match(types, /\[Required\(ErrorMessage = "La categoría de magnitud es obligatoria"\)\]/);
    assert.match(types, /\[Required\(ErrorMessage = "La unidad resumen es obligatoria"\)\]/);
    assert.match(types, /!crear && string.IsNullOrWhiteSpace\(input.Id\)/);
    assert.match(types, /Id = crear \? string.Empty : input.Id!/);
    assert.match(types, /!ModelState.IsValid/);
});
test('legacy projection and service security remain in place', () => {
    const controller = read('Controllers/InsumosController.cs');
    assert.match(controller, /ProyectarInventario\(List<Insumo>/);
    assert.match(controller, /InventarioAgrupadoViewModel.Crear\(todos, listaTipos, reservas, pagina\)/);
    const types = read('Controllers/TiposInsumoController.cs');
    assert.match(types, /\[HttpPost, ValidateAntiForgeryToken\][\s\S]*?ModalGuardar/);
    assert.match(types, /GuardarConAuditoria\(tipo, crear, Actor\(\)\)/);
    assert.match(types, /User.TienePermiso\(crear \? "Insumos.Crear" : "Insumos.Modificar"\)/);
});
