const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const forms = {
  Cliente: ['submitClienteAjax(this, event)', 'loadClienteForm(null)'],
  Rol: ['submitRolAjax(this, event)', 'cancelRolForm()'],
  Vehiculo: ['submitVehiculoAjax(this, event)', 'cancelarEdicionVehiculo()'],
  PaqueteServicio: ['submitPaqueteAjax(this, event)', 'cancelarEdicionPaquete()'],
  Servicio: ['submitServicioAjax(this)', 'loadServicioForm(null)'],
  Lavados: ['id="lavado-form"', 'limpiarFormularioLavado()'],
};
for (const [name, hooks] of Object.entries(forms)) {
  test(`${name} conserva acciones y usa variantes comunes`, () => {
    const file = `Views/${name}/_${name === 'Lavados' ? 'Lavado' : name}Form.cshtml`;
    const source = fs.readFileSync(file, 'utf8');
    for (const hook of hooks) assert.ok(source.includes(hook), hook);
    assert.match(source, /type="submit"[^>]*class="app-button app-button--success"/s);
    assert.match(source, /type="button"[^>]*class="app-button app-button--danger"/s);
  });
}
test('configuración conserva envío y navegación con variantes', () => {
  const source = fs.readFileSync('Views/Configuracion/Index.cshtml', 'utf8');
  assert.ok(source.includes('asp-action="Actualizar"'));
  assert.ok(source.includes('@Url.Action("Index", "Lavados")'));
  assert.match(source, /class="app-button app-button--secondary"/);
  assert.match(source, /class="app-button app-button--primary"/);
});
// Business attributes captured from b70473d; classes are deliberately excluded.
const contracts = {
  Cliente: ['id="cliente-form" data-validate="true" data-edit="@edit" method="post" action="@formAction" onsubmit="return submitClienteAjax(this, event);"', 'type="submit" id="submit-button"', `type="button" id="clear-button" onclick="@(edit ? "loadClienteForm(null)" : "document.getElementById('cliente-form').reset()")"`],
  Rol: ['id="rol-form" data-edit="@(edit ? "true" : "false")" method="post" action="@formAction" onsubmit="return submitRolAjax(this, event);"', 'type="submit"', 'type="button" onclick="cancelRolForm()"'],
  Vehiculo: ['id="vehiculo-form" data-validate="true" data-edit="@edit.ToString().ToLower()" data-color="@(Model?.Color ?? "")" method="post" action="@formAction" onsubmit="return submitVehiculoAjax(this, event);"', 'type="submit" id="submit-button"', `type="button" id="clear-button" onclick="@(edit ? "cancelarEdicionVehiculo()" : "document.getElementById('vehiculo-form').reset()")"`],
  PaqueteServicio: ['id="paquete-form" data-validate="true" data-edit="@edit" method="post" action="@formAction" onsubmit="return submitPaqueteAjax(this, event);"', 'type="submit" id="submit-button"', 'type="button" onclick="@(Model == null ? "limpiarFormularioPaquete()" : "cancelarEdicionPaquete()")"'],
  Servicio: ['id="servicio-form" data-validate="true" data-edit="@edit" method="post" action="@formAction" onsubmit="return submitServicioAjax(this);"', 'type="submit" id="submit-button"', `type="button" id="clear-button" onclick="@(edit ? "loadServicioForm(null)" : "document.getElementById('servicio-form').reset()")"`],
  Lavados: ['id="lavado-form" data-validate="true" data-edit="@edit"', 'type="submit" id="submit-button" disabled', 'type="button" id="clear-button" onclick="limpiarFormularioLavado()"'],
};
function business(tag) { return tag.replace(/^<\w+\s*/, '').replace(/\s*class="[^"]*"/g, '').replace(/>$/, '').replace(/\s+/g, ' ').trim(); }
for (const [name, expected] of Object.entries(contracts)) {
  test(`${name} conserva atributos de formulario y ambos controles`, () => {
    const source = fs.readFileSync(`Views/${name}/_${name === 'Lavados' ? 'Lavado' : name}Form.cshtml`, 'utf8');
    const form = source.match(/<form\b[^>]*>/)[0];
    const actions = [...source.matchAll(/<button\b[^>]*class="app-button app-button--(?:success|danger)"[^>]*>/g)].map(m => m[0]);
    assert.deepEqual([form, ...actions].map(business), expected);
  });
}
test('configuración conserva atributos y permisos de acciones', () => {
  const source = fs.readFileSync('Views/Configuracion/Index.cshtml', 'utf8');
  const form = source.match(/<form\b[^>]*>/)[0];
  const actions = [...source.matchAll(/<(?:a|button)\b[^>]*class="app-button app-button--(?:primary|secondary)"[^>]*>/g)].map(m => m[0]);
  assert.deepEqual([form, ...actions].map(business), ['id="configuracion-form" data-validate="true" asp-action="Actualizar" method="post"', 'href="@Url.Action("Index", "Lavados")"', 'type="submit"']);
  assert.match(source, /@if \(puedeConsultarLavados\)\s*\{\s*<a/);
  assert.match(source, /@if \(puedeModificar\)\s*\{\s*<button type="submit"/);
});
test('celdas vacías no añaden padding exterior', () => {
  for (const name of ['Auditoria', 'Cliente', 'Personal', 'Lavados', 'Rol', 'PaqueteServicio', 'Vehiculo', 'Servicio']) {
    const file = `Views/${name}/_${name === 'Lavados' ? 'Lavado' : name}Table.cshtml`;
    const source = fs.readFileSync(file, 'utf8');
    assert.match(source, /<td colspan="\d+" class="p-0">\s*@await Html.PartialAsync\("_TableEmptyState"/);
  }
});
