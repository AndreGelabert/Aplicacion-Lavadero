const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const root = path.resolve(__dirname, '..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');

test('Views/Insumos/_InsumoForm.cshtml tiene estructura de 2 filas con proveedores en columnas 3-4', () => {
    const view = read('Views/Insumos/_InsumoForm.cshtml');

    // Comprueba que los campos nuevos y existentes tienen sus placeholders y ayudas de contexto
    assert.match(view, /name="Nombre"[^>]*placeholder="Ej: Shampoo siliconado concentrado"/);
    assert.match(view, /name="StockMinimo"[^>]*placeholder="Ej: 5"[^>]*required/);
    assert.match(view, /id="minimo-ayuda"/);
    assert.match(view, /name="StockMaximo"[^>]*placeholder="Ej: 25"[^>]*required/);
    assert.match(view, /id="maximo-ayuda"/);
    assert.match(view, /name="CantidadReabastecimiento"[^>]*placeholder="Ej: 20"/);
    assert.match(view, /id="reposicion-ayuda"/);

    // Comprueba que el fieldset de proveedores abarca 2 columnas en lg y contiene el switch de reabastecimiento en su encabezado
    assert.match(view, /<fieldset class="[^"]*sm:col-span-2 lg:col-span-2[^"]*">/);
    assert.match(view, /<fieldset[\s\S]*?<legend[^>]*>Proveedores[\s\S]*?name="ReabastecimientoAutomatico"[\s\S]*?id="insumo-proveedores"/);
});

test('wwwroot/js/insumos.js auto-ajusta StockMaximo si StockMinimo lo supera', () => {
    const window = { etiquetaUnidadInsumo: x => x };
    const elements = {
        StockMinimo: { value: '5', addEventListener(evt, cb) { this.oninput = cb; } },
        StockMaximo: { value: '10', addEventListener(evt, cb) { this.oninput = cb; }, setCustomValidity() {} },
        CantidadReabastecimiento: { value: '5', addEventListener(evt, cb) { this.oninput = cb; } },
        crear: { value: 'true' },
        Id: { value: '' }
    };
    const form = {
        elements,
        dataset: { canCreate: 'true' },
        querySelector: sel => sel === 'select[name="UnidadMedida"]' ? { value: 'l', addEventListener() {} } : null,
        querySelectorAll: () => [],
        addEventListener: (evt, cb) => { form['on' + evt] = cb; },
        reset() {}
    };
    const document = {
        addEventListener() {},
        getElementById: id => id === 'insumo-form' ? form : null,
        querySelector: () => null,
        querySelectorAll: () => []
    };

    function Option(text, value) { this.text = text; this.value = value; }
    vm.runInNewContext(read('wwwroot/js/tipos-insumo.js'), { window, Option });
    vm.runInNewContext(read('wwwroot/js/insumos.js'), { window, document, URL, Option });

    // Subir mínimo a 15 (superando el máximo actual de 10)
    elements.StockMinimo.value = '15';
    elements.StockMinimo.oninput();

    // El stock máximo debe crecer automáticamente para superar al mínimo
    const minVal = parseFloat(elements.StockMinimo.value);
    const maxVal = parseFloat(elements.StockMaximo.value);
    assert.ok(maxVal > minVal, `Stock máximo (${maxVal}) debe ser mayor que el mínimo (${minVal})`);
    assert.equal(elements.CantidadReabastecimiento.value, String(maxVal - minVal));
});

test('wwwroot/js/servicio.js rechaza cantidades decimales en recetas con unidad', () => {
    const window = { Notificaciones: { mostrar() {} } };
    let tableErrorMessage = '';
    const doseInput = { value: '1.5' };
    const selectInsumo = {
        value: 'herramienta-1',
        selectedIndex: 0,
        options: [{ value: 'herramienta-1', dataset: { categoria: 'Unidades', nombre: 'Microfibra' } }]
    };
    const document = {
        addEventListener() {},
        getElementById: id => {
            if (id === 'recetaModalidad') return { value: 'Producto' };
            if (id === 'recetaInsumo') return selectInsumo;
            if (id === 'recetaUnidad') return { value: 'unidad' };
            if (id === 'recetaDosis') return doseInput;
            if (id === 'tipos-herramienta-json') return { value: '[]' };
            return null;
        },
        querySelectorAll: () => []
    };

    const script = read('wwwroot/js/servicio.js');
    const context = { window, document, console, tableErrorMessage: '', result: true };
    vm.runInNewContext(
        `
        let showTableMessage = (type, msg) => { tableErrorMessage = msg; };
        let recetaEditorTarget = { Receta: [] };
        ${script}
        result = window.addRecipeItem();
        `,
        context
    );

    // Debe haber fallado y mostrado mensaje específico
    assert.equal(context.result, false);
});

test('wwwroot/js/insumos.js muestra advertencia si la brecha entre mínimo y máximo es inferior al 15%', () => {
    const window = { etiquetaUnidadInsumo: x => x };
    const elements = {
        StockMinimo: { value: '4', addEventListener(evt, cb) { this.oninput = cb; } },
        StockMaximo: { value: '4.01', addEventListener(evt, cb) { this.oninput = cb; }, setCustomValidity() {} },
        CantidadReabastecimiento: { value: '0.01', addEventListener(evt, cb) { this.oninput = cb; } },
        crear: { value: 'true' },
        Id: { value: '' }
    };
    const form = {
        elements,
        dataset: { canCreate: 'true' },
        querySelector: sel => sel === 'select[name="UnidadMedida"]' ? { value: 'l', addEventListener() {} } : null,
        querySelectorAll: () => [],
        addEventListener: () => {},
        reset() {}
    };
    const warningClasses = new Set(['hidden']);
    const warningEl = {
        classList: {
            add(c) { warningClasses.add(c); },
            remove(c) { warningClasses.delete(c); },
            toggle(c, force) {
                if (force === undefined) {
                    if (warningClasses.has(c)) warningClasses.delete(c); else warningClasses.add(c);
                } else if (force) {
                    warningClasses.add(c);
                } else {
                    warningClasses.delete(c);
                }
            },
            contains(c) { return warningClasses.has(c); }
        }
    };
    const document = {
        addEventListener() {},
        getElementById: id => {
            if (id === 'insumo-form') return form;
            if (id === 'stock-brecha-warning') return warningEl;
            return null;
        },
        querySelector: () => null,
        querySelectorAll: () => []
    };

    function Option(text, value) { this.text = text; this.value = value; }
    vm.runInNewContext(read('wwwroot/js/tipos-insumo.js'), { window, Option });
    vm.runInNewContext(read('wwwroot/js/insumos.js'), { window, document, URL, Option });

    // Con min=4 y max=4.01 (brecha=0.01 < 0.60), debe mostrarse la advertencia (hidden false)
    elements.StockMaximo.oninput();
    assert.equal(warningEl.classList.contains('hidden'), false);

    // Si aumentamos max a 6 (brecha=2 >= 0.60), debe ocultarse (hidden true)
    elements.StockMaximo.value = '6';
    elements.StockMaximo.oninput();
    assert.equal(warningEl.classList.contains('hidden'), true);
});
