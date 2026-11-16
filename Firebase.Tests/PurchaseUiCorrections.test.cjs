'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const orders = readFileSync('Views/Insumos/Ordenes.cshtml', 'utf8');
const quotes = readFileSync('Views/Insumos/Cotizaciones.cshtml', 'utf8');
const css = readFileSync('wwwroot/css/app.min.css', 'utf8');

test('S2 modal usa ancho mayor disponible en CSS compilado', () => {
  assert.match(orders, /data-order-modal[\s\S]*?w-full max-w-7xl max-h-full/);
  assert.match(css, /\.max-w-7xl\{max-width:80rem\}/);
  assert.match(css, /\.w-full\{width:100%\}/);
});

test('S2 recepción bloqueada agrupa todos los controles y explica el motivo', () => {
  const form = orders.match(/<form\b[^>]*asp-action="RecibirOrden"[^>]*>[\s\S]*?<\/form>/)[0];
  assert.match(form, /<fieldset[^>]*disabled="@\(o\.Estado != "Despachada"\)"[^>]*aria-describedby="@\(o\.Estado != "Despachada" \? \$"recepcion-bloqueada-\{o\.Id\}" : null\)"/);
  assert.match(form, /id="recepcion-bloqueada-@o\.Id"[^>]*bg-yellow-100/);
  assert.match(form, /Para registrar la recepción, primero registre el despacho del proveedor\./);
  assert.match(form, /bg-gray-100 text-gray-500/);
  assert.match(form, /<fieldset[\s\S]*name="cantidades"[\s\S]*name="cancelarConcurrentes"[\s\S]*Registrar recepción[\s\S]*<\/fieldset>/);
  assert.match(css, /\.bg-yellow-100\{/); assert.match(css, /\.bg-gray-100\{/);
});

test('S3 notas ocupa una columna de grilla adaptable', () => {
  assert.match(quotes, /<div class="m-4 grid gap-4 sm:grid-cols-2"><label class="block[^\"]*">Notas <input name="notas"/);
  assert.ok(css.includes('.sm\\:grid-cols-2{grid-template-columns:repeat(2,minmax(0,1fr))}'));
});

test('PRESERVE acciones, progreso y decimal parcial permanecen', () => {
  assert.match(orders, /aria-label="Progreso de la orden"/);
  assert.match(orders, /o\.Estado == "Enviada" \|\| o\.Estado == "Confirmada" \|\| o\.Estado == "Despachada"/);
  assert.equal((orders.match(/\(item\.Cantidad - item\.CantidadRecibida\)\.ToString\(System\.Globalization\.CultureInfo\.InvariantCulture\)/g) || []).length, 2);
  assert.match(quotes, /asp-action="CrearSolicitudes"/);
  assert.match(quotes, /id="quotation-selected"/);
});
