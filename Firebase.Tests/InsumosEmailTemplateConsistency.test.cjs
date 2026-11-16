const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const read = path => fs.readFileSync(path, 'utf8');

test('formularios identifican plantillas separadas y protegen el envío', () => {
  const view = read('Views/Configuracion/Index.cshtml');
  for (const name of ['PlantillaEmail', 'AsuntoCotizacion', 'CuerpoCotizacion', 'AsuntoOrden', 'CuerpoOrden']) assert.ok(view.includes(`name="${name}"`));
  assert.ok(view.includes('@Html.AntiForgeryToken()'));
  assert.ok(view.includes('{contacto_proveedor}'));
});

test('rutas manual y automática comparten formateador', () => {
  const controller = read('Controllers/InsumosController.cs');
  const service = read('Services/InsumosService.cs');
  assert.equal((controller.match(/InsumosEmailFormatter\.FormatearSolicitud/g) || []).length, 2);
  assert.equal((service.match(/InsumosEmailFormatter\.FormatearSolicitud/g) || []).length, 1);
  assert.equal((service.match(/InsumosEmailFormatter\.FormatearOrden/g) || []).length, 1);
  assert.ok(service.includes('"RevisionManual"'));
});
