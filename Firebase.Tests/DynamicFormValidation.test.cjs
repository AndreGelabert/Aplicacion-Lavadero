'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

test('Cliente validation checks the current replacement instead of the detached original', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent('<div id="cliente-form-container"><form id="cliente-form" data-validate="true"><label for="Nombre">Nombre *</label><input id="Nombre" name="Nombre" required value="Correcto"></form></div>');
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    await page.addScriptTag({ path: path.resolve('wwwroot/js/cliente-validation-integration.js') });
    await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
    await page.waitForFunction(() => typeof window.validateClienteForm === 'function');
    const result = await page.evaluate(() => {
      document.getElementById('cliente-form-container').innerHTML = '<form id="cliente-form" data-validate="true"><label for="Nombre">Nombre *</label><input id="Nombre" name="Nombre" required value=" "></form>';
      const valid = window.validateClienteForm();
      return { valid, value: document.getElementById('Nombre').value, error: document.getElementById('Nombre-validation-error')?.textContent };
    });
    assert.deepEqual(result, { valid: false, value: ' ', error: 'Nombre es obligatorio' });
  } finally { await browser.close(); }
});

test('Paquete HTTP error replacement retains server errors and gains validation', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent(`<base href="http://localhost/"><div id="paquete-form-container"><form id="paquete-form" action="/PaqueteServicio/Guardar" data-validate="true"><input name="__RequestVerificationToken" value="token"><input id="PaqueteServiciosIdsData" value="1,2"><input id="ServiciosIdsJson" name="ServiciosIdsJson"><select id="TipoVehiculo"><option value="auto">Auto</option></select><span id="tiempo-total"></span></form></div>`);
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    await page.evaluate(() => { document.getElementById('PaqueteServiciosIdsData').value = ''; });
    await page.addScriptTag({ path: path.resolve('wwwroot/js/paqueteservicio.js') });
    const result = await page.evaluate(async () => {
      const requests = [], messages = [];
      window.Notificaciones = { mostrar: (...args) => messages.push(args) };
      window.Peticiones = { enviar: async (url, options) => {
        if (!options?.method) return { json: async () => ({ success: true, servicios: [{ id: '1', nombre: 'Uno', tipo: 'A', precio: 1, tiempoEstimado: 1 }, { id: '2', nombre: 'Dos', tipo: 'B', precio: 2, tiempoEstimado: 2 }] }) };
        requests.push({ url, method: options.method, body: Object.fromEntries(options.body), header: options.headers['X-Requested-With'] });
        return { ok: false, status: 400, headers: new Headers(), text: async () => '<form id="paquete-form" data-validate="true"><span id="server-error">Error del servidor</span><label for="Probe">Campo *</label><input id="Probe" required value=" "></form>' };
      } };
      await window.loadServiciosPorTipoVehiculo();
      window.selectServicioFromDropdown('1');
      window.agregarServicioSeleccionado();
      window.selectServicioFromDropdown('2');
      window.agregarServicioSeleccionado();
      window.submitPaqueteAjax(document.getElementById('paquete-form'), new Event('submit'));
      await new Promise(resolve => setTimeout(resolve, 100));
      const input = document.getElementById('Probe');
      input.dispatchEvent(new Event('blur'));
      return { requests, messages, initialized: input.dataset.formValidationInitialized, error: document.getElementById('server-error').textContent, validation: document.getElementById('Probe-validation-error')?.textContent };
    });
    assert.deepEqual(result.requests, [{ url: 'http://localhost/PaqueteServicio/Guardar', method: 'POST', body: { __RequestVerificationToken: 'token', ServiciosIdsJson: '["1","2"]' }, header: 'XMLHttpRequest' }]);
    assert.deepEqual(result.messages.at(-1), ['Error al procesar la solicitud.', 'error']);
    assert.equal(result.error, 'Error del servidor');
    assert.equal(result.initialized, 'true');
    assert.equal(result.validation, 'Campo es obligatorio');
  } finally { await browser.close(); }
});

