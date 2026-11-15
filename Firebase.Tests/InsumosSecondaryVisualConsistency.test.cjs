const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const read = path => readFileSync(path, 'utf8');

test('la configuración de reabastecimiento vive en configuración general', () => {
  const general = read('Views/Configuracion/Index.cshtml');
  assert.match(general, /name="ModoEmision"/);
  assert.match(general, /name="PlantillaEmail"/);
  assert.match(general, /GuardarConfiguracion/);
  assert.doesNotMatch(read('Views/Insumos/Index.cshtml'), /asp-action="Configuracion"/);
});

test('las vistas secundarias usan iconos y estilos de botones existentes', () => {
  for (const view of ['Cotizaciones', 'Proveedores', 'Ordenes', 'Kardex']) {
    const source = read(`Views/Insumos/${view}.cshtml`);
    assert.match(source, /<svg\b/, `${view}: icono inline`);
    assert.match(source, /app-button/, `${view}: botón existente`);
  }
});

test('formularios de proveedores y órdenes están agrupados junto a sus acciones', () => {
  const proveedores = read('Views/Insumos/Proveedores.cshtml');
  const ordenes = read('Views/Insumos/Ordenes.cshtml');
  assert.match(proveedores, /Registrar proveedor/);
  assert.match(proveedores, /aria-label="Tabla de proveedores"/);
  assert.match(ordenes, /aria-label="Tabla de órdenes"/);
  assert.match(ordenes, /Detalle y acciones/);
});

test('Kardex conserva navegación por cursores y filtros', () => {
  const source = read('Views/Insumos/Kardex.cshtml');
  for (const name of ['insumoId', 'tipo', 'desde', 'hasta', 'sortOrder']) assert.match(source, new RegExp(`asp-route-${name}=`));
  assert.match(source, /asp-route-cursor="@ViewBag.PrevCursor"/);
  assert.match(source, /asp-route-cursor="@ViewBag.NextCursor"/);
  assert.doesNotMatch(source, /asp-route-page=/);
});
