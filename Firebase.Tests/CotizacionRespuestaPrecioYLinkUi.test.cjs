const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

test('Modal de respuesta en Cotizaciones muestra cantidad, unidad, selector Unitario/Final y vista previa', () => {
  const view = fs.readFileSync('Views/Insumos/Cotizaciones.cshtml', 'utf8');
  assert.match(view, /data-quote-response-form/);
  assert.match(view, /data-quote-item\s+data-quantity="/);
  assert.match(view, /Cantidad solicitada:/);
  assert.match(view, /placeholder="0\.00"[^>]*name="Items\[@i\]\.PrecioUnitario"/);
  assert.match(view, /name="Items\[@i\]\.TipoPrecio"[^>]*data-quote-price-mode/);
  assert.match(view, /<option value="Unitario" selected>Precio unitario<\/option>/);
  assert.match(view, /<option value="Final">Precio final \(total del ítem\)<\/option>/);
  assert.match(view, /data-quote-preview/);
  assert.match(view, /data-quote-total/);

  const js = fs.readFileSync('wwwroot/js/insumos-secondary.js', 'utf8');
  assert.match(js, /\[data-quote-response-form\]/);
  assert.match(js, /Precio unitario equivalente:/);
  assert.match(js, /Subtotal estimado/);
  assert.match(js, /Total estimado:/);
});
