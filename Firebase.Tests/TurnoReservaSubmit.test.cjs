const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

class Element {
  constructor(tag = '') { this.tag = tag; this.children = []; this.listeners = {}; this.dataset = {}; this.value = ''; this.textContent = ''; this.disabled = false; }
  addEventListener(name, fn) { this.listeners[name] = fn; }
  append(...items) { this.children.push(...items); }
  add(item) { this.append(item); }
  replaceChildren(...items) { this.children = items; }
  after(item) { this.afterElement = item; }
  querySelector(selector) { for (const child of this.children) { const found = child.querySelector?.(selector); if (found) return found; } return null; }
  querySelectorAll() { const visit = node => [node, ...node.children.flatMap(visit)]; return this.children.flatMap(visit).filter(x => x.tag === 'input' && x.checked); }
}
class VisualElement extends Element {
  set innerHTML(html) {
    this.html = html;
    if (html.includes('id="servicios-select-turno"')) {
      this.selects = {};
      for (const match of html.matchAll(/<select id="([^"]+)"[^>]*>([\s\S]*?)<\/select>/g)) {
        const select = new Element('select'); select.options = [];
        for (const option of match[2].matchAll(/<option value="([^"]*)"[^>]*>/g)) {
          const node = new Element('option'); node.value = option[1]; node.style = {}; select.options.push(node);
        }
        this.selects[match[1]] = select;
      }
      this.list = new VisualElement('list');
    } else if (this.tag === 'list') {
      this.buttons = [...html.matchAll(/data-del-key="([^"]+)"/g)].map(match => {
        const button = new Element('button'); button.dataset.delKey = match[1]; return button;
      });
    }
  }
  get innerHTML() { return this.html || ''; }
  querySelector(selector) { return selector === '#servicios-lista-turno' ? this.list : this.selects?.[selector.slice(1)]; }
  querySelectorAll(selector) { return selector === '.btn-remove-item' ? this.buttons || [] : super.querySelectorAll(selector); }
}
function setup(post, couponFetch = null, identity = 'identity-123', clientPost = null, catalogue = null, edit = null) {
  const ids = ['turno-form','turnoMensaje','turnoPatente','VehiculoId','ClienteId','CuponId','turnoCuponMensaje','turnoInfoVehiculo','turnoServiciosPorVehiculo','ClienteTrajoId','ClienteRetiraId','turnoResumen','turnoPrecio','turnoTiempo','turnoPatenteResultados','Descuento','Fecha','Hora','Notas','AceptaAdelanto'];
  const el = Object.fromEntries(ids.map(id => [id, new Element()]));
  if (clientPost) {
    for (const id of ['registroClienteForm','registroClienteModal','registroClienteTipoDocumento','registroClienteNumeroDocumento','registroClienteNombre','registroClienteApellido','registroClienteTelefono','registroClienteEmail','registroClienteClave']) el[id] = new Element();
    el.registroClienteForm.querySelector = () => new Element('button');
    el.registroClienteForm.reset = () => {};
  }
  const button = new Element('button'); const form = el['turno-form']; form.action = '/Turnos/Crear'; form.querySelector = () => button;
  if (edit) {
    for (const id of ['turno-form-container','turnoEdicionAviso','form-title','accordion-flush-body-1']) el[id] = new Element();
    el['turno-form-container'].dataset = {puedeCrear:'true', editar:'t'};
    el['accordion-flush-body-1'].classList = {remove() {}};
    el.Fecha.focus = () => {};
    for (const id of ['ClienteTrajoId','ClienteRetiraId']) Object.defineProperty(el[id], 'options', {get() { return this.children; }});
    el.turnoServiciosPorVehiculo.querySelectorAll = function(selector) { if (selector === 'input, select, button') return []; return Element.prototype.querySelectorAll.call(this, selector); };
  }
  form.querySelectorAll = () => [...Object.values(el).filter(x => x !== form), button, ...el.turnoServiciosPorVehiculo.querySelectorAll('input:checked')];
  const calls = [];
  const fetch = async (url, options) => {
    if (url.includes('DatosEdicion')) return response(edit.data);
    if (url.includes('EditarToken')) return edit.token();
    if (options && url.includes('RegistrarClienteAsociado')) return clientPost();
    if (options) { calls.push({url, options}); return post(); }
    if (url.includes('TurnoCupones') && couponFetch) return couponFetch();
    if (url.includes('BuscarVehiculos')) return response([{id:'v',patente:'ABC',descripcion:'Auto',tipoVehiculo:'auto'}]);
    if (url.includes('ObtenerClientes')) return response([{id:'c',nombre:'Cliente',documento:'1'}]);
    if (url.includes('ObtenerServicios')) { if (edit?.catalogueFails) throw new Error('catalogue'); return response(catalogue?.services || [{id:'s',nombre:'Lavado',precio:100,tiempoEstimado:30}]); }
    if (url.includes('ObtenerPaquetes')) return response(catalogue?.packages || []);
    return response([]);
  };
  class FormData { constructor() { this.entries = [['FormIdentity',identity], ...['VehiculoId','ClienteId','ClienteTrajoId','ClienteRetiraId','Fecha','Hora','Notas','Descuento','CuponId','AceptaAdelanto'].map(id => [id,el[id].value])]; } append(k,v) { this.entries.push([k,v]); } delete(k) { this.entries = this.entries.filter(e => e[0] !== k); } set(k,v) { this.delete(k); this.append(k,v); } get(k) { return this.entries.find(e => e[0] === k)?.[1] ?? null; } }
  class Option { constructor(text,value) { this.textContent=text; this.value=value; } }
  const document = {getElementById: id => el[id], querySelector: () => null, createElement: tag => tag === 'div' ? new VisualElement(tag) : new Element(tag), createTextNode: text => ({textContent:text,children:[]})};
  const window = {location: {href: '/Turnos/Crear'}};
  vm.runInNewContext(fs.readFileSync('wwwroot/js/turnos.js','utf8'), {document,window,fetch,FormData,Option,URLSearchParams,Date,JSON,Map,Set,Number});
  return {el, button, calls, form, window};
}
module.exports = { setup, response: (body, ok = true) => ({ok, json: async () => body}), submit: c => c.form.listeners.submit({preventDefault(){}}) };
const response = (body, ok = true) => ({ok, json: async () => body});
async function ready(c) {
  await c.el.turnoPatente.afterElement.listeners.click();
  await c.el.turnoPatenteResultados.children[0].listeners.click();
  const service = c.el.turnoServiciosPorVehiculo.querySelectorAll('input:checked');
  assert.equal(service.length, 0);
  c.el.turnoServiciosPorVehiculo.children[0].children[1].children[0].checked = true;
}
function submit(c) { let prevented = false; const result = c.form.listeners.submit({preventDefault(){prevented = true;}}); assert.equal(prevented,true); return result; }

