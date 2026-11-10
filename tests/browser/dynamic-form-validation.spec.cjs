const path = require('node:path');
const { test, expect } = require('@playwright/test');

test('reloaded Servicio form gains shared live validation', async ({ page }) => {
  await page.setContent('<div id="servicio-form-container"></div>');
  await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
  await page.addScriptTag({ path: path.resolve('wwwroot/js/servicio.js') });
  await page.evaluate(() => {
    window.Peticiones = { enviar: async () => ({ text: async () => '<form id="servicio-form" data-validate="true"><label for="Probe">Campo *</label><input id="Probe" name="Probe" required></form>' }) };
    window.loadServicioForm('1');
  });
  await expect(page.locator('#Probe')).toHaveAttribute('data-form-validation-initialized', 'true');
  await page.locator('#Probe').dispatchEvent('blur');
  await expect(page.locator('#Probe-validation-error')).toHaveText('Campo es obligatorio');
  await expect(page.locator('label [aria-hidden="true"]')).toHaveCount(1);
});
