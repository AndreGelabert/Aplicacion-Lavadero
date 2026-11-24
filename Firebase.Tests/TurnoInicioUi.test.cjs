const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

class Element {
  constructor(tag = '') { this.tag = tag; this.children = []; this.dataset = {}; this.listeners = {}; this.value = ''; this.textContent = ''; this.disabled = false; }
  addEventListener(name, fn) { this.listeners[name] = fn; }
  append(...items) { this.children.push(...items); }
  replaceChildren(...items) { this.children = items; }
  setAttribute(k, v) { this[k] = v; }
  removeAttribute(k) { delete this[k]; }
  focus() { this.focused = true; }
  showModal() { if (this.open) throw new Error('InvalidStateError'); this.open = true; }
  close() { this.open = false; if (this.listeners.close) this.listeners.close(); }
}
function setup(responses) {
  const elements = Object.fromEntries(['turno-inicio-dialog','turno-inicio-form','turno-inicio-items','turno-inicio-error','turno-inicio-submit','turno-inicio-cancel'].map(id => [id, new Element()]));
  const form = elements['turno-inicio-form']; form.dataset = { turnoId: 't', recursosUrl: '/Turnos/RecursosInicio', inicioUrl: '/Turnos/Iniciar', lavadoUrl: '/Lavados/Detalle/' };
  form.querySelector = () => ({value:'csrf'});
  const trigger = new Element(); const calls = [];
  const document = { getElementById: id => elements[id], querySelectorAll: () => [trigger], createElement: tag => new Element(tag) };
  const window = { location: { href: '' } };
  const fetch = async (...args) => { calls.push(args); return responses.shift()(); };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/turno-inicio.js','utf8'), { document, window, fetch, encodeURIComponent, JSON });
  return { elements, trigger, calls, window, form };
}
const ok = body => () => Promise.resolve({ok:true,json:async()=>body});
const fail = body => () => Promise.resolve({ok:false,status:409,json:async()=>({success:false,...body})});
const tick = () => new Promise(resolve => setImmediate(resolve));
async function open(ctx) { await ctx.trigger.listeners.click(); }
async function submit(ctx) { await ctx.form.listeners.submit({preventDefault(){}}); }

