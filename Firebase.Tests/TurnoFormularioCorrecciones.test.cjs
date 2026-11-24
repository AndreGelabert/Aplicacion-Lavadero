const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

test('el formulario muestra botones completos y modal con pie separado como Lavados', () => {
  const html = fs.readFileSync('Views/Turnos/_TurnoForm.cshtml', 'utf8');
  assert.match(html, /flex flex-wrap justify-end m-4 gap-2/);
  assert.match(html, /<\/form>\s*<div class="flex flex-wrap justify-end gap-2 border-t p-4 dark:border-gray-600">/);
});
