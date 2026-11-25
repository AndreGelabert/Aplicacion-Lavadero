const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
test('edición desde tabla y detalle utiliza el formulario principal y conserva la selección acordada', () => {
  const index = fs.readFileSync('Views/Turnos/Index.cshtml', 'utf8');
  const table = fs.readFileSync('Views/Turnos/_TurnoTable.cshtml', 'utf8');
  const detail = fs.readFileSync('Views/Turnos/Detalle.cshtml', 'utf8');
  const js = fs.readFileSync('wwwroot/js/turnos.js', 'utf8');
  assert.match(index, /puedeCrear \|\| puedeEditar/);
  assert.match(table, /data-turno-editar/);
  assert.match(detail, /asp-route-editar="@Model.Id"/);
  assert.match(js, /DatosEdicion/);
  assert.match(js, /EditarToken/);
  assert.match(js, /VersionEsperada/);
  assert.match(js, /CuponId/);
  assert.match(js, /delete\('Servicios/);
});

const { setup, response, submit } = require('./TurnoReservaSubmit.test.cjs');
const tick = () => new Promise(resolve => setImmediate(resolve));
const data = { id:'t', versionEsperada:'version', inicioUtc:'2030-01-01T15:00:00Z', aceptaAdelanto:false, clienteId:'c', clienteTrajoId:'c', clienteRetiraId:'c', notas:'nota', descuento:0, precioAcordado:100, vehiculos:[{vehiculoId:'v', patente:'ABC', tipoVehiculo:'auto', servicios:[{servicioId:'s'}]}] };

test('fallo de catálogo cancela inicialización de edición', async () => {
  const c = setup(() => response({success:true}), null, 'id', null, null, {data, catalogueFails:true, token:() => response({identity:'edit'})});
  await tick(); await tick();
  assert.equal(c.el.turnoMensaje.textContent, 'No se pudieron cargar los datos de edición.');
  assert.notEqual(c.el['form-title'].textContent, 'Editando un Turno');
  assert.equal(c.calls.length, 0);
});

test('fallo de token antes del POST permite corregir y reintentar', async () => {
  let attempt = 0;
  const c = setup(() => response({success:true}), null, 'id', null, null, {data, token:() => ++attempt === 1 ? Promise.reject(new Error('offline')) : response({identity:'edit'})});
  await tick(); await tick();
  await submit(c);
  assert.equal(c.calls.length, 0);
  assert.equal(c.el.Fecha.disabled, false);
  assert.equal(c.button.disabled, false);
  assert.equal(c.el.turnoMensaje.textContent, 'No se pudo preparar la solicitud. Intente nuevamente.');
  await submit(c);
  assert.equal(c.calls.length, 1);
  assert.equal(c.calls[0].url, '/Turnos/Editar');
});
