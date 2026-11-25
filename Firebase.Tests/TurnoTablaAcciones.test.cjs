const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

test('acciones de tabla requieren permisos y estado pendiente y conservan el detalle', () => {
  const table = fs.readFileSync('Views/Turnos/_TurnoTable.cshtml', 'utf8');
  assert.match(table, /turno\.Estado == "Pendiente" && User\.TienePermiso\("Turnos\.Consultar"\) && User\.TienePermiso\("Turnos\.Modificar"\)/);
  assert.match(table, /turno\.Estado == "Pendiente" && User\.TienePermiso\("Turnos\.Consultar"\) && User\.TienePermiso\("Turnos\.Cancelar"\)/);
  assert.match(table, /title="Editar turno"[^>]*|[^>]*title="Editar turno"/);
  assert.match(table, /data-turno-cancelacion[^>]*data-turno-id="@turno.Id"/);
  assert.match(table, /title="Ver detalle"/);
  assert.match(table, /asp-route-sortBy/);
  const index = fs.readFileSync('Views/Turnos/Index.cshtml', 'utf8');
  assert.match(index, /PartialAsync\("_TurnoCancelacion"/);
  assert.match(index, /turno-cancelacion\.js/);
});

test('selecciona el turno del botón y conserva el id del detalle', () => {
  const listeners = {};
  const input = {value:'detalle'};
  const dialog = {open:false,addEventListener(){},showModal(){this.open=true;},close(){this.open=false;}};
  const form = {querySelector:()=>input,addEventListener(){}};
  const error = {textContent:''};
  const button = {dataset:{turnoId:'fila 2'},addEventListener:(name,fn)=>listeners[name]=fn};
  const elements = {'turno-cancelacion-dialog':dialog,'turno-cancelacion-form':form,'turno-cancelacion-error':error,'turno-cancelacion-submit':{},'turno-cancelacion-cancel':{addEventListener(){}}};
  const document = {getElementById:id=>elements[id],querySelectorAll:()=>[button]};
  vm.runInNewContext(fs.readFileSync('wwwroot/js/turno-cancelacion.js','utf8'),{document});
  listeners.click(); assert.equal(input.value,'fila 2');
  dialog.open=false; button.dataset={}; input.value='detalle'; listeners.click(); assert.equal(input.value,'detalle');
});
