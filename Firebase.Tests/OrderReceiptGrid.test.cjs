const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const view = fs.readFileSync('Views/Insumos/Ordenes.cshtml', 'utf8');
const css = fs.readFileSync('wwwroot/css/app.min.css', 'utf8');
const form = view.match(/<form\b[^>]*asp-action="RecibirOrden"[^>]*>[\s\S]*?<\/form>/)[0];

test('receipt items use a compiled responsive grid with cohesive item fields and separate actions', () => {
  assert.match(form, /<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">\s*@foreach \(var item in o\.Items\) \{ <div>\s*<input type="hidden" name="insumoIds"[\s\S]*?name="cantidades"[\s\S]*?cantidad pendiente\.<\/p><\/div> \}\s*<\/div>\s*@if \(!string\.IsNullOrWhiteSpace\(o\.GrupoCotizacionId\)\)/);
  for (const selector of ['.grid-cols-1', '.md\\:grid-cols-2', '.lg\\:grid-cols-3']) assert.ok(css.includes(selector), selector);
});

test('receipt preserves ordering, permissions, antiforgery and numeric and disabled contracts', () => {
  assert.match(view, /User\.TienePermiso\("Insumos\.Recepcionar"\) && \(o\.Estado == "Enviada" \|\| o\.Estado == "Confirmada" \|\| o\.Estado == "Despachada"\)/);
  assert.match(form, /method="post"/);
  assert.match(form, /@Html\.AntiForgeryToken\(\)/);
  assert.match(form, /<fieldset disabled="@\(o\.Estado != "Despachada"\)"/);
  assert.match(form, /name="insumoIds" value="@item\.InsumoId"[\s\S]*?Cantidad recibida de @item\.NombreInsumo \(pend\. @\(item\.Cantidad - item\.CantidadRecibida\)\)[\s\S]*?name="cantidades"[^>]*disabled="@\(o\.Estado != "Despachada"\)"[^>]*required min="0"[^>]*step="0\.0001"/);
  const invariant = '@((item.Cantidad - item.CantidadRecibida).ToString(System.Globalization.CultureInfo.InvariantCulture))';
  assert.ok(form.includes(`max="${invariant}"`));
  assert.ok(form.includes(`value="${invariant}"`));
  assert.match(form, /name="cancelarConcurrentes"/);
  assert.match(form, /<button disabled="@\(o\.Estado != "Despachada"\)"/);
});