test('public submits preserve payload and initialize server replacements or retain data on network failure', async () => {
  const browser = await chromium.launch();
  try {
    for (const module of ['cliente', 'vehiculo', 'servicio', 'paqueteservicio']) {
      for (const outcome of ['replacement', 'network', 'denied', 'invalid']) {
        if (module === 'vehiculo' && outcome === 'denied') continue;
        const page = await browser.newPage();
        const name = module === 'paqueteservicio' ? 'paquete' : module;
        const title = { cliente: 'Cliente', vehiculo: 'Vehiculo', servicio: 'Servicio', paqueteservicio: 'PaqueteServicio' }[module];
        const submit = { cliente: 'submitClienteAjax', vehiculo: 'submitVehiculoAjax', servicio: 'submitServicioAjax', paqueteservicio: 'submitPaqueteAjax' }[module];
        const form = `<form id="${name}-form" action="/${title}/Guardar" data-validate="true"><input id="Id" name="Id" value="edit"><input name="__RequestVerificationToken" value="token"><input name="Dato" value="original"><input name="Duplicado" value="uno"><input name="Duplicado" value="dos"><input id="VehiculosIdsData" value="${module === 'cliente' && outcome === 'invalid' ? '' : 'v1'}"><input id="PaqueteServiciosIdsData" value=""><input id="ServiciosIdsJson" name="ServiciosIdsJson"><select id="TipoVehiculo"><option value="auto">Auto</option></select><input id="Patente" name="Patente" value="ABC123"><span id="tiempo-total"></span><input id="vehiculo-search"><div id="vehiculo-dropdown"><div id="vehiculo-dropdown-content"></div></div><div id="vehiculos-seleccionados-container"><div id="vehiculos-seleccionados-list"></div></div><input id="servicio-search"><div id="servicio-selector-container"><div id="servicio-dropdown"><div id="servicio-dropdown-content"></div></div></div><div id="servicios-seleccionados-container"><div id="servicios-seleccionados-list"></div></div><button id="submit-button">Guardar</button></form>`;
        await page.setContent(`<base href="http://localhost/"><div id="accordion-flush"><div id="${name}-form-container" data-can-create="false">${form}</div></div>`);
        await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
        await page.addScriptTag({ path: path.resolve(`wwwroot/js/${module}.js`) });
        const result = await page.evaluate(async ({ module, name, submit, outcome, form }) => {
          const requests = [], messages = [];
          const vehicle = { id: 'v1', patente: 'ABC123', marca: 'Marca', modelo: 'Modelo', color: 'Azul', tipoVehiculo: 'auto' };
          const services = [{ id: '1', nombre: 'Uno', tipo: 'A', precio: 1, tiempoEstimado: 1 }, { id: '2', nombre: 'Dos', tipo: 'B', precio: 2, tiempoEstimado: 2 }];
          window.Notificaciones = { mostrar: (...args) => messages.push(args) };
          window.Peticiones = { enviar: async (url, options) => {
            if (!options?.method) {
              if (String(url).includes('FormPartial')) return { text: async () => form };
              return { json: async () => String(url).includes('GetVehiculosCliente') ? { success: true, vehiculos: [vehicle] } : { success: true, servicios: services } };
            }
            requests.push({ url, method: options.method, header: options.headers['X-Requested-With'], body: Array.from(options.body.entries()) });
            if (outcome === 'network') throw new Error('offline');
            return { ok: true, status: 200, headers: new Headers({ 'X-Form-Valid': outcome === 'denied' ? 'true' : 'false' }), text: async () => `<form id="${name}-form" data-validate="true"><span id="server-error">Error del servidor</span><label for="Probe">Campo *</label><input id="Probe" required value=" "></form>` };
          } };
          if (module === 'cliente') await window.loadClienteForm('edit');
          if (module === 'paqueteservicio') {
            await window.loadServiciosPorTipoVehiculo();
            window.selectServicioFromDropdown('1'); window.agregarServicioSeleccionado();
            if (outcome !== 'invalid') { window.selectServicioFromDropdown('2'); window.agregarServicioSeleccionado(); }
          }
          if (outcome === 'invalid' && module === 'vehiculo') document.getElementById('Patente').value = 'AB';
          if (outcome === 'invalid' && module === 'servicio') {
            const f = document.getElementById('servicio-form');
            f.insertAdjacentHTML('beforeend', '<input id="Nombre" name="Nombre" required value=" ">');
            window.PageModules.servicios.init();
          }
          const snapshot = () => ({ fields: Array.from(document.querySelectorAll(`#${name}-form input, #${name}-form select, #${name}-form textarea`), el => [el.id, el.name, el.value, el.checked, el.selectedIndex ?? null]), selection: Array.from(document.querySelectorAll('#vehiculo-dropdown, #vehiculos-seleccionados-container, #servicio-selector-container, #servicios-seleccionados-container'), el => [el.id, el.className, el.innerHTML]) });
          const before = snapshot();
          if (outcome === 'invalid' && module === 'servicio') {
            const f = document.getElementById('servicio-form');
            f.addEventListener('submit', e => { if (!e.defaultPrevented) window.submitServicioAjax(f); });
            f.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
          } else window[submit](document.getElementById(`${name}-form`), new Event('submit', { cancelable: true }));
          await new Promise(resolve => setTimeout(resolve, 150));
          const field = document.getElementById('Probe');
          field?.dispatchEvent(new Event('blur'));
          return { requests, messages, before, after: snapshot(), invalidMessage: document.getElementById('form-error-message')?.textContent, patenteMessage: document.getElementById('Patente')?.validationMessage, initialized: field?.dataset.formValidationInitialized, error: document.getElementById('server-error')?.textContent, validation: document.getElementById('Probe-validation-error')?.textContent, data: document.querySelector('[name="Dato"]')?.value, patente: document.getElementById('Patente')?.value, denied: document.getElementById(`${name}-form-container`).textContent, accordionHidden: document.getElementById('accordion-flush').classList.contains('hidden') };
        }, { module, name, submit, outcome, form });
        if (outcome === 'invalid') {
          assert.equal(result.requests.length, 0, `${module} invalid`);
          assert.equal(result.data, 'original', module);
          assert.deepEqual(result.after, result.before, `${module} invalid state`);
          if (module === 'vehiculo') assert.equal(result.patenteMessage, 'La patente debe tener al menos 3 caracteres');
          if (module === 'servicio') assert.match(result.invalidMessage, /Por favor, complete todos los campos obligatorios\./);
          if (module === 'cliente') assert.deepEqual(result.messages.at(-1), ['Debe agregar al menos un vehículo para el cliente.', 'error']);
          if (module === 'paqueteservicio') assert.deepEqual(result.messages.at(-1), ['Debe seleccionar al menos 2 servicios.', 'error']);
        } else {
          assert.equal(result.requests.length, 1, `${module} ${outcome}`);
          const request = result.requests[0];
          assert.equal(request.url, `http://localhost/${title}/Guardar`);
          assert.equal(request.method, 'POST'); assert.equal(request.header, 'XMLHttpRequest');
          const expected = [['Id', 'edit'], ['__RequestVerificationToken', 'token'], ['Dato', 'original'], ['Duplicado', 'uno'], ['Duplicado', 'dos'], ['ServiciosIdsJson', module === 'paqueteservicio' ? '["1","2"]' : ''], ['Patente', 'ABC123']];
          if (module === 'cliente') expected.push(['VehiculosDataJson', JSON.stringify([{ id: 'v1', patente: 'ABC123', marca: 'Marca', marcaId: '', marcaManual: false, modelo: 'Modelo', modeloManual: false, color: 'Azul', tipoVehiculo: 'auto', esNuevo: false, esReasignacion: false, esAsociacion: false, claveAsociacion: '' }])]);
          assert.deepEqual(request.body, expected, `${module} complete request entries`);
          if (outcome === 'network') {
            assert.equal(result.data, 'original');
            if (module === 'paqueteservicio') {
              assert.equal(result.before.fields.find(field => field[0] === 'ServiciosIdsJson')[2], '');
              assert.equal(result.after.fields.find(field => field[0] === 'ServiciosIdsJson')[2], '["1","2"]');
              assert.deepEqual(result.after.fields.filter(field => field[0] !== 'ServiciosIdsJson'), result.before.fields.filter(field => field[0] !== 'ServiciosIdsJson'));
              assert.deepEqual(result.after.selection, result.before.selection);
            } else assert.deepEqual(result.after, result.before, `${module} network state`);
            assert.deepEqual(result.messages.at(-1), [module === 'paqueteservicio' ? 'Error al procesar la solicitud.' : 'Error de comunicación con el servidor.', 'error']);
          } else if (outcome === 'denied') {
            assert.match(result.denied, /No tiene permiso para registrar/); assert.equal(result.accordionHidden, true);
          } else {
            assert.equal(result.error, 'Error del servidor'); assert.equal(result.initialized, 'true');
            assert.equal(result.validation, 'Campo es obligatorio');
            assert.deepEqual(result.messages.at(-1), ['Revise los errores del formulario.', 'error']);
          }
        }
        await page.close();
      }
    }
  } finally { await browser.close(); }
});

