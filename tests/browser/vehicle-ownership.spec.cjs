const path = require('node:path');
const { test, expect } = require('@playwright/test');

async function setup(page, selected) {
  await page.goto('/fixture');
  await page.setContent(`<span id="form-title">Editando Cliente</span><div id="accordion-flush-body-1"></div><input id="Id" value="owner"><input id="VehiculosIdsData" value="${selected}"><div id="vehiculos-seleccionados-container"><div id="vehiculos-seleccionados-list"></div></div><input id="vehiculo-search"><div id="vehiculo-dropdown"><div id="vehiculo-dropdown-content"></div></div>`);
  await page.evaluate(() => {
    window.messages = [];
    window.Notificaciones = { mostrar: (text, type) => window.messages.push({ text, type }) };
    window.Peticiones = { enviar: async () => ({ json: async () => ({ success: true, vehiculos: [
      { id: 'shared', patente: 'AB123CD', marca: 'Toyota', modelo: 'Corolla', color: 'Blanco', tipoVehiculo: 'Auto', esTitular: true, tieneOtrosAsociados: true },
      { id: 'secondary', patente: 'AB124CD', marca: 'Toyota', modelo: 'Corolla', color: 'Blanco', tipoVehiculo: 'Auto', esTitular: false, tieneOtrosAsociados: true },
      { id: 'sole', patente: 'AB125CD', marca: 'Toyota', modelo: 'Corolla', color: 'Blanco', tipoVehiculo: 'Auto', esTitular: true, tieneOtrosAsociados: false }
    ] }) }) };
  });
  await page.addScriptTag({ path: path.resolve(__dirname, '../../wwwroot/js/cliente.js') });
  await page.evaluate(() => window.PageModules.clientes.init());
}

test('duplicate owned plate offers association and validates the key', async ({ page }) => {
  await setup(page, 'sole');
  await page.evaluate(() => {
    window.Peticiones.enviar = async url => {
      if (url.startsWith('/Vehiculo/FormPartialCliente')) return { text: async () => '<form id="vehiculo-form"><input id="Patente" value="AB123CD"><input id="Marca" value="Toyota"><input id="Modelo" value="Corolla"><input id="Color" value="Blanco"><input id="TipoVehiculo" value="Auto"></form>' };
      if (url.startsWith('/Vehiculo/VerificarVehiculoSinDueno')) return { json: async () => ({ existe: false, asociable: true }) };
      if (url.startsWith('/Vehiculo/GetVehiculosParaAsociacion')) return { json: async () => [] };
      if (url.startsWith('/Vehiculo/ValidarClaveAsociacion')) return { json: async () => ({ valida: false, error: 'Clave inválida' }) };
      return { json: async () => [] };
    };
  });
  await page.evaluate(() => openQuickCreateVehiculoModal());
  await page.locator('#vehiculo-form').evaluate(form => form.requestSubmit());
  await expect(page.locator('#modo-asociacion-content')).toBeVisible();
  await expect(page.locator('#asociacion-patente')).toHaveValue('AB123CD');
  await page.locator('#clave-asociacion').fill('WRNG-KEY1');
  await page.locator('button[onclick="validarClaveAsociacion()"]').click();
  await expect(page.locator('#validacion-clave-resultado')).toContainText('Clave inválida');
  await expect(page.locator('[data-vehiculo-id="shared"]')).toHaveCount(0);
});

