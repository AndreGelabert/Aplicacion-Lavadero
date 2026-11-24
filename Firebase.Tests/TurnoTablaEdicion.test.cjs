const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function setup() {
  class Element {
    constructor(tag = '') { this.tag = tag; this.children = []; this.listeners = {}; this.dataset = {}; this.value = ''; this.textContent = ''; this.disabled = false; }
    addEventListener(name, fn) { this.listeners[name] = fn; }
    click() { return this.listeners.click?.(); }
    append(...items) { this.children.push(...items); }
    add(item) { this.append(item); }
    replaceChildren(...items) { this.children = items; }
    after(item) { this.afterElement = item; }
    querySelectorAll() { return []; }
  }
  const ids = ['turno-form','turnoMensaje','turnoPatente','VehiculoId','ClienteId','CuponId','turnoCuponMensaje','turnoInfoVehiculo','turnoServiciosPorVehiculo','ClienteTrajoId','ClienteRetiraId','turnoResumen','turnoPrecio','turnoTiempo','turnoPatenteResultados','Descuento','Fecha'];
  const el = Object.fromEntries(ids.map(id => [id, new Element()]));
  const timers = [], notices = [], calls = [];
  const document = { getElementById: id => el[id], createElement: tag => new Element(tag), createTextNode: text => ({textContent:text}) };
  const fetch = async url => {
    calls.push(url);
    if (url.includes('BuscarVehiculos')) return {ok:true,json:async()=>[{id:'v',patente:'AB',descripcion:'Auto',tipoVehiculo:'auto'}]};
    if (url.includes('ObtenerClientes')) return {ok:true,json:async()=>[{id:'c',nombre:'Cliente',documento:'1'}]};
    if (url.includes('TurnoCupones')) throw Error('network');
    return {ok:true,json:async()=>[]};
  };
  class Option { constructor(text,value) { this.textContent=text; this.value=value; } }
  const window = { Notificaciones: { mostrar: (...args) => notices.push(args) } };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/turnos.js','utf8'), {document,window,fetch,Option,URLSearchParams,Date,JSON,Map,Set,Number,setTimeout: fn => { timers.push(fn); return timers.length; },clearTimeout:()=>{}});
  return {el,timers,notices,calls};
}
test('búsqueda no consulta ni notifica con una letra, consulta con dos', async () => {
  const c = setup(); c.el.turnoPatente.value = 'A'; c.el.turnoPatente.listeners.input();
  assert.equal(c.timers.length,0); assert.equal(c.calls.length,0); assert.deepEqual(c.notices,[]);
  c.el.turnoPatente.value = 'AB'; c.el.turnoPatente.listeners.input(); assert.equal(c.timers.length,1);
  c.timers[0](); await new Promise(resolve => setImmediate(resolve));
  assert.equal(c.calls.filter(x => x.includes('BuscarVehiculos')).length,1);
});
test('fallo de cupones informa en línea y por notificación', async () => {
  const c = setup(); await c.el.turnoPatente.afterElement.listeners.click();
  await c.el.turnoPatenteResultados.children[0].listeners.click();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(c.el.turnoCuponMensaje.textContent,'No se pudieron consultar los cupones.');
  assert.ok(c.notices.some(([text,type]) => text === 'No se pudieron consultar los cupones.' && type === 'error'));
});