test('recursos congelados usan índices originales y envían solo selección y token', async () => {
  const c = setup([ok({success:true,items:[{indice:3,nombre:'Jabón',unidad:'ml',dosis:50,opciones:[{id:'i',nombre:'Uno',unidad:'l'}]}]}),ok({success:true,lavadoId:'lav 1'})]);
  await open(c); assert.match(c.calls[0][0], /id=t/);
  const select = c.elements['turno-inicio-items'].children[0].children.find(x => x.tag === 'select');
  assert.ok(select.required); select.value = 'i'; await submit(c);
  assert.equal(c.calls[1][1].headers.RequestVerificationToken,'csrf');
  assert.deepEqual(JSON.parse(c.calls[1][1].body), {turnoId:'t',seleccion:{3:'i'}});
  assert.equal(c.window.location.href,'/Lavados/Detalle/lav%201');
});
test('sin opciones y errores de consulta o inicio no envían selección inválida', async () => {
  const c = setup([ok({success:true,items:[{indice:1,nombre:'X',unidad:'ml',dosis:1,opciones:[]}]}),fail({message:'No disponible'})]);
  await open(c); await submit(c); assert.equal(c.calls.length,1); assert.ok(c.elements['turno-inicio-error'].textContent);
  const d = setup([fail({message:'No disponible'})]); await open(d); await submit(d); assert.equal(d.calls.length,1); assert.equal(d.elements['turno-inicio-error'].textContent,'No disponible');
  const e = setup([ok({success:true,items:[]}),fail({message:'Conflicto'})]); await open(e); await submit(e); assert.equal(e.elements['turno-inicio-error'].textContent,'Conflicto'); assert.equal(e.elements['turno-inicio-submit'].disabled,false);
});
test('evita doble envío mientras el inicio está pendiente', async () => {
  let resolve; const c = setup([ok({success:true,items:[]}),() => new Promise(r => {resolve=r;})]);
  await open(c); const first = submit(c); await tick(); await submit(c); assert.equal(c.calls.length,2); assert.equal(c.elements['turno-inicio-submit'].disabled,true);
  resolve({ok:false,json:async()=>({message:'Error'})}); await first; assert.equal(c.elements['turno-inicio-submit'].disabled,false);
});
test('resultado incierto advierte verificar estado y reintenta exactamente la misma solicitud', async () => {
  const ambiguous = [
    () => Promise.resolve({ok:false,status:503,json:async()=>({success:false,message:'No se pudo iniciar el turno.'})}),
    () => Promise.reject(new Error('network')),
    () => Promise.resolve({ok:true,status:200,json:async()=>{throw new Error('invalid json');}}),
    () => Promise.resolve({ok:true,status:200,json:async()=>({success:true})})
  ];
  for (const response of ambiguous) {
    const c = setup([ok({success:true,items:[{indice:3,nombre:'X',unidad:'ml',dosis:1,opciones:[{id:'a',nombre:'A',unidad:'ml'},{id:'b',nombre:'B',unidad:'ml'}]}]}), response, fail({message:'Conflicto'}), ok({success:true,lavadoId:'l'})]);
    await open(c); const select = c.elements['turno-inicio-items'].children[0].children.find(x => x.tag === 'select'); select.value = 'a';
    await submit(c); assert.match(c.elements['turno-inicio-error'].textContent, /verifique el estado/i); assert.equal(c.window.location.href, '');
    select.value = 'b'; await submit(c); assert.equal(c.calls[1][1].body,c.calls[2][1].body); assert.match(c.elements['turno-inicio-error'].textContent, /verifique el estado/i);
    await submit(c); assert.equal(c.calls[1][1].body,c.calls[3][1].body); assert.equal(c.window.location.href,'/Lavados/Detalle/l');
  }
});
test('apertura repetida no reinicia la consulta ni mueve el foco', async () => {
  let resolve; const c = setup([() => new Promise(r => { resolve = r; })]);
  const first = open(c); await tick(); await open(c);
  assert.equal(c.calls.length, 1); assert.equal(c.elements['turno-inicio-dialog'].open, true);
  assert.equal(c.trigger.focused, undefined);
  resolve({ok:true,json:async()=>({success:true,items:[]})}); await first;
});
test('cerrar y reabrir descarta respuestas antiguas exitosas o fallidas', async () => {
  for (const stale of [{ok:true,json:async()=>({success:true,items:[{indice:9,nombre:'Viejo',unidad:'ml',dosis:1,opciones:[]}]})}, {ok:false,json:async()=>({message:'Error viejo'})}]) {
    let resolve; const c = setup([() => new Promise(r => { resolve = r; }), ok({success:true,items:[{indice:2,nombre:'Nuevo',unidad:'ml',dosis:1,opciones:[{id:'nuevo',nombre:'Nuevo',unidad:'ml'}]}]}), ok({success:true,lavadoId:'l'})]);
    const first = open(c); await tick(); c.elements['turno-inicio-cancel'].listeners.click();
    assert.equal(c.elements['turno-inicio-dialog'].open, false);
    await open(c); resolve(stale); await first;
    assert.equal(c.elements['turno-inicio-error'].textContent, '');
    assert.equal(c.elements['turno-inicio-items'].children.length, 1);
    const select = c.elements['turno-inicio-items'].children[0].children.find(x => x.tag === 'select');
    assert.equal(select.name, 'seleccion-2'); select.value = 'nuevo'; await submit(c);
    assert.deepEqual(JSON.parse(c.calls[2][1].body), {turnoId:'t',seleccion:{2:'nuevo'}});
  }
});
test('boton deshabilitado no abre dialogo ni consulta recursos', async () => {
  const c = setup([ok({success:true,items:[]})]);
  c.trigger.disabled = true;
  await open(c);
  assert.equal(c.calls.length, 0);
  assert.equal(c.elements['turno-inicio-dialog'].open, undefined);
});
test('vista conserva detalle y exige los tres permisos y estado pendiente', () => {
  const view = fs.readFileSync('Views/Turnos/Detalle.cshtml','utf8');
  for (const s of ['Turnos.Modificar','Lavados.Crear','Lavados.Modificar','Pendiente','Información General','Volver a turnos','_TurnoInicio']) assert.ok(view.includes(s), s);
  const partial = fs.readFileSync('Views/Turnos/_TurnoInicio.cshtml','utf8');
  for (const s of ['AntiForgeryToken','<noscript>','<dialog','method="post"','aria-live']) assert.ok(partial.includes(s), s);
});
