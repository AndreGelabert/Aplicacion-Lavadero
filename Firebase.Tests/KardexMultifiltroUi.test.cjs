'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const view = fs.readFileSync(path.join(__dirname, '../Views/Insumos/Kardex.cshtml'), 'utf8');

test('Kardex checkboxes allow more than one selection per group', () => {
  for (const name of ['insumoId', 'tipo']) {
    const input = view.match(new RegExp('<input type="checkbox" name="' + name + '"[^>]*>'));
    assert.ok(input, name);
    assert.doesNotMatch(input[0], /onchange\s*=/);
  }
});

test('Kardex retains GET apply, reset, dates, sorting and local search', () => {
  assert.match(view, /form method="get" asp-action="Kardex"/);
  assert.match(view, /Aplicar Filtros/);
  assert.match(view, /asp-action="Kardex"[^>]*>Limpiar filtros/);
  for (const name of ['desde', 'hasta', 'sortOrder']) assert.match(view, new RegExp('name="' + name + '"'));
  assert.match(view, /id="kardex-search"/);
});
