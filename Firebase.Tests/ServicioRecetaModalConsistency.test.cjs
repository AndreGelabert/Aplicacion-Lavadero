'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const view = fs.readFileSync('Views/Servicio/_ServicioForm.cshtml', 'utf8');
const index = fs.readFileSync('Views/Servicio/Index.cshtml', 'utf8');
const source = fs.readFileSync('wwwroot/js/servicio.js', 'utf8');

test('service fields reflow without wider spans than the active grid', () => {
    assert.match(view, /class="grid gap-4 m-4 grid-cols-1 sm:grid-cols-2 lg:grid-cols-5"/);
    for (const label of ['Nombre', 'Precio', 'TipoVehiculo', 'TiempoEstimado']) {
        assert.match(view, new RegExp(`<div class="min-w-0">\\s*<label for="${label}"`));
    }
    assert.match(view, /class="min-w-0 sm:col-span-2"[^>]*>\s*<label for="Tipo"/);
    assert.match(view, /class="min-w-0 sm:col-span-2 lg:col-span-3"[^>]*>\s*<label for="Descripcion"/);
    assert.match(view, /id="servicio-receta-container" class="min-w-0 sm:col-span-2 lg:col-span-5"/);
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
    nodes['unidades-receta-json'] = { value: JSON.stringify({ Volumen: ['ml', 'l'] }) };
    const options = [];
    nodes.recetaUnidad = { replaceChildren: (...x) => { options.length = 0; options.push(...x); }, add: x => options.push(x) };
    const document = { getElementById: id => nodes[id], addEventListener() {} };
    const window = {};
    function Option(text, value) { this.text = text; this.value = value; }
    vm.runInNewContext(source, { document, window, Option });
    window.actualizarUnidadesReceta();
    assert.deepEqual(options.slice(1).map(x => [x.text, x.value]), [['Mililitro (mL)', 'ml'], ['Litro (L)', 'l']]);
});

test('stage and recipe cancellation have separate public exits', () => {
    assert.match(source, /event\.key === 'Escape'/);
    assert.match(source, /closeRecipeEditor\(false\)/);
    assert.match(index, /onclick="cancelEtapasModal\(\)"/);
    assert.match(source, /originalEtapasSnapshot/);
    assert.match(source, /recetaEditorOriginal/);
});
