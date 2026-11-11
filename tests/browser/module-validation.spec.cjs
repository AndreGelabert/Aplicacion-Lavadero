const fs = require('node:fs');
const path = require('node:path');
const { test, expect } = require('@playwright/test');

test('Servicio real controls preserve normalization and shared marker after repeat initialization', async ({ page }) => {
  const source = fs.readFileSync('Views/Servicio/_ServicioForm.cshtml', 'utf8');
  const input = source.match(/<input\b[^>]*\bid="Precio"[^>]*>/s)[0].replace(/value="@\([^\"]*\)"/g, 'value=""');
  await page.setContent(`<form id="servicio-form"><label for="Precio">Precio <span class="text-red-600">*</span></label>${input}</form>`);
  await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
  await page.addScriptTag({ path: path.resolve('wwwroot/js/servicio.js') });
  await page.evaluate(() => { PageModules.servicios.init(); FormValidation.init(document.querySelector('form')); FormValidation.init(document.querySelector('form')); });
  await page.locator('#Precio').fill('-3');
  await expect(page.locator('#Precio')).toHaveValue('3');
  await expect(page.locator('label [aria-hidden="true"]')).toHaveCount(1);
  await expect(page.locator('#Precio-validation-error')).toHaveCount(1);
  await page.locator('#Precio').fill('');
  await page.locator('#Precio').dispatchEvent('blur');
  await expect(page.locator('#Precio')).toHaveValue('0');
});
