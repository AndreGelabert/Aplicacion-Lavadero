const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const source = fs.readFileSync('wwwroot/js/turno-reprogramacion.js', 'utf8');
const tick = () => new Promise(setImmediate);
function setup(fetch) {
  class E {
    constructor() { this.listeners = {}; this.disabled = false; this.textContent = ''; this.open = false; this.value = ''; this.dataset = {}; this.children = []; }
    replaceChildren() { this.children = []; }
    append(...items) { this.children.push(...items); }
    querySelectorAll(selector) {
      if (selector === 'input:checked') {
        const walk = node => typeof node === 'object' && node ? [node, ...node.children.flatMap(walk)] : [];
        return this.children.flatMap(walk).filter(node => node.type === 'checkbox' && node.checked);
      }
      return this.controls || [];
    }
    addEventListener(k, f) { this.listeners[k] = f; }
    showModal() { this.open = true; }
    close() { this.open = false; }
  }
  const e = Object.fromEntries(['dialog', 'form', 'error', 'submit', 'cerrar'].map(x => [x, new E()]));
  e.form.action = '/editar'; e.form.dataset = { tokenUrl: '/token' };
  for (const id of ['patente', 'buscar', 'resultados', 'vehiculo', 'servicios', 'descuento', 'hora']) e[id] = new E();
  e.descuento.value = '10';
  e.form.controls = [e.patente, e.buscar, e.descuento, e.hora];
  const trigger = new E();
  let hour = '09:15';
  class FormData {
    constructor(form) { this.items = form ? [['Id','t'],['VersionEsperada','v'],['Fecha','2030-01-01'],['Hora',hour],['AceptaAdelanto','true'],['__RequestVerificationToken','csrf']] : []; }
    set(k,v) { this.items = this.items.filter(x => x[0] !== k); this.items.push([k,v]); }
    append(k,v) { this.items.push([k,v]); }
    entries() { return this.items[Symbol.iterator](); }
  }
  const window = { location: { reload() { this.reloaded = true; } } };
  vm.runInNewContext(source, { document: { getElementById: id => e[id.replace('reprogramacion-','')], querySelectorAll: () => [trigger], createElement: () => new E(), createTextNode: text => text }, window, fetch, FormData, Array, URLSearchParams });
  trigger.listeners.click();
  return { e, window, trigger, setHour: value => { hour = value; }, submit: () => e.form.listeners.submit({ preventDefault() {} }) };
}
const response = (status, data) => ({ ok: status >= 200 && status < 300, status, json: async () => data });

const car = { id: 'auto', descripcion: 'Auto ABC', patente: 'ABC', tipoVehiculo: 'auto' };
const service = { id: 's1', nombre: 'Lavado', precio: 100 };
const pack = { id: 'p1', nombre: 'Paquete', precio: 200, servicios: [{id:'s2'}, {id:'s3'}] };
const checked = ui => ui.e.servicios.children.flatMap(field => field.children.slice(1).map(label => label.children[0]));
function catalogFetch(calls) {
  return async (url, opts = {}) => {
    calls.push({url, opts});
    if (url.startsWith('/Turnos/Buscar')) return response(200, [car]);
    if (url.startsWith('/Turnos/ObtenerServicios')) return response(200, [service]);
    if (url.startsWith('/Turnos/ObtenerPaquetes')) return response(200, [pack]);
    return opts.method ? response(200, {success:true}) : response(200, {identity:'key'});
  };
}
async function selectCar(ui) {
  ui.e.patente.value = 'ABC'; ui.e.patente.listeners.input();
  await ui.e.buscar.listeners.click();
  await ui.e.resultados.children[0].listeners.click();
}

test('selección de vehículo, servicio y paquete envía IDs y descuento cambiado sin precios', async () => {
  const calls = [], ui = setup(catalogFetch(calls));
  await selectCar(ui);
  assert.equal(ui.e.vehiculo.textContent, car.descripcion);
  assert.equal(checked(ui).length, 2);
  checked(ui).forEach(c => { c.checked = true; });
  ui.e.descuento.value = '15'; await ui.submit();
  const items = calls.at(-1).opts.body.items;
  for (const pair of [['VehiculoId','auto'], ['Servicios[0].ServicioId','s1'], ['Servicios[1].ServicioId','s2'], ['Servicios[1].PaqueteId','p1'], ['Servicios[2].ServicioId','s3'], ['Servicios[2].PaqueteId','p1'], ['Descuento','15']]) assert.ok(items.some(x => x[0] === pair[0] && x[1] === pair[1]), pair.join('='));
  assert.ok(items.every(([key]) => !/precio/i.test(key)));
  assert.equal(ui.window.location.reloaded, true);
});