test('servicios individuales filtran tipos seleccionados y restauran opciones al quitar servicios y paquetes', async () => {
  const services = [
    {id:'a', nombre:'A', tipo:'Exterior', precio:100, tiempoEstimado:10},
    {id:'b', nombre:'B', tipo:'Exterior', precio:120, tiempoEstimado:12},
    {id:'c', nombre:'C', tipo:'Interior', precio:80, tiempoEstimado:8},
    {id:'d', nombre:'D', tipo:'Motor', precio:60, tiempoEstimado:6}
  ];
  const packages = [{id:'p', nombre:'Pack', precio:80, tiempoEstimado:8, servicios:[services[2]]}];
  const c = setup(() => response({success:true}), null, 'identity', null, {services, packages});
  await c.el.turnoPatente.afterElement.listeners.click();
  await c.el.turnoPatenteResultados.children[0].listeners.click();
  const visual = c.el.turnoServiciosPorVehiculo.children[2];
  const select = visual.querySelector('#servicios-select-turno');
  const pkg = visual.querySelector('#paquete-select-turno');
  const states = () => select.options.slice(1).map(o => [o.value, o.disabled, o.style.display || '']);
  const choose = (control, value) => { control.value = value; control.listeners.change(); };
  choose(select, 'a');
  assert.deepEqual(states(), [['a',true,'none'],['b',true,'none'],['c',false,''],['d',false,'']]);
  choose(pkg, 'p');
  assert.equal(pkg.disabled, true);
  assert.deepEqual(states(), [['a',true,'none'],['b',true,'none'],['c',true,'none'],['d',false,'']]);
  visual.list.buttons.find(b => b.dataset.delKey === 'srv_a').listeners.click();
  assert.deepEqual(states(), [['a',false,''],['b',false,''],['c',true,'none'],['d',false,'']]);
  visual.list.buttons.find(b => b.dataset.delKey === 'pkg_p').listeners.click();
  assert.equal(pkg.disabled, false);
  assert.deepEqual(states(), [['a',false,''],['b',false,''],['c',false,''],['d',false,'']]);
});