test('valid association key is sent in the actual client form payload', async ({ page }) => {
  await setup(page, 'sole');
  await page.evaluate(() => {
    const form = document.createElement('form');
    form.id = 'client-submit'; form.action = '/Cliente/ActualizarClienteAjax';
    form.innerHTML = '<button type="submit">Guardar cliente</button>';
    form.onsubmit = event => submitClienteAjax(form, event);
    document.body.appendChild(form);
    window.Peticiones.enviar = async (url, options) => {
      if (url.startsWith('/Vehiculo/FormPartialCliente')) return { text: async () => '<form id="vehiculo-form"><input id="Patente" value="AB123CD"><input id="Marca" value="Toyota"><input id="Modelo" value="Corolla"><input id="Color" value="Blanco"><input id="TipoVehiculo" value="Auto"></form>' };
      if (url.startsWith('/Vehiculo/VerificarVehiculoSinDueno')) return { json: async () => ({ existe: false, asociable: true }) };
      if (url.startsWith('/Vehiculo/ValidarClaveAsociacion')) return { json: async () => ({ valida: true, vehiculo: { id: 'shared', patente: 'AB123CD', marca: 'Toyota', modelo: 'Corolla', color: 'Blanco', tipoVehiculo: 'Auto' } }) };
      if (url.includes('/Cliente/ActualizarClienteAjax')) { window.submitted = JSON.parse(options.body.get('VehiculosDataJson')); throw new Error('payload captured'); }
      return { json: async () => [] };
    };
  });
  await page.evaluate(() => openQuickCreateVehiculoModal());
  await page.locator('#vehiculo-form').evaluate(form => form.requestSubmit());
  await expect(page.locator('#asociacion-patente')).toHaveValue('AB123CD');
  await page.locator('#clave-asociacion').fill('ABCD-2345');
  await page.locator('button[onclick="validarClaveAsociacion()"]').click();
  await expect(page.locator('[data-vehiculo-id="shared"]')).toHaveCount(1);
  await page.locator('#client-submit button').click();
  await expect.poll(() => page.evaluate(() => window.submitted)).not.toBeUndefined();
  const payload = await page.evaluate(() => window.submitted.find(v => v.id === 'shared'));
  expect(payload.esAsociacion).toBe(true);
  expect(payload.claveAsociacion).toBe('ABCD-2345');
  expect(payload.esNuevo).toBe(false);
});

test('X blocks shared titular after initial load and reselection, but allows secondary and sole', async ({ page }) => {
  await setup(page, 'shared,secondary,sole');
  const row = id => page.locator(`[data-vehiculo-id="${id}"]`);
  await expect(row('shared')).toHaveCount(1);
  await row('shared').getByRole('button', { name: 'Quitar vehículo' }).click();
  await expect(row('shared')).toHaveCount(1);
  expect(await page.evaluate(() => messages.at(-1))).toEqual({ type: 'error', text: 'Para quitar este vehículo, primero los demás deben desvincularse. Cuando seas el único titular podrás quitarlo y el vehículo se desactivará.' });
  for (const id of ['secondary', 'sole']) {
    await row(id).getByRole('button', { name: 'Quitar vehículo' }).click();
    await expect(row(id)).toHaveCount(0);
    await page.evaluate(id => { selectVehiculoFromDropdown(id); agregarVehiculoSeleccionado(); }, id);
    await expect(row(id)).toHaveCount(1);
    await row(id).getByRole('button', { name: 'Quitar vehículo' }).click();
    await expect(row(id)).toHaveCount(0);
  }
  await setup(page, 'sole');
  await page.evaluate(() => { selectVehiculoFromDropdown('shared'); agregarVehiculoSeleccionado(); });
  await row('shared').getByRole('button', { name: 'Quitar vehículo' }).click();
  await expect(row('shared')).toHaveCount(1);
  expect(await page.evaluate(() => messages.at(-1).text)).toContain('primero los demás deben desvincularse');
});

 test('vehicle eye requests and closes the associated-person summary', async ({ page }) => {
  await page.goto('/fixture');
  await page.setContent(`<button id="eye" onclick="openVehiculoAsociadosModal('car')">Ver personas</button>`);
  await page.evaluate(() => {
    window.Peticiones = { enviar: async url => { window.summaryUrl = url; return { ok: true, text: async () => '<p>Ana Perez — Titular</p><p>Juan Gomez — Asociado</p>' }; } };
  });
  await page.addScriptTag({ path: path.resolve(__dirname, '../../wwwroot/js/vehiculo.js') });
  await page.locator('#eye').click();
  await expect(page.locator('#vehiculo-asociados-modal')).toBeVisible();
  expect(await page.evaluate(() => window.summaryUrl)).toBe('/Vehiculo/AsociadosPartial?id=car');
  await expect(page.locator('#vehiculo-asociados-modal')).toContainText('Juan Gomez');
  await page.locator('#vehiculo-asociados-modal button').first().click();
  await expect(page.locator('#vehiculo-asociados-modal')).toHaveCount(0);
});
