const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const view = readFileSync('Views/Insumos/Ordenes.cshtml', 'utf8');

test('el detalle limita la recepción antes del despacho y explica el bloqueo', () => {
  assert.match(view, /disabled="@\(o\.Estado != "Despachada"\)"/);
  assert.match(view, /Para registrar la recepción, primero registre el despacho del proveedor\./);
  assert.match(view, /name="cantidades"[^>]*disabled="@\(o\.Estado != "Despachada"\)"/);
  assert.match(view, /<button[^>]*disabled="@\(o\.Estado != "Despachada"\)"[^>]*>/);
});

test('los atributos numéricos de recepción usan formato decimal invariante', () => {
  const form = view.match(/<form\b[^>]*asp-action="RecibirOrden"[^>]*>[\s\S]*?<\/form>/)[0];
  const input = form.match(/<input name="cantidades"[^>]*\/>/)[0];
  const expression = '@((item.Cantidad - item.CantidadRecibida).ToString(System.Globalization.CultureInfo.InvariantCulture))';
  assert.ok(input.includes(`max="${expression}"`));
  assert.ok(input.includes(`value="${expression}"`));
  assert.match(input, /min="0"/);
  assert.match(input, /step="0\.0001"/);
});

test('el detalle muestra progreso real, permite cerrar con X y conserva cancelar orden', () => {
  assert.match(view, /w-full max-w-\w+ max-h-full overflow-y-auto/);
  assert.match(view, /aria-label="Progreso de la orden"/);
  for (const state of ['PendienteAprobacion', 'Enviada', 'Confirmada', 'Despachada', 'Recibida', 'Cancelada', 'RevisionManual']) assert.ok(view.includes(`"${state}"`), state);
  assert.match(view, /aria-current="step"/);
  assert.match(view, /data-close-modal[^>]*aria-label="Cerrar"/);
  assert.match(view, />Cancelar orden<\/button>/);
  assert.doesNotMatch(view, />Cancelar<\/button>/);
  assert.match(view, /data-popover-placement="bottom"/);
});

test('la vista conserva los contratos de acciones, permisos y recepción', () => {
  assert.match(view, /asp-action="Index"/);
  assert.match(view, /asp-action="Cotizaciones"/);
  for (const action of ['AprobarOrden', 'EditarOrdenPendiente', 'CambiarEstadoOrden', 'RecibirOrden']) {
    const forms = [...view.matchAll(new RegExp(`<form\\b[^>]*asp-action="${action}"[^>]*>[\\s\\S]*?<\\/form>`, 'g'))];
    assert.ok(forms.length, action);
    for (const [form] of forms) {
      assert.match(form, /method="post"/);
      assert.match(form, /@Html\.AntiForgeryToken\(\)/);
      assert.match(form, /name="id" value="@o\.Id"/);
    }
  }
  for (const permission of ['Insumos.Crear', 'Insumos.Modificar', 'Insumos.Recepcionar']) assert.ok(view.includes(`User.TienePermiso("${permission}")`));
  for (const name of ['insumoId', 'cantidad', 'precio', 'estado', 'motivo', 'insumoIds', 'cantidades', 'cancelarConcurrentes']) assert.ok(view.includes(`name="${name}"`), name);
  assert.match(view, /o\.Estado == "Enviada" \|\| o\.Estado == "Confirmada" \|\| o\.Estado == "Despachada"/);
  assert.match(view, /etapas = new\[\] \{ "Enviada", "Confirmada", "Despachada", "Recibida" \}/);
  assert.match(view, /else \{ <p>Estado actual: @o\.Estado<\/p> \}/);
});
