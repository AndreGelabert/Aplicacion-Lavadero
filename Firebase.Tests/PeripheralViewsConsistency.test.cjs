'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const read = p => fs.readFileSync(p, 'utf8');
const home = read('Views/Home/AccessDenied.cshtml');
const catalogue = read('Views/VehicleCatalogueReview/Index.cshtml');
const config = read('Views/Configuracion/Index.cshtml');
const css = read('wwwroot/css/app.css');

test('shared button variants have their expected color families', () => {
  for (const [variant, color] of Object.entries({primary:'blue', secondary:'gray', success:'green'}))
    assert.match(css, new RegExp(`\\.app-button--${variant}\\s*\\{[^}]*${color}`));
});
test('access denied preserves conditional navigation and logout post', () => {
  assert.match(home, /@if \(tieneModulo\)\s*\{\s*<a href="\/"\s+class="app-button app-button--primary"/);
  assert.match(home, /<form data-session-logout asp-controller="Login" asp-action="Logout" method="post">\s*<button type="submit"\s+class="app-button app-button--secondary"/);
});
test('catalogue keeps exact resolution inputs and decision meanings', () => {
  assert.match(catalogue, /<form asp-action="Resolve" method="post" class="mt-4 space-y-3">\s*@Html.AntiForgeryToken\(\)\s*<input type="hidden" name="id" value="@proposal.VehiculoId"/);
  assert.match(catalogue, /<input name="brand" value="@proposal.Marca" maxlength="100"/);
  assert.match(catalogue, /<input name="model" value="@proposal.Modelo" maxlength="100"/);
  for (const [decision, variant] of Object.entries({link:'primary', approve:'success', discard:'secondary'}))
    assert.match(catalogue, new RegExp(`<button name="decision" value="${decision}" class="app-button app-button--${variant}">`));
  assert.match(catalogue, /@if \(Model.Count == 0\)[\s\S]*?No hay datos pendientes de revisión\./);
  assert.doesNotMatch(catalogue, /_TableEmptyState/);
});
test('schedule footer keeps actual public handlers and standard actions', () => {
  assert.match(config, /<button type="button" onclick="cerrarModalHorarios\(\)" class="app-button app-button--secondary">/);
  assert.match(config, /<button type="button" onclick="guardarHorarios\(\)" class="app-button app-button--primary">/);
  assert.match(config, /<form id="configuracion-form" data-validate="true" asp-action="Actualizar" method="post"[^>]*>\s*@Html.AntiForgeryToken\(\)/);
  assert.match(config, /@if \(puedeModificar\)/);
});
