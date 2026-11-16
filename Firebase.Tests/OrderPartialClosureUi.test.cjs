'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const view = readFileSync('Views/Insumos/Ordenes.cshtml', 'utf8');

test('el progreso parcial muestra cierre terminal sin afirmar etapas previas ni recepción completa', () => {
  assert.match(view, /else if \(o\.Estado == "RecibidaParcial"\) \{ <p>Estado actual: Recibida parcialmente \(orden cerrada\)<\/p> \}\s*else \{ <p>Estado actual: @o\.Estado<\/p> \}/);
  assert.match(view, /etapas = new\[\] \{ "Enviada", "Confirmada", "Despachada", "Recibida" \}/);
});

test('el cierre parcial muestra diferencia y contacto y no habilita acciones de recepción ni cancelación', () => {
  assert.match(view, /o\.Estado == "RecibidaParcial" \? "Recibida parcialmente"/);
  assert.match(view, /@if \(o\.Estado == "RecibidaParcial"\)[\s\S]*?item\.CantidadRecibida[\s\S]*?item\.Cantidad[\s\S]*?item\.Cantidad - item\.CantidadRecibida[\s\S]*?contactar al proveedor/i);
  assert.match(view, /User\.TienePermiso\("Insumos\.Recepcionar"\) && \(o\.Estado == "Enviada" \|\| o\.Estado == "Confirmada" \|\| o\.Estado == "Despachada"\)/);
  assert.match(view, /o\.Estado != "Recibida" && o\.Estado != "RecibidaParcial" && o\.Estado != "Cancelada"/);
});
