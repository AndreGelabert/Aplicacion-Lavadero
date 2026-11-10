'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

test('markers and repeated initialization preserve messages and initialize new fields once', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent(`<form id="f"><label for="a">Nombre <span class="text-red-500">*</span></label><input id="a" name="a" required><span data-valmsg-for="a">Error del servidor</span></form>`);
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    const result = await page.evaluate(async () => {
      const form = document.querySelector('form');
      FormValidation.init(form);
      FormValidation.init(form);
      const marker = document.querySelector('label span');
      const server = document.querySelector('[data-valmsg-for]').textContent;
      form.insertAdjacentHTML('beforeend', '<label for="b">Otro</label><input id="b" required>');
      FormValidation.init(form);
      let calls = 0;
      FormValidation.registerCustomValidator('b', () => { calls++; return { isValid: false, message: 'Personalizado' }; });
      document.querySelector('#b').dispatchEvent(new Event('blur'));
      return { marker: marker.className, semantic: marker.getAttribute('aria-hidden'), stars: document.querySelector('label').textContent.match(/\*/g).length, server, calls, error: document.querySelector('#b-validation-error')?.textContent };
    });
    assert.match(result.marker, /dark:text-red-400/);
    assert.equal(result.semantic, 'true');
    assert.equal(result.stars, 1);
    assert.equal(result.server, 'Error del servidor');
    assert.equal(result.calls, 1);
    assert.equal(result.error, 'Personalizado');
  } finally { await browser.close(); }
});

test('options stay fixed on first initialization, select change and hidden required validation remain active', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent(`<form><label for="a">Nombre</label><input id="a" required><label for="s">Tipo</label><select id="s" required><option value="">Elija</option><option value="x">X</option></select><div hidden><label for="h">Oculto</label><input id="h" required></div></form>`);
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    const result = await page.evaluate(async () => {
      const form = document.querySelector('form');
      FormValidation.init(form, { validateOnInput: false, validateOnBlur: false, showAsterisk: false });
      FormValidation.init(form, { validateOnInput: true, validateOnBlur: true, showAsterisk: false });
      const input = document.querySelector('#a');
      input.dispatchEvent(new Event('blur')); input.dispatchEvent(new Event('input'));
      await new Promise(resolve => setTimeout(resolve, 350));
      const unchanged = document.querySelector('#a-validation-error').textContent;
      const select = document.querySelector('#s');
      select.dispatchEvent(new Event('change'));
      const selectError = document.querySelector('#s-validation-error').textContent;
      select.value = 'x'; select.dispatchEvent(new Event('change'));
      const selectCleared = document.querySelector('#s-validation-error').textContent;
      input.value = 'Ana';
      const valid = FormValidation.validateForm(form);
      return { unchanged, selectError, selectCleared, valid, hiddenError: document.querySelector('#h-validation-error').textContent, stars: form.querySelectorAll('label span').length };
    });
    assert.deepEqual(result, { unchanged: '', selectError: 'Tipo es obligatorio', selectCleared: '', valid: false, hiddenError: 'Oculto es obligatorio', stars: 0 });
  } finally { await browser.close(); }
});

test('existing nested and mixed label markers preserve text and are not duplicated', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent(`<form><label for="a">Nombre <strong><span class="text-red-500">*</span></strong> completo</label><input id="a" required><label for="b"><span class="text-red-600">Código * de cliente</span></label><input id="b" required><label for="c">Correo <em>*</em> personal</label><input id="c" required></form>`);
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    const labels = await page.evaluate(() => {
      const form = document.querySelector('form');
      FormValidation.init(form); FormValidation.init(form);
      return Array.from(form.querySelectorAll('label'), label => ({ text: label.textContent, markers: label.querySelectorAll('[aria-hidden="true"]').length, stars: (label.textContent.match(/\*/g) || []).length, markerClass: label.querySelector('[aria-hidden="true"]')?.className }));
    });
    assert.deepEqual(labels.map(({ text, markers, stars }) => ({ text, markers, stars })), [
      { text: 'Nombre * completo', markers: 1, stars: 1 },
      { text: 'Código * de cliente', markers: 1, stars: 1 },
      { text: 'Correo * personal', markers: 1, stars: 1 },
    ]);
    for (const label of labels) assert.match(label.markerClass, /dark:text-red-400/);
  } finally { await browser.close(); }
});

test('enabled required fields retain validation and input debounce', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent('<form><label for="a">Nombre</label><input id="a" required></form>');
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    const result = await page.evaluate(async () => {
      FormValidation.init(document.querySelector('form'));
      const field = document.querySelector('input');
      field.dispatchEvent(new Event('blur'));
      const error = document.querySelector('#a-validation-error').textContent;
      field.value = 'Ana'; field.dispatchEvent(new Event('input'));
      const beforeDebounce = document.querySelector('#a-validation-error').textContent;
      await new Promise(resolve => setTimeout(resolve, 350));
      const cleared = document.querySelector('#a-validation-error').textContent;
      field.value = ''; field.dispatchEvent(new Event('blur'));
      await new Promise(resolve => setTimeout(resolve, 3200));
      return { error, beforeDebounce, cleared, timedOut: document.querySelector('#a-validation-error').textContent };
    });
    assert.deepEqual(result, { error: 'Nombre es obligatorio', beforeDebounce: 'Nombre es obligatorio', cleared: '', timedOut: '' });
  } finally { await browser.close(); }
});
