const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');

const cases = {
  Index: ['insumos-ayuda', /stock físico/i, /filtros/i],
  Kardex: ['kardex-help', /esta página/i, /saldo/i],
  Proveedores: ['providers-help', /contacto/i, /estado/i],
  Ordenes: ['orders-help', /detalle/i, /recepción/i],
  Cotizaciones: ['quotations-help', /proveedores/i, /comparación/i],
};
for (const [view, [id, ...content]] of Object.entries(cases)) {
  test(`${view}: ayuda contextual con formato de referencia y apertura inferior`, () => {
    const source = readFileSync(`Views/Insumos/${view}.cshtml`, 'utf8');
    assert.match(source, new RegExp(`data-popover-target="${id}"[^>]*data-popover-trigger="click"[^>]*data-popover-placement="bottom"`));
    const block = source.match(new RegExp(`<div data-popover id="${id}"[\\s\\S]*?<div data-popper-arrow></div></div>`));
    assert.ok(block, 'popover completo');
    assert.match(block[0], /w-64 text-sm text-gray-500/);
    assert.match(block[0], /px-3 py-2 bg-gray-100 border-b border-gray-200 rounded-t-lg dark:border-gray-600 dark:bg-gray-700/);
    assert.match(block[0], /<h3 class="font-semibold text-gray-900 dark:text-white">Ayuda<\/h3>/);
    assert.match(block[0], /<div class="px-3 py-2"><p>/);
    for (const pattern of content) assert.match(block[0], pattern);
  });
}