test('actual AJAX form loaders initialize each replacement without duplicate field listeners', async () => {
  const browser = await chromium.launch();
  try {
    for (const [script, loader, container, form] of [
      ['cliente', 'loadClienteForm', 'cliente-form-container', 'cliente-form'],
      ['vehiculo', 'loadVehiculoForm', 'vehiculo-form-container', 'vehiculo-form'],
      ['servicio', 'loadServicioForm', 'servicio-form-container', 'servicio-form'],
      ['paqueteservicio', 'editPaquete', 'paquete-form-container', 'paquete-form'],
    ]) {
      const page = await browser.newPage();
      await page.setContent(`<div id="${container}"><form id="${form}" data-validate="true"><label for="Probe">Campo *</label><input id="Probe" name="Probe" required value="initial"></form></div><span id="form-title"></span>`);
      await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
      await page.addScriptTag({ path: path.resolve(`wwwroot/js/${script}.js`) });
      const result = await page.evaluate(async ({ loader, form }) => {
        FormValidation.init(document.getElementById(form));
        const initial = document.querySelector('#Probe').dataset.formValidationInitialized;
        const html = `<form id="${form}" data-validate="true"><label for="Probe">Campo *</label><input id="Probe" name="Probe" required value=" "></form>`;
        window.Peticiones = { enviar: async () => ({ ok: true, headers: new Headers(), text: async () => html }) };
        for (let i = 0; i < 2; i++) {
          window[loader]('1');
          await new Promise(resolve => setTimeout(resolve, 100));
        }
        const field = document.querySelector('#Probe');
        let calls = 0;
        const validate = FormValidation.validateField.bind(FormValidation);
        FormValidation.validateField = input => { calls++; return validate(input); };
        field.dispatchEvent(new Event('blur'));
        return { initial, initialized: field.dataset.formValidationInitialized, markers: document.querySelectorAll('label [aria-hidden="true"]').length, calls, error: document.querySelector('#Probe-validation-error')?.textContent, payload: Array.from(new FormData(field.form).entries()) };
      }, { loader, form });
      assert.deepEqual(result, { initial: 'true', initialized: 'true', markers: 1, calls: 1, error: 'Campo es obligatorio', payload: [['Probe', ' ']] }, script);
      await page.close();
    }
  } finally { await browser.close(); }
});