test('edición sólo de horario conserva catálogo y descuento al no enviarlos', async () => {
  const calls = [], ui = setup(catalogFetch(calls));
  ui.setHour('11:30'); await ui.submit();
  const items = calls.at(-1).opts.body.items;
  assert.ok(items.some(([k,v]) => k === 'Hora' && v === '11:30'));
  assert.ok(items.every(([k]) => !/^(VehiculoId|Servicios\[|Descuento)/.test(k)));
  assert.equal(ui.window.location.reloaded, true);
});

test('rechaza selecciones vacías o repetidas antes de solicitar identidad', async () => {
  const calls = [], ui = setup(catalogFetch(calls)); await selectCar(ui);
  const count = calls.length; await ui.submit();
  assert.equal(calls.length, count); assert.match(ui.e.error.textContent, /sin repetir/);
  checked(ui)[0].checked = true;
  checked(ui)[1].dataset.selections = JSON.stringify([{servicioId:'s1', paqueteId:'p1'}]);
  checked(ui)[1].checked = true; await ui.submit();
  assert.equal(calls.length, count); assert.match(ui.e.error.textContent, /sin repetir/);
});

test('consultas obsoletas no restauran vehículo ni servicios tras editar patente', async () => {
  let resolveSearch; const calls = [];
  const ui = setup((url, opts = {}) => { calls.push(url); if (url.startsWith('/Turnos/Buscar')) return new Promise(r => { resolveSearch = r; }); return catalogFetch([])(url, opts); });
  ui.e.patente.value = 'ABC'; const pending = ui.e.buscar.listeners.click();
  await ui.submit(); assert.match(ui.e.error.textContent, /Espere/);
  ui.e.patente.value = 'XYZ'; ui.e.patente.listeners.input();
  resolveSearch(response(200, [car])); await pending;
  assert.equal(ui.e.resultados.children.length, 0); assert.equal(ui.e.servicios.children.length, 0);
  await ui.submit(); assert.match(ui.e.error.textContent, /Seleccione un vehículo/); assert.equal(calls.length, 1);
});

test('catálogo tardío no restaura selección tras cambiar patente', async () => {
  let resolveServices; const calls = [];
  const ui = setup((url, opts = {}) => { calls.push(url); if (url.startsWith('/Turnos/ObtenerServicios')) return new Promise(r => { resolveServices = r; }); return catalogFetch([])(url, opts); });
  ui.e.patente.value = 'ABC'; await ui.e.buscar.listeners.click();
  const pending = ui.e.resultados.children[0].listeners.click();
  await ui.submit(); assert.match(ui.e.error.textContent, /Espere/);
  ui.e.patente.value = 'XYZ'; ui.e.patente.listeners.input();
  resolveServices(response(200, [service])); await pending;
  assert.equal(ui.e.vehiculo.textContent, ''); assert.equal(ui.e.servicios.children.length, 0);
  await ui.submit(); assert.match(ui.e.error.textContent, /Seleccione un vehículo/); assert.equal(calls.length, 3);
});

test('la vista condiciona botón, diálogo y script a permiso, estado y versión', () => {
  const view = fs.readFileSync('Views/Turnos/Detalle.cshtml', 'utf8');
  assert.match(view, /@if \(Model\.Estado == "Pendiente" && User\.TienePermiso\("Turnos\.Consultar"\) && User\.TienePermiso\("Turnos\.Modificar"\)\)\s*\{\s*@if \(!string\.IsNullOrEmpty\(Model\.VersionEsperada\)\)\s*\{\s*<a[^>]*asp-route-editar="@Model\.Id"/);
  const partial = fs.readFileSync('Views/Turnos/_TurnoReprogramacion.cshtml', 'utf8');
  for (const field of ['AntiForgeryToken', 'VersionEsperada', 'FormIdentity', 'Fecha', 'Hora', 'AceptaAdelanto']) assert.ok(partial.includes(field), field);
  assert.doesNotMatch(partial, /name="(?:ClienteId|CuponId|Precio)"/);
});

test('edición ofrece selección de catálogo y datos acordados sin enviar precios', () => {
  const partial = fs.readFileSync('Views/Turnos/_TurnoReprogramacion.cshtml', 'utf8');
  for (const value of ['VehiculoId', 'ServicioId', 'PaqueteId', 'Descuento', 'reprogramacion-buscar', 'reprogramacion-servicios']) assert.ok(partial.includes(value), value);
  for (const action of ['BuscarVehiculosPorPatente', 'ObtenerServiciosPorTipoVehiculo', 'ObtenerPaquetesPorTipoVehiculo']) assert.ok(source.includes(action), action);
  assert.match(source, /Servicios\[\$\{i\}\]\.ServicioId/);
  assert.match(source, /Servicios\[\$\{i\}\]\.PaqueteId/);
});

test('bloquea controles durante identidad y hasta resolver resultado ambiguo', async () => {
  let token; const ui = setup((url, opts) => opts.method ? Promise.resolve(response(503, {})) : new Promise(resolve => { token = resolve; }));
  const pending = ui.submit();
  for (const control of ui.e.form.controls) assert.equal(control.disabled, true);
  token(response(200, {identity:'key'})); await pending;
  for (const control of ui.e.form.controls) assert.equal(control.disabled, true);
});

test('una identidad inválida desbloquea los controles sin enviar', async () => {
  const ui = setup(async () => response(200, {})); await ui.submit();
  for (const control of ui.e.form.controls) assert.equal(control.disabled, false);
});

test('no guarda mientras una patente escrita espera selección', async () => {
  const calls = []; const ui = setup(async (...args) => { calls.push(args); return response(200, {identity:'key'}); });
  ui.e.patente.value = 'ABC123'; ui.e.patente.listeners.input();
  await ui.submit(); assert.equal(calls.length, 0); assert.match(ui.e.error.textContent, /vehículo/i);
});

test('conflicto de versión exige actualizar incluso tras cerrar y reabrir', async () => {
  const calls = [];
  const ui = setup(async (url, opts) => { calls.push({url, opts}); return opts.method ? response(409, {success:false, message:'Versión desactualizada'}) : response(200, {identity:'key'}); });
  await ui.submit();
  assert.equal(ui.e.error.textContent, 'Versión desactualizada Actualice el detalle antes de volver a intentar.');
  assert.equal(ui.e.submit.disabled, true);
  assert.equal(ui.e.cerrar.disabled, false);
  ui.e.cerrar.listeners.click(); ui.trigger.listeners.click();
  await ui.submit();
  assert.equal(calls.length, 2);
  assert.equal(ui.window.location.reloaded, undefined);
});

test('ambigüedad congela todos los campos incluyendo antiforgery, impide duplicados y reproduce exactamente', async () => {
  const calls = []; let resolvePost;
  const ui = setup((url, opts) => { calls.push({url, opts}); if (!opts.method) return Promise.resolve(response(200, {identity:'key'})); if (calls.length === 2) return new Promise(r => { resolvePost = r; }); return Promise.resolve(response(200, {success:true})); });
  const first = ui.submit(); await tick();
  await ui.submit(); assert.equal(calls.length, 2);
  resolvePost(response(503, {success:false})); await first;
  assert.match(ui.e.error.textContent, /misma solicitud/);
  assert.equal(ui.e.cerrar.disabled, true);
  let prevented = false; ui.e.dialog.listeners.cancel({preventDefault() { prevented = true; }}); assert.equal(prevented, true);
  ui.setHour('10:00'); await ui.submit();
  assert.deepEqual(calls[1].opts.body.items, calls[2].opts.body.items);
  assert.ok(calls[1].opts.body.items.some(([k,v]) => k === '__RequestVerificationToken' && v === 'csrf'));
  assert.ok(calls[1].opts.body.items.some(([k,v]) => k === 'FormIdentity' && v === 'key'));
  assert.equal(ui.window.location.reloaded, true);
});

test('respuesta POST malformada es ambigua y se reproduce sin obtener otra identidad', async () => {
  const calls = [];
  const ui = setup(async (url, opts) => { calls.push({url, opts}); if (!opts.method) return response(200, {identity:'key'}); if (calls.length === 2) return {ok:true, status:200, json:async () => { throw new SyntaxError('invalid'); }}; return response(200, {success:true}); });
  await ui.submit(); assert.match(ui.e.error.textContent, /misma solicitud/);
  ui.setHour('11:00'); await ui.submit();
  assert.equal(calls.length, 3); assert.deepEqual(calls[1].opts.body.items, calls[2].opts.body.items);
  assert.equal(ui.window.location.reloaded, true);
});

test('identidad malformada no envía POST y permite reintentar', async () => {
  const calls = [];
  const ui = setup(async (url, opts) => { calls.push({url,opts}); return calls.length === 1 ? response(200, {}) : opts.method ? response(200, {success:true}) : response(200, {identity:'key'}); });
  await ui.submit(); assert.equal(calls.length, 1); assert.match(ui.e.error.textContent, /preparar/); assert.equal(ui.e.submit.disabled, false);
  await ui.submit(); assert.equal(calls.length, 3); assert.equal(ui.window.location.reloaded, true);
});
