'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

function field(source, id) {
  const tag = source.match(new RegExp(`<input\\b[^>]*\\bid="${id}"[^>]*>`, 's'))?.[0];
  assert.ok(tag, `missing ${id}`);
  return tag.replace(/value="@\([^\"]*\)"/g, 'value=""');
}

test('module declarations retain shared opt-in and Rol remains independent', () => {
  for (const name of ['Cliente', 'Vehiculo', 'Servicio', 'PaqueteServicio', 'Lavados', 'Configuracion']) {
    const file = name === 'Configuracion' ? 'Views/Configuracion/Index.cshtml' : `Views/${name}/_${name === 'Lavados' ? 'Lavado' : name}Form.cshtml`;
    const source = fs.readFileSync(file, 'utf8');
    assert.match(source, /<form\b[^>]*data-validate="true"/);
    assert.match(fs.readFileSync(`Views/${name}/Index.cshtml`, 'utf8'), /form-validation\.js/);
  }
  assert.doesNotMatch(fs.readFileSync('Views/Rol/_RolForm.cshtml', 'utf8').match(/<form\b[^>]*>/s)[0], /data-validate/);
});

test('Servicio preserves custom normalization, submit checks, hooks and payload', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    const source = fs.readFileSync('Views/Servicio/_ServicioForm.cshtml', 'utf8');
    assert.match(source, /onsubmit="return submitServicioAjax\(this\);"/);
    await page.setContent(`<form id="servicio-form" action="/Servicio/CrearServicioAjax">${['Nombre', 'Precio', 'TiempoEstimado'].map(id => `<div><label for="${id}">${id} *</label>${field(source, id)}</div>`).join('')}<textarea name="Descripcion" id="Descripcion" required></textarea><input name="EtapasJson" value="[]"></form><input id="nuevaEtapaNombre">`);
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    await page.addScriptTag({ path: path.resolve('wwwroot/js/servicio.js') });
    const result = await page.evaluate(() => {
      window.PageModules.servicios.init();
      const form = document.querySelector('form');
      FormValidation.init(form); FormValidation.init(form);
      const precio = document.querySelector('#Precio');
      const tiempo = document.querySelector('#TiempoEstimado');
      const etapa = document.querySelector('#nuevaEtapaNombre');
      precio.value = '-5'; precio.dispatchEvent(new Event('input')); const normalizedPrice = precio.value;
      tiempo.value = '0'; tiempo.dispatchEvent(new Event('input')); const normalizedTime = tiempo.value;
      etapa.value = 'Ab1'; etapa.dispatchEvent(new Event('input')); const stage = [etapa.value, etapa.validationMessage];
      document.querySelector('#Nombre').value = 'Nombre2'; precio.value = '100'; tiempo.value = '10'; document.querySelector('#Descripcion').value = 'Lavado';
      const rejected = form.dispatchEvent(new Event('submit', { cancelable: true }));
      const error = document.querySelector('#form-error-message')?.textContent;
      document.querySelector('#Nombre').value = 'Lavado';
      const accepted = form.dispatchEvent(new Event('submit', { cancelable: true }));
      const payload = Object.fromEntries(new FormData(form));
      return { normalizedPrice, normalizedTime, stage, rejected, error, accepted, payload, markers: document.querySelectorAll('label[for="Precio"] [aria-hidden="true"]').length, containers: document.querySelectorAll('#Precio-validation-error').length };
    });
    assert.deepEqual(result, { normalizedPrice: '5', normalizedTime: '1', stage: ['Ab', 'El nombre debe tener al menos 3 letras'], rejected: false, error: '¡Error! El nombre solo puede contener letras y espacios', accepted: true, payload: { Nombre: 'Lavado', Precio: '100', TiempoEstimado: '10', Descripcion: 'Lavado', EtapasJson: '[]' }, markers: 1, containers: 1 });
  } finally { await browser.close(); }
});

test('Cliente retains delayed integration and telephone custom validator without duplicate listeners', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    const source = fs.readFileSync('Views/Cliente/_ClienteForm.cshtml', 'utf8');
    assert.match(source, /onsubmit="return submitClienteAjax\(this, event\);"/);
    await page.setContent(`<form id="cliente-form" data-validate="true"><label for="Telefono">Teléfono *</label>${field(source, 'Telefono')}</form>`);
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    await page.addScriptTag({ path: path.resolve('wwwroot/js/cliente-validation-integration.js') });
    const result = await page.evaluate(async () => {
      document.dispatchEvent(new Event('DOMContentLoaded'));
      await new Promise(resolve => setTimeout(resolve, 350));
      const form = document.querySelector('form'), input = document.querySelector('#Telefono');
      FormValidation.init(form);
      let calls = 0; const validator = FormValidation.customValidators.Telefono;
      FormValidation.registerCustomValidator('Telefono', () => { calls++; return validator(); });
      input.value = '123'; input.dispatchEvent(new Event('blur'));
      const error = document.querySelector('#Telefono-validation-error').textContent;
      input.value = '1234567890'; input.dispatchEvent(new Event('blur'));
      return { calls, error, cleared: document.querySelector('#Telefono-validation-error').textContent, valid: window.validateClienteForm(), markers: document.querySelectorAll('label [aria-hidden="true"]').length, containers: document.querySelectorAll('#Telefono-validation-error').length };
    });
    assert.deepEqual(result, { calls: 2, error: 'El teléfono debe contener exactamente 10 dígitos', cleared: '', valid: true, markers: 1, containers: 1 });
  } finally { await browser.close(); }
});