test('paquete omite tipos repetidos como Lavados y filtra tipos ausentes sin conflarlos', async () => {
  const services = [
    {id:'a', nombre:'A', tipo:'Exterior', precio:100, tiempoEstimado:10},
    {id:'b', nombre:'B', tipo:'Exterior', precio:120, tiempoEstimado:12},
    {id:'c', nombre:'C', precio:80, tiempoEstimado:8},
    {id:'d', nombre:'D', tipo:'', precio:60, tiempoEstimado:6}
  ];
  const packages = [{id:'p', nombre:'Pack', precio:220, tiempoEstimado:22, servicios:services.slice(0,2)}];
  const c = setup(() => response({success:true}), null, 'identity', null, {services, packages});
  await c.el.turnoPatente.afterElement.listeners.click();
  await c.el.turnoPatenteResultados.children[0].listeners.click();
  const visual = c.el.turnoServiciosPorVehiculo.children[2];
  const pkg = visual.querySelector('#paquete-select-turno');
  const select = visual.querySelector('#servicios-select-turno');
  pkg.value = 'p'; pkg.listeners.change();
  assert.equal(c.el.turnoMensaje.textContent, 'No se puede agregar el paquete completo por servicios duplicados, incompatibles o ya seleccionados.');
  assert.equal(pkg.disabled, false);
  assert.equal(c.el.turnoPrecio.textContent, '$0.00'); assert.equal(c.el.turnoTiempo.textContent, '0 min');
  assert.deepEqual(select.options.slice(1).map(o => o.disabled), [false,false,false,false]);
  await submit(c);
  assert.equal(c.calls.length, 0);
});

test('paquete completo conserva precio, duración y servicios para el contrato del servidor', async () => {
  const services = [{id:'a',nombre:'A',tipo:'Exterior',precio:100,tiempoEstimado:10}, {id:'b',nombre:'B',tipo:'Interior',precio:50,tiempoEstimado:5}];
  const packages = [{id:'p',nombre:'Pack',precio:120,precioOriginal:150,descuento:20,tiempoEstimado:15,servicios:services}];
  const c = setup(() => response({success:true}), null, 'identity', null, {services,packages});
  await c.el.turnoPatente.afterElement.listeners.click(); await c.el.turnoPatenteResultados.children[0].listeners.click();
  const pkg = c.el.turnoServiciosPorVehiculo.children[2].querySelector('#paquete-select-turno');
  pkg.value = 'p'; pkg.listeners.change();
  assert.equal(c.el.turnoPrecio.textContent, '$120.00'); assert.equal(c.el.turnoTiempo.textContent, '15 min');
  await submit(c);
  assert.deepEqual(c.calls[0].options.body.entries.filter(e => e[0].startsWith('Servicios[')), [['Servicios[0].ServicioId','a'],['Servicios[0].PaqueteId','p'],['Servicios[1].ServicioId','b'],['Servicios[1].PaqueteId','p']]);
});

