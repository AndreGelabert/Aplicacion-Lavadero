const path = require('node:path');
const { test, expect } = require('@playwright/test');

test('reinitialization retains server errors and binds dynamically added fields once', async ({ page }) => {
  await page.setContent('<form><label for="a">Nombre <span class="text-red-600">*</span></label><input id="a" name="a" required><span data-valmsg-for="a">Error del servidor</span></form>');
  await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
  await page.evaluate(() => {
    const form = document.querySelector('form');
    FormValidation.init(form);
    form.insertAdjacentHTML('beforeend', '<label for="b">Otro *</label><input id="b" required>');
    FormValidation.init(form);
    FormValidation.init(form);
  });
  await expect(page.locator('[data-valmsg-for="a"]')).toHaveText('Error del servidor');
  await expect(page.locator('label span')).toHaveCount(2);
  await expect(page.locator('label span').first()).toHaveClass(/dark:text-red-400/);
  await page.addStyleTag({ path: path.resolve('wwwroot/css/app.min.css') });
  const marker = page.locator('label span').first();
  await expect(marker).toHaveCSS('color', 'rgb(224, 36, 36)');
  await page.locator('html').evaluate(element => element.classList.add('dark'));
  await expect(marker).toHaveCSS('color', 'rgb(249, 128, 128)');
  await page.locator('#b').focus();
  await page.locator('#b').blur();
  await expect(page.locator('#b-validation-error')).toHaveText('Otro es obligatorio');
  await page.locator('#b').fill('valor');
  await expect(page.locator('#b-validation-error')).toHaveText('');
});
