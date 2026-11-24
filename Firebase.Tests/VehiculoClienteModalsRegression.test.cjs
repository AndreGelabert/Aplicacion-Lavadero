'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

test('Views/Vehiculo/Index.cshtml renders accordion-flush and vehiculo-form-container when puedeModificar is true', () => {
  const vehiculoIndex = fs.readFileSync('Views/Vehiculo/Index.cshtml', 'utf8');
  assert.match(vehiculoIndex, /@if\s*\(\s*puedeModificar\s*\)/, 'Must render accordion container when puedeModificar is true');
  assert.doesNotMatch(vehiculoIndex, /@if\s*\(\s*puedeModificar\s*&&\s*ViewBag\.EditVehiculo\s*!=\s*null\s*\)/, 'Must not require ViewBag.EditVehiculo != null for container');
  assert.ok(vehiculoIndex.includes('id="vehiculo-form-container"'), 'vehiculo-form-container must be present');
  assert.ok(vehiculoIndex.includes('id="accordion-flush"'), 'accordion-flush must be present');
});

test('Views/Cliente/Index.cshtml defines puedeCrearTipoVehiculo and contains tipoVehiculoModal', () => {
  const clienteIndex = fs.readFileSync('Views/Cliente/Index.cshtml', 'utf8');
  assert.match(clienteIndex, /var puedeCrearTipoVehiculo\s*=\s*User\.TieneAlgunPermiso\("Vehiculos\.Crear",\s*"Servicios\.Crear"\);/, 'Permission check for creating vehicle type');
  assert.ok(clienteIndex.includes('id="tipoVehiculoModal"'), 'tipoVehiculoModal must be present in Cliente Index view');
  assert.ok(clienteIndex.includes('id="formCrearTipoVehiculo"'), 'formCrearTipoVehiculo must be present in Cliente Index view');
  assert.match(clienteIndex, /asp-controller="Servicio"\s+asp-action="CrearTipoVehiculo"/, 'Form must target Servicio/CrearTipoVehiculo');
});

test('loadVehiculoForm loads partial, unhides accordion, and expands body without error', async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage();
    await page.setContent(`
      <div id="accordion-flush" class="hidden">
        <h2 id="accordion-flush-heading-1">
          <button type="button" data-accordion-target="#accordion-flush-body-1" aria-expanded="false">
            <span id="form-title">Editar Vehículo</span>
          </button>
        </h2>
        <div id="accordion-flush-body-1" class="hidden">
          <div id="vehiculo-form-container"></div>
        </div>
      </div>
      <table id="vehiculos-table">
        <tr>
          <td>
            <button type="button" id="btn-edit" title="Editar" onclick="loadVehiculoForm('veh123')">Editar</button>
          </td>
        </tr>
      </table>
    `);

    await page.addScriptTag({ path: path.resolve('wwwroot/js/vehiculo.js') });

    let pageError = null;
    page.on('pageerror', err => { pageError = err; });

    const result = await page.evaluate(async () => {
      let requestedUrl = null;
      window.Peticiones = {
        enviar: async (url) => {
          requestedUrl = url;
          return {
            text: async () => `
              <form id="vehiculo-form" data-validate="true" data-color="Azul">
                <input id="Id" value="veh123">
                <input id="Patente" value="AA123BB">
                <select id="Color"><option value="Azul" selected>Azul</option></select>
                <button type="button" id="clear-button" onclick="cancelarEdicionVehiculo()">Cancelar</button>
              </form>
            `
          };
        }
      };

      window.loadVehiculoForm('veh123');
      await new Promise(r => setTimeout(r, 150));

      const accordion = document.getElementById('accordion-flush');
      const accordionBody = document.getElementById('accordion-flush-body-1');
      const formContainer = document.getElementById('vehiculo-form-container');
      const formTitle = document.getElementById('form-title');

      return {
        requestedUrl,
        accordionHidden: accordion.classList.contains('hidden'),
        accordionBodyHidden: accordionBody.classList.contains('hidden'),
        hasForm: !!formContainer.querySelector('#vehiculo-form'),
        titleText: formTitle.textContent
      };
    });

    assert.equal(pageError, null, 'No page errors should occur');
    assert.equal(result.requestedUrl, '/Vehiculo/FormPartial?id=veh123');
    assert.equal(result.accordionHidden, false, 'Accordion must be unhidden');
    assert.equal(result.accordionBodyHidden, false, 'Accordion body must be expanded');
    assert.equal(result.hasForm, true, 'Form must be loaded into container');
    assert.equal(result.titleText, 'Editando Vehículo', 'Title should update to Editando Vehículo');

    // Test cancel button
    const cancelResult = await page.evaluate(async () => {
      window.cancelarEdicionVehiculo();
      await new Promise(r => setTimeout(r, 350));
      const accordion = document.getElementById('accordion-flush');
      const accordionBody = document.getElementById('accordion-flush-body-1');
      return {
        accordionHidden: accordion.classList.contains('hidden'),
        accordionBodyHidden: accordionBody.classList.contains('hidden')
      };
    });

    assert.equal(cancelResult.accordionHidden, true, 'Accordion should be hidden after cancel');
    assert.equal(cancelResult.accordionBodyHidden, true, 'Accordion body should be hidden after cancel');
  } finally {
    await browser.close();
  }
});

