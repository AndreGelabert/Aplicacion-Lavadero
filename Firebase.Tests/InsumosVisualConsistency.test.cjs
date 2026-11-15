const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const view = read('Views/Insumos/Index.cshtml');

test('inventory creation has a modal instead of details', () => {
    assert.match(view, /data-modal-target="crearInsumoModal"/);
    assert.doesNotMatch(view, /<summary[^>]*>Nuevo insumo<\/summary>/);
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
test('type filters use checkboxes', () => {
    assert.match(view, /type="checkbox"\s+name="tipos"/);
    assert.doesNotMatch(view, /<select name="tipo"/);
});
test('compact filter toolbar keeps checkbox filters in a disclosure', () => {
    assert.match(view, /<details[^>]*data-insumos-filters/);
    assert.match(view, /<summary[^>]*>[\s\S]*?Filtros<\/summary>/);
    assert.match(view, /name="search"/);
    assert.match(view, /name="soloBajoMinimo"/);
    assert.doesNotMatch(view, /class="grid md:grid-cols-4 gap-3 mb-4"/);
});
test('navigation and actions have icons and table has contained minimum width', () => {
    for (const action of ['Kardex', 'Proveedores', 'Ordenes', 'Cotizaciones'])
        assert.match(view, new RegExp(`<a asp-action="${action}"[^>]*>\\s*<svg`));
    assert.match(view, /overflow-x-auto[\s\S]*?<table class="[^"]*min-w-\[800px\]/);
    assert.match(view, /<summary[^>]*>\s*<svg[^>]*>[\s\S]*?<\/svg>\s*Editar<\/summary>/);
    assert.match(view, /<button[^>]*>\s*<svg[^>]*>[\s\S]*?<\/svg>\s*@\(item.Estado/);
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