test('clientes asociados: ambos selectores eligen el primer cliente sin opción vacía', async () => {
  const c = setup(() => response({success:true})); await ready(c);
  for (const id of ['ClienteTrajoId', 'ClienteRetiraId']) {
    assert.deepEqual(c.el[id].children.map(option => option.value), ['c']);
    assert.equal(c.el[id].value, 'c');
  }
});

test('registro asociado selecciona el nuevo cliente en ambos selectores y actualiza el cliente del turno', async () => {
  const c = setup(() => response({success:true}), null, 'identity-123', () => response({success:true, message:'Registrado', cliente:{id:'nuevo', nombre:'Nuevo', documento:'2'}}));
  await ready(c);
  let prevented = false;
  await c.el.registroClienteForm.listeners.submit({preventDefault() { prevented = true; }});
  assert.equal(prevented, true);
  for (const id of ['ClienteTrajoId', 'ClienteRetiraId']) {
    assert.deepEqual(c.el[id].children.map(option => option.value), ['c', 'nuevo']);
    assert.equal(c.el[id].value, 'nuevo');
  }
  assert.equal(c.el.ClienteId.value, 'nuevo');
  assert.equal(c.el.turnoMensaje.textContent, 'Registrado');
});

test('reentrant: ignora envíos pendientes y después del éxito', async () => {
  const pending = []; const c = setup(() => new Promise(r => { pending.push(r); })); await ready(c);
  const first = submit(c); const second = submit(c);
  const count = c.calls.length; const disabled = c.button.disabled;
  for (const resolve of pending) resolve(response({success:true,message:'Registrado'}));
  await Promise.all([first, second]);
  assert.equal(count,1); assert.equal(disabled,true);
  assert.equal(c.el.turnoMensaje.textContent,'Registrado'); assert.equal(c.button.disabled,true);
  assert.equal(c.window.location.href, '/Turnos');
  await submit(c); assert.equal(c.calls.length,1);
  assert.equal(c.calls[0].options.body.entries[0][1], 'identity-123');
  assert.deepEqual(c.calls[0].options.body.entries.at(-1), ['Servicios[0].ServicioId','s']);
});

test('reentrant: bloquea cambio de vehículo pendiente y tras éxito hasta abrir nuevo formulario', async () => {
  let resolve; const c = setup(() => new Promise(r => { resolve = r; })); await ready(c);
  const first = submit(c);
  const locked = [c.el.turnoPatente.disabled, c.el.turnoPatente.afterElement.disabled];
  c.el.turnoPatente.value = 'OTRA'; c.el.turnoPatente.listeners.input();
  await c.el.turnoPatente.afterElement.listeners.click();
  const vehicleDuring = c.el.VehiculoId.value;
  resolve(response({success:true,message:'Turno registrado'})); await first;
  assert.deepEqual(locked,[true,true]); assert.equal(vehicleDuring,'v'); assert.equal(c.el.turnoPatente.value,'ABC');
  assert.equal(c.el.turnoMensaje.textContent,'Turno registrado'); assert.equal(c.button.disabled,true);
  assert.equal(c.el.turnoPatente.disabled,true); await submit(c); assert.equal(c.calls.length,1);
});

test('retry: fallo ambiguo conserva selección e identidad de reintento', async () => {
  let reject; const c = setup(() => new Promise((_, r) => { reject = r; })); await ready(c);
  const first = submit(c); c.el.turnoPatente.value = 'OTRA'; c.el.turnoPatente.listeners.input();
  reject(new Error('network')); await first;
  assert.equal(c.el.VehiculoId.value,'v'); assert.equal(c.el.turnoPatente.value,'ABC');
  assert.equal(c.el.turnoMensaje.textContent,'No se pudo confirmar el resultado. Reintente la misma solicitud sin modificarla.');
  assert.equal(c.el.turnoPatente.disabled,true); assert.equal(c.el.turnoPatente.afterElement.disabled,true);
  assert.equal(c.button.disabled,false); assert.equal(c.calls[0].options.body.entries[0][1],'identity-123');
  assert.equal(c.window.location.href, '/Turnos/Crear');
});