test('tipoVehiculoModal opens from quick create vehicle modal and updates select on submit', async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage();
    await page.setContent(`
      <div id="cliente-form-container"></div>
      <div id="quick-create-modal" class="hidden">
        <div id="quick-vehiculo-messages"></div>
        <div id="quick-vehiculo-form-content">
          <form id="vehiculo-form">
            <select id="TipoVehiculo">
              <option value="">Seleccione primero...</option>
              <option value="Auto">Auto</option>
            </select>
            <button type="button" id="btn-add-tipo" data-modal-target="tipoVehiculoModal" data-modal-toggle="tipoVehiculoModal">
              +
            </button>
            <input id="Patente" value="">
            <p id="patente-formato-hint"></p>
          </form>
        </div>
      </div>

      <div id="tipoVehiculoModal" class="hidden" tabindex="-1">
        <div id="tipo-vehiculo-modal-messages"></div>
        <form id="formCrearTipoVehiculo" action="/Servicio/CrearTipoVehiculo">
          <input type="text" name="nombreTipo" id="nombreTipoVehiculo" value="">
          <input type="text" name="formatoPatente" id="formatoPatenteVehiculo" value="">
          <button type="submit" id="btn-submit-tipo">Registrar</button>
          <button type="button" id="btn-cancel-tipo" onclick="closeTipoVehiculoModal()">Cancelar</button>
        </form>
      </div>
    `);

    await page.addScriptTag({ path: path.resolve('wwwroot/js/cliente.js') });

    let pageError = null;
    page.on('pageerror', err => { pageError = err; });

    await page.evaluate(() => {
      document.dispatchEvent(new Event('DOMContentLoaded'));
      const quick = document.getElementById('quick-create-modal');
      quick.classList.remove('hidden');
    });

    // 1. Click "+" button on TipoVehiculo: should open tipoVehiculoModal
    await page.locator('#btn-add-tipo').click();
    const modalStateAfterClick = await page.evaluate(() => {
      const modal = document.getElementById('tipoVehiculoModal');
      return {
        hidden: modal.classList.contains('hidden')
      };
    });
    assert.equal(modalStateAfterClick.hidden, false, 'tipoVehiculoModal should be open/visible');

    // 2. Validate empty submission inside tipoVehiculoModal shows error
    await page.evaluate(() => {
      const form = document.getElementById('formCrearTipoVehiculo');
      form.dispatchEvent(new Event('submit', { cancelable: true }));
    });
    const errorMsg = await page.locator('#tipo-vehiculo-modal-messages').textContent();
    assert.ok(errorMsg.includes('obligatorio'), 'Must show validation error for empty name');

    // 3. Submit valid new vehicle type "Camioneta" with format "llnnnll"
    const submitResult = await page.evaluate(async () => {
      let postRequest = null;
      window.Peticiones = {
        enviar: async (url, options) => {
          if (options?.method === 'POST') {
            postRequest = {
              url,
              nombreTipo: options.body.get('nombreTipo'),
              formatoPatente: options.body.get('formatoPatente')
            };
            return {
              ok: true,
              text: async () => JSON.stringify({
                success: true,
                message: 'Tipo de vehículo creado correctamente.',
                tipos: ['Auto', 'Camioneta'],
                tiposCompletos: [
                  { nombre: 'Auto', formatoPatente: 'llnnnll|lllnnn' },
                  { nombre: 'Camioneta', formatoPatente: 'llnnnll' }
                ]
              })
            };
          }
          if (url.includes('ObtenerTiposConFormatos')) {
            return {
              json: async () => [
                { nombre: 'Auto', formato: 'llnnnll|lllnnn', regex: '^[A-Z]{2}\\d{3}[A-Z]{2}$' },
                { nombre: 'Camioneta', formato: 'llnnnll', regex: '^[A-Z]{2}\\d{3}[A-Z]{2}$' }
              ]
            };
          }
          return { json: async () => [] };
        }
      };

      document.getElementById('nombreTipoVehiculo').value = 'Camioneta';
      document.getElementById('formatoPatenteVehiculo').value = 'llnnnll';

      const form = document.getElementById('formCrearTipoVehiculo');
      form.dispatchEvent(new Event('submit', { cancelable: true }));
      await new Promise(r => setTimeout(r, 100));

      const modal = document.getElementById('tipoVehiculoModal');
      const quick = document.getElementById('quick-create-modal');
      const select = document.getElementById('TipoVehiculo');
      const hint = document.getElementById('patente-formato-hint');

      return {
        postRequest,
        modalHidden: modal.classList.contains('hidden'),
        quickHidden: quick.classList.contains('hidden'),
        selectedValue: select.value,
        selectOptions: Array.from(select.options).map(o => o.value),
        hintText: hint.textContent
      };
    });

    assert.equal(pageError, null, 'No page errors during flow');
    assert.deepEqual(submitResult.postRequest, {
      url: '/Servicio/CrearTipoVehiculo',
      nombreTipo: 'Camioneta',
      formatoPatente: 'llnnnll'
    });
    assert.equal(submitResult.modalHidden, true, 'tipoVehiculoModal should close after success');
    assert.equal(submitResult.quickHidden, false, 'quick-create-modal must remain open');
    assert.equal(submitResult.selectedValue, 'Camioneta', 'New type must be selected');
    assert.ok(submitResult.selectOptions.includes('Camioneta'), 'Options must include Camioneta');
    assert.ok(submitResult.hintText.includes('llnnnll'), 'Hint must update with new format');
  } finally {
    await browser.close();
  }
});

