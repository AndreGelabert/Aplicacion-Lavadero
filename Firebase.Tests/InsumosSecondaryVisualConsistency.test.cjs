const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const read = path => readFileSync(path, 'utf8');

test('encabezados secundarios usan estructura e icono de ayuda de Servicios', () => {
  const reference = read('Views/Servicio/Index.cshtml');
  const buttonClasses = 'text-white inline-flex items-center bg-blue-700 hover:bg-blue-800 focus:ring-4 focus:outline-none focus:ring-blue-300 font-medium rounded-lg text-sm px-2 py-2 text-center dark:bg-blue-600 dark:hover:bg-blue-700 dark:focus:ring-blue-800';
  const iconPath = 'M9.879 7.519c1.171-1.025';
  assert.ok(reference.includes(buttonClasses)); assert.ok(reference.includes(iconPath));
  for (const view of ['Cotizaciones', 'Proveedores', 'Ordenes', 'Kardex']) {
    const source = read(`Views/Insumos/${view}.cshtml`);
    assert.ok(source.includes(buttonClasses), view); assert.ok(source.includes(iconPath), view);
    assert.match(source, /class="w-5 h-5" fill="none" stroke="currentColor" stroke-width="1.5"/, view);
    assert.match(source, /p-2 flex justify-between items-center rounded-t border-b dark:border-gray-600/, view);
    assert.match(source, /bg-white dark:bg-gray-800 relative shadow-md sm:rounded-lg overflow-visible/, view);
  }
  for (const view of ['Cotizaciones', 'Proveedores']) {
    const source = read(`Views/Insumos/${view}.cshtml`);
    assert.match(source, /data-active-classes="bg-white dark:bg-gray-900/);
    assert.match(source, /<span[^>]*>[^<]+<\/span><svg data-accordion-icon/);
  }
  assert.match(read('Views/Insumos/Kardex.cshtml'), /md:flex-1/);
});

test('las vistas secundarias evitan acciones y edición inline', () => {
  for (const view of ['Cotizaciones', 'Proveedores', 'Ordenes', 'Kardex']) {
    const source = read(`Views/Insumos/${view}.cshtml`);
    assert.doesNotMatch(source, /<(details|summary)\b/i, view);
    assert.match(source, /<table class="w-full/, view);
  }
});
test('proveedores usa formulario superior y confirmación', () => {
  const source = read('Views/Insumos/Proveedores.cshtml');
  assert.match(source, /data-provider-edit/);
  assert.match(source, /provider-confirm/);
  assert.match(source, /asp-action="GuardarProveedor"/);
  assert.match(source, /asp-action="EstadoProveedor"/);
  assert.match(source, /@Html.AntiForgeryToken\(\)/);
});
test('cotizaciones selecciona insumos dinámicamente y conserva contrato', () => {
  const source = read('Views/Insumos/Cotizaciones.cshtml');
  assert.match(source, /Insumos de la Solicitud/);
  assert.match(source, /Insumos Seleccionados/);
  assert.match(source, /quotation-picker/);
  assert.doesNotMatch(source, /items\[@i\]\.Cantidad/);
  assert.match(source, /asp-action="CrearSolicitudes"/);
  const js = read('wwwroot/js/insumos-secondary.js');
  assert.match(js, /items\[\$\{index\}\]\.InsumoId/);
  assert.match(js, /items\[\$\{index\}\]\.Cantidad/);
});
test('órdenes usa modal y conserva acciones protegidas', () => {
  const source = read('Views/Insumos/Ordenes.cshtml');
  assert.match(source, /data-order-modal/);
  for (const action of ['AprobarOrden', 'EditarOrdenPendiente', 'CambiarEstadoOrden', 'RecibirOrden']) assert.match(source, new RegExp(`asp-action="${action}"`));
  assert.match(source, /@Html.AntiForgeryToken\(\)/);
});
test('acciones de cotizaciones son iconos y los diálogos están fuera de la tabla', () => {
  const source = read('Views/Insumos/Cotizaciones.cshtml');
  const firstTable = source.slice(source.indexOf('<table'), source.indexOf('</table>'));
  assert.doesNotMatch(firstTable, /role="dialog"|hover:underline/);
  for (const action of ['Aprobar', 'Enviar email', 'Adjudicar']) assert.match(source, new RegExp(`aria-label="${action}`));
});
test('las listas completas ofrecen paginación real', () => {
  for (const view of ['Proveedores', 'Cotizaciones', 'Ordenes']) assert.match(read(`Views/Insumos/${view}.cshtml`), /data-finite-table/, view);
});
test('paginación de lista completa cambia filas sin POST y respeta límites', () => {
  const vm = require('node:vm');
  const rows = Array.from({ length: 11 }, () => ({ hidden: false }));
  const listeners = new Map();
  function element() { return { children: [], attrs: {}, addEventListener(name, handler) { this[name] = handler; }, setAttribute(name, value) { this.attrs[name] = value; }, getAttribute(name) { return this.attrs[name]; }, removeAttribute(name) { delete this.attrs[name]; }, append(...nodes) { this.children.push(...nodes); } }; }
  let nav;
  const table = { tBodies: [{ rows }], querySelectorAll() { return []; }, parentElement: { after(node) { nav = node; } } };
  const document = { addEventListener(name, fn) { listeners.set(name, fn); }, getElementById() { return null; }, querySelectorAll(selector) { return selector === '[data-finite-table]' ? [table] : []; }, createElement: element };
  vm.runInNewContext(read('wwwroot/js/insumos-secondary.js'), { document });
  const [label, controls] = nav.children;
  const previous = controls.children.find(x => x.getAttribute('aria-label') === 'Anterior');
  const next = controls.children.find(x => x.getAttribute('aria-label') === 'Siguiente');
  const active = () => controls.children.find(x => x.getAttribute('aria-current') === 'page').textContent;
  assert.equal(label.textContent, 'Mostrando página 1 de 2'); assert.equal(active(), 1); assert.equal(rows.filter(r => !r.hidden).length, 10); assert.equal(previous.disabled, true);
  next.click(); assert.equal(active(), 2); assert.equal(rows.filter(r => !r.hidden).length, 1); assert.equal(next.disabled, true);
  next.click(); assert.equal(active(), 2); previous.click(); assert.equal(active(), 1);
});
test('listas finitas ofrecen ordenamiento funcional accesible', () => {
  for (const view of ['Proveedores', 'Cotizaciones', 'Ordenes']) assert.match(read(`Views/Insumos/${view}.cshtml`), /data-sort-column/, view);
  assert.match(read('wwwroot/js/insumos-secondary.js'), /aria-sort/);
});
test('ordenamiento numérico y por fecha usa valores canónicos', () => {
  const vm = require('node:vm');
  const makeElement = () => ({ children: [], addEventListener(n, f) { this[n] = f; }, setAttribute() {}, append(...n) { this.children.push(...n); } });
  const header = { attrs: {}, getAttribute(n) { return this.attrs[n]; }, setAttribute(n,v) { this.attrs[n]=v; }, removeAttribute(n) { delete this.attrs[n]; } };
  const button = { dataset: { sortColumn: '0', sortType: 'number' }, closest() { return header; }, addEventListener(n, f) { this[n] = f; } };
  const rows = ['10.5', '2.25', '100'].map(value => ({ cells: [{ dataset: { sortValue: value }, textContent: value }] }));
  const body = { rows, appendChild(row) { const i = this.rows.indexOf(row); this.rows.splice(i, 1); this.rows.push(row); } };
  const table = { tBodies: [body], parentElement: { after() {} }, querySelectorAll(s) { return s === '[data-sort-column]' ? [button] : [header]; } };
  const document = { addEventListener() {}, getElementById() { return null; }, querySelectorAll(s) { return s === '[data-finite-table]' ? [table] : []; }, createElement: makeElement };
  vm.runInNewContext(read('wwwroot/js/insumos-secondary.js'), { document });
  button.click(); assert.deepEqual(body.rows.map(r => r.cells[0].dataset.sortValue), ['2.25','10.5','100']);
  body.rows[0].cells[0].dataset.sortValue = '2025-12-01T00:00:00Z'; body.rows[1].cells[0].dataset.sortValue = '2024-01-01T00:00:00Z'; body.rows[2].cells[0].dataset.sortValue = '2025-01-01T00:00:00Z';
  button.dataset.sortType = 'date'; header.removeAttribute('aria-sort'); button.click(); assert.deepEqual(body.rows.map(r => r.cells[0].dataset.sortValue), ['2024-01-01T00:00:00Z','2025-01-01T00:00:00Z','2025-12-01T00:00:00Z']);
});
test('selector rechaza cantidades inválidas y preserva selección al quitar', () => {
  const vm = require('node:vm');
  const node = () => ({ children: [], dataset: {}, classList: { add() {}, remove() {} }, addEventListener(n,f) { this[n]=f; }, setAttribute() {}, append(...x) { this.children.push(...x); }, replaceChildren(...x) { this.children = x; } });
  const form = node(); let provider = true; form.querySelector = () => provider ? {} : null;
  const picker = node(); picker.closest = () => form;
  const search = node(), dropdown = node(), list = node(), error = node(), add = node();
  const option = node(); option.dataset = { id: 'i1', name: 'Jabón', unit: 'l' }; option.closest = () => option;
  dropdown.querySelectorAll = () => [option];
  const ids = { 'quotation-picker': picker, 'quotation-search': search, 'quotation-dropdown': dropdown, 'quotation-selected': list, 'quotation-error': error, 'quotation-add': add };
  const document = { addEventListener() {}, getElementById(id) { return ids[id] || null; }, querySelector() { return null; }, querySelectorAll() { return []; }, createElement: node, createElementNS: node };
  vm.runInNewContext(read('wwwroot/js/insumos-secondary.js'), { document });
  let prevented = false; const submit = () => { prevented = false; form.submit({ preventDefault() { prevented = true; } }); return prevented; };
  assert.equal(submit(), true); assert.equal(list.children.length, 0);
  dropdown.click({ target: option }); add.click(); assert.equal(list.children.length, 1);
  let row = list.children[0]; assert.equal(row.children[1].name, 'items[0].InsumoId'); assert.equal(row.children[1].value, 'i1'); const quantity = row.children[2].children[0]; assert.equal(quantity.name, 'items[0].Cantidad');
  quantity.value = '0'; quantity.input(); assert.equal(submit(), true); assert.equal(list.children.length, 1);
  quantity.value = '2.5'; quantity.input(); assert.equal(submit(), false);
  row.children[3].click(); assert.equal(list.children.length, 0); assert.equal(submit(), true);
});
test('Kardex identifica búsqueda local de movimientos de la página', () => {
  const source = read('Views/Insumos/Kardex.cshtml');
  assert.match(source, /Buscar movimientos en esta página/);
  assert.match(source, /data-kardex-row/);
  assert.match(read('wwwroot/js/insumos-secondary.js'), /\[data-kardex-row\]/);
});
test('buscar movimientos filtra solo filas cargadas y restaurar búsqueda las muestra', () => {
  const vm = require('node:vm');
  const rows = [{ textContent: 'Shampoo entrada', hidden: false }, { textContent: 'Jabón salida', hidden: false }];
  const input = { value: '', addEventListener(name, fn) { this[name] = fn; } };
  const document = { addEventListener() {}, getElementById(id) { return id === 'kardex-search' ? input : null; }, querySelectorAll(selector) { return selector === '[data-kardex-row]' ? rows : []; } };
  vm.runInNewContext(read('wwwroot/js/insumos-secondary.js'), { document });
  input.value = 'SALIDA'; input.input(); assert.deepEqual(rows.map(row => row.hidden), [true, false]);
  input.value = ''; input.input(); assert.deepEqual(rows.map(row => row.hidden), [false, false]);
});
test('Kardex conserva navegación por cursores y filtros', () => {
  const source = read('Views/Insumos/Kardex.cshtml');
  for (const name of ['insumoId', 'tipo', 'desde', 'hasta', 'sortOrder', 'cursor']) assert.match(source, new RegExp(`"${name}="`));
  assert.match(source, /AddRange\(selectedInsumos\.Select\(x => "insumoId=" \+ Uri\.EscapeDataString\(x\)\)\)/);
  assert.match(source, /AddRange\(selectedTipos\.Select\(x => "tipo=" \+ Uri\.EscapeDataString\(x\)\)\)/);
  assert.match(source, /"cursor=" \+ Uri\.EscapeDataString\(cursor\)/);
  assert.match(source, /href="@PageUrl\(\(string\)ViewBag.PrevCursor\)"/);
  assert.match(source, /href="@PageUrl\(\(string\)ViewBag.NextCursor\)"/);
  assert.doesNotMatch(source, /asp-route-page=|ViewBag.TotalPages/);
  assert.match(source, /Aplicar Filtros/);
  assert.match(source, /Limpiar filtros/);
});
