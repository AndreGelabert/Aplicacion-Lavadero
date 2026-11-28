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

test('Vista pública de proveedor y su JS usan fragmento, Trusted Types sin innerHTML y selector Unitario/Final', () => {
  const view = fs.readFileSync('Views/CotizacionProveedor/Responder.cshtml', 'utf8');
  assert.match(view, /@\{\s*Layout\s*=\s*null;\s*\}/);
  assert.match(view, /<script nonce="@ViewData\["Nonce"\]" src="\/js\/cotizacion-proveedor\.js" defer><\/script>/);
  assert.match(view, /<style nonce="@ViewData\["Nonce"\]">/);

  const js = fs.readFileSync('wwwroot/js/cotizacion-proveedor.js', 'utf8');
  assert.match(js, /location\.hash\.slice\(1\)/);
  assert.match(js, /history\.replaceState\(null,\s*'',\s*location\.pathname\)/);
  assert.doesNotMatch(js, /location\.search/);
  assert.doesNotMatch(js, /innerHTML/);
  assert.match(js, /credentials:\s*'omit'/);
  assert.match(js, /referrerPolicy:\s*'no-referrer'/);
  assert.match(js, /\/api\/cotizacion-proveedor\/detalle/);
  assert.match(js, /\/api\/cotizacion-proveedor\/responder/);
  assert.match(js, /Precio unitario equivalente:/);
});
