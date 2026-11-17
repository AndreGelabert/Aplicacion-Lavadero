'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

test('solicitud permite fecha y hora preferidas opcionales con mínimos', () => {
  const view = fs.readFileSync('Views/Insumos/Cotizaciones.cshtml', 'utf8');
  assert.match(view, /name="fechaEntregaPreferida"/i);
  assert.match(view, /name="horaEntregaPreferida"/i);
  assert.match(view, /type="date"[^>]*min=|min=[^>]*type="date"/i);
  assert.match(view, /preferida/i);
});

test('mínimo de hora se actualiza con zona argentina y tras limpiar', () => {
  const script = fs.readFileSync('wwwroot/js/insumos-secondary.js', 'utf8');
  assert.match(script, /America\/Argentina\/Buenos_Aires/);
  assert.match(script, /horaPreferida\.min\s*=/);
  assert.match(script, /fechaPreferida\?\.addEventListener\('change'/);
  assert.match(script, /form\.reset\(\);\s*actualizarMinimoEntrega\(\)/);
});

test('ayuda activa distingue marcadores para solicitud y orden', () => {
  const view = fs.readFileSync('Views/Configuracion/Index.cshtml', 'utf8');
  assert.match(view, /preferida/i);
  assert.match(view, /solo[^<.]*orden/i);
});
