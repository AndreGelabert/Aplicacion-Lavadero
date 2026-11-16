'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const view = fs.readFileSync('Views/Servicio/_ServicioForm.cshtml', 'utf8');
const index = fs.readFileSync('Views/Servicio/Index.cshtml', 'utf8');
const source = fs.readFileSync('wwwroot/js/servicio.js', 'utf8');

test('service fields reflow without wider spans than the active grid', () => {
    assert.match(view, /class="grid gap-4 m-4 grid-cols-1 lg:grid-cols-3"/);
    for (const label of ['Nombre', 'Precio', 'TipoVehiculo', 'TiempoEstimado']) {
        assert.match(view, new RegExp(`<div class="min-w-0">\\s*<label for="${label}"`));
    }
    // S5 restores equal main thirds, with both type fields sharing the last third.
    assert.match(view, /class="grid grid-cols-1 sm:grid-cols-2 gap-4 min-w-0">\s*<div class="min-w-0">\s*<label for="Tipo"/);
    assert.match(view, /class="min-w-0"[^>]*>\s*<label for="Descripcion"/);
    assert.match(view, /id="servicio-receta-container" class="min-w-0 /);
    assert.match(view, /class="flex flex-wrap items-center gap-2"/);
    assert.match(view, /class="flex flex-wrap justify-end m-4 gap-2"/);
    assert.doesNotMatch(view, /overflow-y: hidden/);
});

test('recipe dialog has production modal structure, labeled controls and explicit add', () => {
    assert.match(view, /id="recetaEditorModal" tabindex="-1" aria-hidden="true"/);
    assert.match(view, /aria-labelledby="recetaEditorTitle"/);
    for (const id of ['recetaModalidad', 'recetaInsumo', 'recetaTipo', 'recetaUnidad', 'recetaDosis']) assert.match(view, new RegExp(`<label for="${id}"`));
    assert.match(view, /onclick="addRecipeItem\(\)"/);
    assert.match(view, /onclick="closeRecipeEditor\(false\)"/);
    assert.match(view, /onclick="closeRecipeEditor\(true\)"/);
    assert.doesNotMatch(view, /@insumo\.Nombre \(@insumo\.UnidadMedida\)/);
    assert.match(index, /onclick="cancelEtapasModal\(\)"/);
});

test('unit labels and canonical values through the public selector', () => {
    const nodes = {};
    const selector = (value, categoria) => ({ value, selectedOptions: [{ dataset: { categoria } }] });
    nodes.recetaModalidad = { value: 'Producto' };
    nodes.recetaInsumo = selector('p', 'Volumen');
    nodes['unidades-receta-json'] = { value: JSON.stringify({ Volumen: ['cc', 'cm3', 'l', 'litro', 'litros', 'lt', 'mililitro', 'mililitros', 'ml'], Masa: ['mg', 'miligramo', 'g', 'gr', 'kg', 'kilo'], Unidades: ['u', 'unidad', 'unidades'] }) };
    const options = [];
    nodes.recetaUnidad = { replaceChildren: (...x) => { options.length = 0; options.push(...x); }, add: x => options.push(x) };
    const document = { getElementById: id => nodes[id], addEventListener() {} };
    const window = {};
    function Option(text, value) { this.text = text; this.value = value; }
    vm.runInNewContext(source, { document, window, Option });
    window.actualizarUnidadesReceta();
    assert.deepEqual(options.slice(1).map(x => [x.text, x.value]), [['L (Litros)', 'l'], ['ml (Mililitros)', 'ml'], ['cm³ (Centímetros cúbicos)', 'cm3']]);
    nodes.recetaInsumo.selectedOptions[0].dataset.categoria = 'Masa';
    window.actualizarUnidadesReceta();
    assert.deepEqual(options.slice(1).map(x => x.value), ['mg', 'g', 'kg']);
    nodes.recetaInsumo.selectedOptions[0].dataset.categoria = 'Unidades';
    window.actualizarUnidadesReceta();
    assert.deepEqual(options.slice(1).map(x => x.value), ['unidad']);

});

test('public recipe handlers preserve selected volume codes, quantities and legacy drafts', () => {
    const list = { innerHTML: '' };
    const classes = { add() {}, remove() {}, toggle() {} };
    const productOption = { dataset: { categoria: 'Volumen', nombre: 'Producto' } };
    const product = { value: '', selectedIndex: 0, options: [productOption], selectedOptions: [productOption] };
    const unit = { value: '', replaceChildren() { this.value = ''; }, add() {} };
    const legacy = [{ InsumoId: 'legacy', NombreInsumo: 'Anterior', UnidadMedida: 'cc', DosisTeorica: 2.5 }];
    const nodes = {
        RecetaJson: { value: JSON.stringify(legacy) }, recetaModalidad: { value: 'Producto', focus() {} },
        recetaInsumo: product, recetaTipo: { value: '' }, recetaUnidad: unit,
        recetaDosis: { value: '' }, 'unidades-receta-json': { value: JSON.stringify({ Volumen: ['ml', 'cm3', 'cc', 'l'] }) },
        'receta-editor-list': list, 'servicio-receta-list': { innerHTML: '' },
        recetaTipoGrupo: { classList: classes }, recetaInsumoGrupo: { classList: classes },
        recetaEditorModal: { classList: classes, setAttribute() {} }
    };
    const document = { getElementById: id => nodes[id], addEventListener() {}, querySelectorAll: () => [], body: { classList: classes }, createElement: () => ({ set textContent(value) { this.innerHTML = String(value); } }) };
    function Option(text, value) { this.text = text; this.value = value; }
    const window = { document, Option };
    window.window = window;
    vm.runInNewContext(source, window);
    window.openServicioRecipeEditor();
    assert.match(list.innerHTML, /2.5 ml \(Mililitros\)/);
    for (const [id, code, amount] of [['a', 'cm3', '3.25'], ['b', 'ml', '4.5']]) {
        product.value = id; unit.value = code; nodes.recetaDosis.value = amount;
        assert.equal(window.addRecipeItem(), true);
    }
    assert.match(list.innerHTML, /3.25 cm³ \(Centímetros cúbicos\)/);
    window.closeRecipeEditor(true);
    const saved = JSON.parse(nodes.RecetaJson.value);
    assert.deepEqual(saved.map(x => [x.UnidadMedida, x.DosisTeorica]), [['cc', 2.5], ['cm3', 3.25], ['ml', 4.5]]);
    window.openServicioRecipeEditor();
    assert.match(list.innerHTML, /3.25 cm³ \(Centímetros cúbicos\)/);
});

test('recipe save and cancel buttons have corresponding icons', () => {
    for (const action of ['true', 'false']) {
        assert.match(view, new RegExp(`onclick="closeRecipeEditor\\(${action}\\)"[^>]*>\\s*<svg`));
    }
});

test('stage and recipe cancellation have separate public exits', () => {
    assert.match(source, /event\.key === 'Escape'/);
    assert.match(source, /closeRecipeEditor\(false\)/);
    assert.match(index, /onclick="cancelEtapasModal\(\)"/);
    assert.match(source, /originalEtapasSnapshot/);
    assert.match(source, /recetaEditorOriginal/);
});
