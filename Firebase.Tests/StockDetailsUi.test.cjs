const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const read = p => fs.readFileSync(path.join(__dirname, '..', p), 'utf8');

test('adjustment displays the selected item unit on every opening without changing the quantity', () => {
    const hint = { textContent: '' }, quantity = { value: '99' }, id = { value: '' };
    const modal = { classList: { remove() {} }, querySelector(s) { return ({ form: { reset() { quantity.value = ''; } }, '[name="insumoId"]': id, '[data-ajuste-unidad]': hint, 'input:not([type="hidden"]), button': { focus() {} } })[s]; } };
    const buttons = ['l', 'kg'].map((unidad, i) => ({ dataset: { id: String(i), unidadAjuste: unidad }, addEventListener(_, fn) { this.click = fn; } }));
    const document = { getElementById: id => id === 'insumoAjusteModal' ? modal : null, querySelector: () => null, querySelectorAll: s => s === '[data-ajustar-insumo]' ? buttons : [], addEventListener() {} };
    vm.runInNewContext(read('wwwroot/js/insumos.js'), { document, window: { etiquetaUnidadInsumo: u => ({ l: 'Litro (L)', kg: 'Kilogramo (kg)' })[u] }, URL });
    buttons[0].click(); assert.equal(id.value, '0'); assert.match(hint.textContent, /Litro \(L\)/); assert.equal(quantity.value, '');
    quantity.value = '7'; buttons[1].click(); assert.equal(id.value, '1'); assert.match(hint.textContent, /Kilogramo \(kg\)/); assert.equal(quantity.value, '');
    const view = read('Views/Insumos/Index.cshtml');
    assert.match(view, /data-ajustar-insumo[^>]*data-unidad-ajuste="@item\.UnidadMedida"/);
    assert.match(view, /name="cantidad"[^>]*aria-describedby="ajuste-unidad"/);
});

test('adjustment and filter handlers preserve their previous observable state', () => {
    const current = read('wwwroot/js/insumos.js');
    const addition = ' modal.querySelector(\'[data-ajuste-unidad]\').textContent = `Ingrese una cantidad mayor que cero en ${window.etiquetaUnidadInsumo(button.dataset.unidadAjuste)}.`;';
    assert.equal(current.split(addition).length, 2);
    const baseline = current.replace(addition, '');
    function exercise(source) {
        const id = { value: '' }, quantity = { value: 'old', focus() {} }, hint = {}, search = { value: 'jabón' };
        const checks = [{ checked: true, name: 'tipos', value: 'Quimico' }, { checked: true, name: 'soloBajoMinimo', value: 'true' }];
        const form = { reset() { quantity.value = ''; } };
        const modal = { classList: { remove() {} }, querySelector(s) { return ({ form, '[name="insumoId"]': id, '[data-ajuste-unidad]': hint, 'input:not([type="hidden"]), button': quantity })[s]; } };
        const button = { dataset: { id: 'item-7', unidadAjuste: 'l' }, addEventListener(_, fn) { this.click = fn; } };
        const clear = { addEventListener(_, fn) { this.click = fn; } };
        const document = { getElementById: key => ({ insumoAjusteModal: modal, 'insumos-search': search })[key] || null, querySelector: s => s === '[data-clear-insumos-filters]' ? clear : null, querySelectorAll: s => s === '[data-ajustar-insumo]' ? [button] : s === '#insumosFiltros input[type="checkbox"]' ? checks : [], addEventListener() {} };
        vm.runInNewContext(source, { document, window: { etiquetaUnidadInsumo: x => x }, URL });
        button.click(); const opened = [id.value, quantity.value]; quantity.value = '2.5';
        const submitted = new URLSearchParams({ insumoId: id.value, cantidad: quantity.value }).toString();
        const getBefore = new URLSearchParams([['search', search.value], ...checks.filter(x => x.checked).map(x => [x.name, x.value])]).toString();
        clear.click(); const getAfter = new URLSearchParams([['search', search.value], ...checks.filter(x => x.checked).map(x => [x.name, x.value])]).toString();
        button.click(); return { opened, submitted, getBefore, getAfter, reopened: [id.value, quantity.value] };
    }
    const expected = { opened: ['item-7', ''], submitted: 'insumoId=item-7&cantidad=2.5', getBefore: 'search=jab%C3%B3n&tipos=Quimico&soloBajoMinimo=true', getAfter: 'search=', reopened: ['item-7', ''] };
    const actual = exercise(current);
    assert.deepEqual(actual, expected);
    assert.deepEqual(actual, exercise(baseline));
});

test('replenishment describes the actual ordered batch, not a stock target', () => {
    const form = read('Views/Insumos/_InsumoForm.cshtml');
    assert.match(form, /name="CantidadReabastecimiento"[^>]*aria-describedby="reposicion-ayuda"/);
    assert.match(form, /id="reposicion-ayuda"[^>]*>[^<]*cantidad[^<]*solicita[^<]*reabastecimiento automático/i);
    assert.match(form, /no es un objetivo de stock/i);
});

test('stock help popovers prefer bottom placement in every delegated view', () => {
    for (const file of ['Index', 'Kardex', 'Proveedores', 'Cotizaciones']) {
        const view = read(`Views/Insumos/${file}.cshtml`);
        assert.match(view, /data-popover-target="[^"]+"[^>]*data-popover-placement="bottom"/, file);
    }
});

test('stock filters reuse Servicios controls and Kardex remains compact and GET-based', () => {
    const service = read('Views/Servicio/Index.cshtml');
    const checkbox = 'w-4 h-4 text-blue-600 bg-gray-100 border-gray-300 rounded focus:ring-blue-500';
    assert.ok(service.includes(checkbox));
    for (const file of ['Index', 'Kardex']) {
        const view = read(`Views/Insumos/${file}.cshtml`);
        assert.ok(view.includes(checkbox), file);
        assert.match(view, /data-dropdown-toggle="(?:insumosFiltros|kardex-filters)"[^>]*focus:ring-4[^>]*>[\s\S]*?<svg/);
    }
    const kardex = read('Views/Insumos/Kardex.cshtml');
    assert.match(kardex, /id="kardex-filters"[^>]*max-w-/);
    assert.match(kardex, /grid grid-cols-1 md:grid-cols-3 gap-4/);
    assert.match(kardex, /name="desde"[^>]*class="[^"]*text-sm[^"]*rounded-lg/);
    assert.match(kardex, /form method="get" asp-action="Kardex"/);
    assert.match(kardex, /href="@PageUrl\(\(string\)ViewBag.NextCursor\)"/);
    assert.match(kardex, /"cursor=" \+ Uri\.EscapeDataString\(cursor\)/);
    assert.match(kardex, /AddRange\(selectedInsumos\.Select\(x => "insumoId=" \+ Uri\.EscapeDataString\(x\)\)\)/);
    assert.match(kardex, /AddRange\(selectedTipos\.Select\(x => "tipo=" \+ Uri\.EscapeDataString\(x\)\)\)/);
    assert.match(kardex, /id="kardex-search"/);
});