test('openQuickCreateVehiculoModal e2e flow allows opening tipoVehiculoModal dynamically', async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage();
    await page.setContent(`
      <div id="tipoVehiculoModal" class="hidden" tabindex="-1">
        <div id="tipo-vehiculo-modal-messages"></div>
        <form id="formCrearTipoVehiculo" action="/Servicio/CrearTipoVehiculo">
          <input type="text" name="nombreTipo" id="nombreTipoVehiculo" value="">
          <input type="text" name="formatoPatente" id="formatoPatenteVehiculo" value="">
        </form>
      </div>
      <button id="add-vehiculo-btn" onclick="openQuickCreateVehiculoModal()">Agregar Vehiculo</button>
    `);

    await page.addScriptTag({ path: path.resolve('wwwroot/js/cliente.js') });

    let pageError = null;
    page.on('pageerror', err => { pageError = err; });

    await page.evaluate(() => {
      window.Peticiones = {
        enviar: async (url) => {
          if (url.includes('ObtenerTiposConFormatos')) {
            return { json: async () => [{ nombre: 'Auto', formato: 'llnnnll', regex: '^[A-Z]{2}\\d{3}[A-Z]{2}$' }] };
          }
          if (url.includes('FormPartialCliente')) {
            return {
              text: async () => `
                <form id="vehiculo-form">
                  <select id="TipoVehiculo"><option value="Auto">Auto</option></select>
                  <button type="button" id="btn-add-tipo-dynamic" data-modal-target="tipoVehiculoModal" data-modal-toggle="tipoVehiculoModal">+</button>
                  <input id="Patente" value="">
                  <input id="Marca" value="">
                  <input id="Modelo" value="">
                  <input id="Color" value="">
                </form>
              `
            };
          }
          return { json: async () => [] };
        }
      };
      document.dispatchEvent(new Event('DOMContentLoaded'));
    });

    // Call openQuickCreateVehiculoModal
    await page.evaluate(async () => {
      await window.openQuickCreateVehiculoModal();
    });

    const quickModalVisible = await page.evaluate(() => {
      const q = document.getElementById('quick-create-modal');
      return q && !q.classList.contains('hidden');
    });
    assert.equal(quickModalVisible, true, 'Quick create modal must be in DOM and visible');

    // Click the dynamically injected button inside quick-create-modal
    await page.locator('#btn-add-tipo-dynamic').click();

    const tipoModalVisible = await page.evaluate(() => {
      const m = document.getElementById('tipoVehiculoModal');
      return m && !m.classList.contains('hidden');
    });
    assert.equal(tipoModalVisible, true, 'tipoVehiculoModal must be opened by clicking the dynamic button in quick create modal');

    // Close tipoVehiculoModal: quick modal should stay open
    await page.evaluate(() => {
      window.closeTipoVehiculoModal();
    });

    const afterClose = await page.evaluate(() => {
      const m = document.getElementById('tipoVehiculoModal');
      const q = document.getElementById('quick-create-modal');
      return {
        tipoModalHidden: m.classList.contains('hidden'),
        quickModalHidden: q.classList.contains('hidden')
      };
    });
    assert.equal(afterClose.tipoModalHidden, true, 'tipoVehiculoModal must be closed');
    assert.equal(afterClose.quickModalHidden, false, 'quick-create-modal must remain open');
    assert.equal(pageError, null, 'No page errors should occur');
  } finally {
    await browser.close();
  }
});
