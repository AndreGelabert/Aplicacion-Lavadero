const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
class Element {
  constructor() { this.listeners = {}; this.disabled = false; this.textContent = ''; }
  addEventListener(k, f) { this.listeners[k] = f; }
  showModal() { this.open = true; }
  close() { this.open = false; if (this.listeners.close) this.listeners.close(); }
  focus() { this.focused = true; }
}
function setup(response) {
  const ids = ['dialog','form','error','submit','cancel','close','motivo'];
  const e = Object.fromEntries(ids.map(id => [id, new Element()]));
  e.form.action = '/Turnos/Cancelar'; e.form.querySelector = () => ({ value: 'csrf' });
  const trigger = new Element(), calls = [], window = { location: { reload() { this.reloaded = true; } } };
  const document = { getElementById: id => e[id.replace('turno-cancelacion-', '')], querySelectorAll: () => [trigger] };
  class FormData { constructor() { this.fields = { id: 'turno 1', __RequestVerificationToken: 'csrf', motivo: e.motivo.value }; } }
  const fetch = (...args) => { calls.push(args); return response(); };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/turno-cancelacion.js','utf8'), { document, window, fetch, FormData });
  return { e, trigger, calls, window };
}
const submit = c => { if (c.e.motivo.value === '') c.e.motivo.value = 'Motivo'; return c.e.form.listeners.submit({ preventDefault() {} }); };
test('vista conserva inicio y limita cancelación a pendientes con permiso', () => {
  const view = fs.readFileSync('Views/Turnos/Detalle.cshtml','utf8');
  assert.match(view, /Model\.Estado == "Pendiente" && User\.TienePermiso\("Turnos\.Consultar"\) && User\.TienePermiso\("Turnos\.Cancelar"\)/);
  for (const value of ['_TurnoInicio','Lavados.Crear','Lavados.Modificar','_TurnoCancelacion','data-turno-cancelacion']) assert.ok(view.includes(value));
  const partial = fs.readFileSync('Views/Turnos/_TurnoCancelacion.cshtml','utf8');
  for (const value of ['<dialog','method="post"','AntiForgeryToken','name="id"','aria-live','Cancelar", "Turnos']) assert.ok(partial.includes(value), value);
});
test('diálogo describe la confirmación y devuelve el foco al abrir y cerrar', () => {
  const partial = fs.readFileSync('Views/Turnos/_TurnoCancelacion.cshtml','utf8');
  assert.match(partial, /<dialog[^>]*aria-describedby="turno-cancelacion-description"/);
  assert.match(partial, /<p[^>]*id="turno-cancelacion-description"[^>]*>¿Está seguro de que desea cancelar este turno\?<\/p>/);
  const c = setup(() => {});
  c.trigger.listeners.click(); c.e.cancel.listeners.click();
  assert.equal(c.trigger.focused, true);
  c.trigger.focused = false; c.trigger.listeners.click(); c.e.dialog.close();
  assert.equal(c.trigger.focused, true);
});
test('modal replica presentación de Lavados y requiere motivo', () => {
  const partial = fs.readFileSync('Views/Turnos/_TurnoCancelacion.cshtml','utf8');
  const lavado = fs.readFileSync('Views/Lavados/Detalle.cshtml','utf8');
  for (const value of ['relative p-4 text-center bg-white rounded-lg shadow dark:bg-gray-800 sm:p-5', 'text-red-400 dark:text-red-500 w-11 h-11 mb-3.5 mx-auto', 'flex justify-center items-center space-x-4', 'No, cancelar', 'Sí, confirmar', 'M9 2a1 1 0 00-.894.553']) {
    assert.ok(lavado.includes(value), value); assert.ok(partial.includes(value), value);
  }
  assert.match(partial, /<textarea[^>]*name="motivo"[^>]*required/);
  const c = setup(() => {}); c.trigger.listeners.click(); c.e.close.listeners.click();
  assert.equal(c.e.dialog.open, false); assert.equal(c.trigger.focused, true); assert.equal(c.calls.length, 0);
});
test('mantiene abierto el diálogo durante el envío incluso al intentar cancelar', async () => {
  let resolve; const c = setup(() => new Promise(r => { resolve = r; }));
  c.trigger.listeners.click(); const pending = submit(c);
  c.e.cancel.listeners.click(); c.e.close.listeners.click(); let prevented = false;
  c.e.dialog.listeners.cancel({ preventDefault() { prevented = true; } });
  assert.equal(prevented, true); assert.equal(c.e.dialog.open, true); assert.equal(c.trigger.focused, undefined);
  resolve({ok:false,json:async()=>({message:'Conflicto'})}); await pending;
  c.e.cancel.listeners.click(); assert.equal(c.trigger.focused, true);
});
test('confirmación rechazada no envía; confirmada envía formulario y recarga solo al éxito', async () => {
  const c = setup(async () => ({ok:true,json:async()=>({success:true})}));
  c.trigger.listeners.click(); c.e.cancel.listeners.click(); assert.equal(c.calls.length,0);
  c.trigger.listeners.click(); c.e.motivo.value = 'Motivo'; await submit(c);
  assert.equal(c.calls.length,1); assert.equal(c.calls[0][0],'/Turnos/Cancelar'); assert.equal(c.calls[0][1].method,'POST');
  assert.deepEqual(c.calls[0][1].body.fields,{id:'turno 1',__RequestVerificationToken:'csrf',motivo:'Motivo'}); assert.equal(c.window.location.reloaded,true);
});
test('rechaza motivo de espacios sin enviar y conserva abierto el diálogo', async () => {
  const c = setup(async () => ({ok:true,json:async()=>({success:true})}));
  c.trigger.listeners.click(); c.e.motivo.value = '   ';
  await c.e.form.listeners.submit({ preventDefault() {} });
  assert.equal(c.calls.length, 0); assert.equal(c.e.dialog.open, true);
  assert.equal(c.e.error.textContent, 'Debe ingresar un motivo de cancelación.');
  assert.equal(c.e.motivo.focused, true); assert.equal(c.window.location.reloaded, undefined);
});
test('evita doble envío y muestra error del servidor', async () => {
  let resolve; const c = setup(() => new Promise(r => {resolve=r;})); c.trigger.listeners.click();
  const first = submit(c); await submit(c); assert.equal(c.calls.length,1); assert.equal(c.e.submit.disabled,true);
  resolve({ok:false,json:async()=>({success:false,message:'Conflicto'})}); await first;
  assert.equal(c.e.error.textContent,'Conflicto'); assert.equal(c.e.submit.disabled,false); assert.equal(c.window.location.reloaded,undefined);
});
test('respuesta ambigua o error de red nunca afirma cancelación', async () => {
  for (const response of [async()=>{throw Error('network');}, async()=>({ok:true,json:async()=>({})}), async()=>({ok:false,json:async()=>{throw Error();}})]) {
    const c = setup(response); c.trigger.listeners.click(); await submit(c);
    assert.match(c.e.error.textContent,/No se pudo confirmar/); assert.equal(c.window.location.reloaded,undefined);
  }
});
