'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

test('login uses shared immediate persistent validation and preserves native form submission', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    const view = fs.readFileSync('Views/Login/Index.cshtml', 'utf8');
    const form = view.slice(view.indexOf('<form method="post" id="loginForm"'), view.indexOf('</form>') + 7).replace(/@if \(ViewBag.Error[\s\S]*?\n            }/, '');
    await page.setContent(form + '<form id="other"><input id="otherField" required><span id="otherField-validation-error" style="display:none">Servidor</span></form>');
    await page.addScriptTag({ path: path.resolve('wwwroot/js/login.js') });
    await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
    const result = await page.evaluate(async () => {
      document.dispatchEvent(new Event('DOMContentLoaded'));
      FormValidation.init(document.querySelector('#other'));
      const other = document.querySelector('#otherField-validation-error');
      const serverBefore = other.textContent;
      document.querySelector('#otherField').dispatchEvent(new Event('blur'));
      const otherDisplay = other.style.display;
      const form = document.querySelector('#loginForm');
      const email = document.querySelector('#email');
      const password = document.querySelector('#password');
      const error = id => document.querySelector(`#${id}-validation-error`).textContent;
      const submit = () => form.dispatchEvent(new Event('submit', { cancelable: true }));
      email.dispatchEvent(new Event('input'));
      const immediate = error('email');
      password.dispatchEvent(new Event('blur'));
      const emptyPassword = error('password');
      await new Promise(resolve => setTimeout(resolve, 3200));
      const persistent = error('email');
      const invalid = submit();
      const unchanged = [email.value, password.value];
      email.value = 'bad'; email.dispatchEvent(new Event('input'));
      const malformed = error('email');
      email.value = 'person@example.com'; email.dispatchEvent(new Event('input'));
      password.value = 'x'; password.dispatchEvent(new Event('input'));
      const valid = submit();
      return { serverBefore, otherDisplay, immediate, emptyPassword, persistent, invalid, unchanged, malformed, valid, errors: [error('email'), error('password')], action: form.getAttribute('action'), method: form.method, fields: [email.name, password.name], initialized: form.dataset.formValidationInitialized };
    });
    assert.deepEqual(result, { serverBefore: 'Servidor', otherDisplay: 'none', immediate: 'El correo electrónico es obligatorio', emptyPassword: 'La contraseña es obligatoria', persistent: 'El correo electrónico es obligatorio', invalid: false, unchanged: ['', ''], malformed: 'Ingrese un correo electrónico válido', valid: true, errors: ['', ''], action: '/Login/Login', method: 'post', fields: ['email', 'password'], initialized: 'true' });
  } finally { await browser.close(); }
});
