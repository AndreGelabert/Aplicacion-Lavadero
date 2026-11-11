const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

test('el fondo cubre al menos el viewport sin limitar el scroll', () => {
  const layout = fs.readFileSync('Views/Shared/_Layout.cshtml', 'utf8');
  assert.match(layout, /class="antialiased min-h-screen bg-gray-50 dark:bg-gray-900"/);
});

test('el formulario tiene campos directos en una grilla adaptable y vehículos de ancho completo', () => {
  const form = fs.readFileSync('Views/Cliente/_ClienteForm.cshtml', 'utf8');
  assert.match(form, /class="grid gap-4 m-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-3"/);
  assert.doesNotMatch(form, /class="grid grid-cols-2"|class="col-span-[36]"/);
  assert.match(form, /class="col-span-full"/);
  assert.match(form, /id="add-vehiculo-btn"\s+class="w-full/);
  for (const id of ['TipoDocumento', 'NumeroDocumento', 'Nombre', 'Apellido', 'Telefono', 'Email', 'submit-button', 'clear-button']) assert.ok(form.includes(`id="${id}"`));
  assert.match(form, /@if \(puedeCrearTipoDocumento\)/);
  assert.match(form, /@if \(puedeEliminarTipoDocumento\)/);
  assert.match(form, /@if \(puedeGestionarVehiculos\)/);
});