test('retry: respuesta JSON inválida mantiene captura y ofrece nuevo formulario solo tras éxito', async () => {
  let attempt = 0;
  const c = setup(() => ++attempt === 1 ? {ok:true, json:async () => { throw new Error('invalid JSON'); }} : response({success:true,message:'Registrado'}));
  await ready(c); c.el.Fecha.value = '2030-01-01'; c.el.Hora.value = '12:00'; c.el.Notas.value = 'original';
  await submit(c);
  for (const id of ['Fecha','Hora','Notas','AceptaAdelanto','ClienteTrajoId','ClienteRetiraId','CuponId','Descuento']) assert.equal(c.el[id].disabled,true,id);
  assert.equal(c.el.turnoPatente.disabled,true);
  c.el.Notas.value = 'alterada'; c.el.Fecha.value = '2040-01-01';
  c.el.turnoServiciosPorVehiculo.children[0].children[1].children[0].checked = false;
  await submit(c);
  assert.equal(c.calls.length,2);
  assert.deepEqual(c.calls[1].options.body.entries,c.calls[0].options.body.entries);
  assert.equal(c.el.turnoMensaje.textContent,'Registrado');
  assert.equal(c.window.location.href, '/Turnos');
});

test('retry: éxito confirmado vuelve a turnos sin duplicar el anterior', async () => {
  let attempt = 0;
  const original = setup(() => ++attempt === 1 ? Promise.reject(new Error('network')) : response({success:true,message:'Registrado'}));
  await ready(original); await submit(original); await submit(original);
  assert.deepEqual(original.calls[1].options.body.entries, original.calls[0].options.body.entries);
  assert.equal(original.window.location.href, '/Turnos');
  await submit(original); assert.equal(original.calls.length, 2);

  // Simulate the GET navigation supplying a newly issued identity, not an in-place reset.
  const fresh = setup(() => response({success:true,message:'Registrado'}), null, 'identity-456');
  assert.equal(fresh.button.disabled, false); assert.equal(fresh.el.Fecha.disabled, false);
  await ready(fresh); await submit(fresh);
  assert.equal(fresh.calls.length, 1);
  assert.equal(fresh.calls[0].options.body.entries[0][1], 'identity-456');
  assert.notEqual(fresh.calls[0].options.body.entries[0][1], original.calls[0].options.body.entries[0][1]);
});

test('retry: HTTP 400 y 503 conservan solicitud exacta hasta confirmación', async () => {
  let attempt = 0;
  const c = setup(() => ++attempt === 1 ? {status:400, ...response({success:false,message:'Datos de turno inválidos.'},false)} : attempt === 2 ? {status:503, ...response({success:false},false)} : response({success:true,message:'Registrado'}));
  await ready(c); await submit(c);
  assert.equal(c.el.turnoPatente.disabled,true); assert.equal(c.button.disabled,false);
  await submit(c); assert.equal(c.el.turnoPatente.disabled,true);
  await submit(c); assert.equal(c.calls.length,3);
  assert.deepEqual(c.calls.map(x => x.options.body.entries), [c.calls[0].options.body.entries,c.calls[0].options.body.entries,c.calls[0].options.body.entries]);
  assert.equal(c.el.turnoMensaje.textContent,'Registrado');
});

test('validation: rechazo explícito pre-escritura permite corregir y muestra mensaje', async () => {
  let attempt = 0;
  const c = setup(() => ++attempt === 1 ? {status:400, ...response({success:false, editable:true, message:'Corrija los datos.'},false)} : response({success:true,message:'Registrado'}));
  await ready(c); await submit(c);
  assert.equal(c.el.turnoMensaje.textContent,'Corrija los datos.');
  assert.equal(c.el.Fecha.disabled,false); assert.equal(c.el.turnoPatente.afterElement.disabled,false);
  c.el.Fecha.value = '2030-01-01'; await submit(c);
  assert.equal(c.calls[1].options.body.entries.find(x => x[0] === 'Fecha')[1], '2030-01-01');
});

test('disponibilidad: rechazo explícito permite corregir y enviar nueva captura', async () => {
  let attempt = 0;
  const c = setup(() => ++attempt === 1 ? {status:409, ...response({success:false,editable:true,message:'No hay disponibilidad para el turno.'},false)} : response({success:true,message:'Registrado'}));
  await ready(c); await submit(c);
  assert.equal(c.el.turnoMensaje.textContent,'No hay disponibilidad para el turno.');
  assert.equal(c.el.Fecha.disabled,false); assert.equal(c.el.turnoPatente.disabled,false);
  c.el.Fecha.value = '2031-02-03'; await submit(c);
  assert.equal(c.calls[1].options.body.entries.find(x => x[0] === 'Fecha')[1],'2031-02-03');
});

test('disponibilidad: rechazo tras ambigüedad conserva solicitud original', async () => {
  let attempt = 0;
  const c = setup(() => ++attempt === 1 ? Promise.reject(new Error('network')) : {status:409, ...response({success:false,editable:true,message:'No hay disponibilidad para el turno.'},false)});
  await ready(c); await submit(c); await submit(c);
  assert.equal(c.el.Fecha.disabled,true); assert.deepEqual(c.calls[1].options.body.entries,c.calls[0].options.body.entries);
  assert.match(c.el.turnoMensaje.textContent,/Reintente la misma solicitud/);
});

test('retry: rechazo editable tras resultado ambiguo conserva captura', async () => {
  let attempt = 0;
  const c = setup(() => ++attempt === 1 ? Promise.reject(new Error('network')) : {status:400, ...response({success:false,editable:true,message:'Corrija los datos.'},false)});
  await ready(c); await submit(c); await submit(c);
  assert.equal(c.el.Fecha.disabled,true);
  assert.deepEqual(c.calls[1].options.body.entries,c.calls[0].options.body.entries);
  assert.match(c.el.turnoMensaje.textContent,/Corrija los datos\./);
  assert.match(c.el.turnoMensaje.textContent,/Reintente la misma solicitud/);
});

test('retry: respuesta tardía de cupón no modifica formulario durante la reserva', async () => {
  let resolveCoupon, resolveBooking;
  const c = setup(() => new Promise(r => { resolveBooking = r; }), () => new Promise(r => { resolveCoupon = r; }));
  await ready(c); const booking = submit(c);
  resolveCoupon(response([{id:'coupon',percentage:20,expiresUtc:'2030-01-01T00:00:00Z'}]));
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(c.el.CuponId.children.length,1); assert.equal(c.el.turnoCuponMensaje.textContent,'');
  resolveBooking(response({success:true,message:'Registrado'})); await booking;
});

test('retry y validation conservan identidad y validación previa', async () => {
  let attempt = 0; const c = setup(() => ++attempt === 1 ? Promise.reject(new Error('network')) : response({success:true,message:'Registrado'}));
  await submit(c); assert.equal(c.calls.length,0); assert.equal(c.el.turnoMensaje.textContent,'Seleccione un vehículo y al menos un servicio o paquete.');
  await ready(c); await submit(c);
  assert.equal(c.el.turnoMensaje.textContent,'No se pudo confirmar el resultado. Reintente la misma solicitud sin modificarla.'); assert.equal(c.button.disabled,false);
  await submit(c); assert.equal(c.calls.length,2); assert.equal(c.button.disabled,true); assert.equal(c.el.turnoMensaje.textContent,'Registrado');
  assert.deepEqual(c.calls.map(call => call.options.body.entries[0]), [['FormIdentity','identity-123'],['FormIdentity','identity-123']]);
});
