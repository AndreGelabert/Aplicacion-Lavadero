const fs = require('node:fs');
const path = require('node:path');
const { test, expect } = require('@playwright/test');

test('login validates immediately and preserves password toggle and native POST', async ({ page }) => {
  const view = fs.readFileSync('Views/Login/Index.cshtml', 'utf8');
  const form = view.slice(view.indexOf('<form method="post" id="loginForm"'), view.indexOf('</form>') + 7).replace(/@if \(ViewBag.Error[\s\S]*?\n            }/, '');
  const register = view.slice(view.indexOf('<form class="p-4" id="registerForm"'), view.indexOf('</form>', view.indexOf('<form class="p-4" id="registerForm"')) + 7)
    .replace(/@if \(ViewData\.ModelState[\s\S]*?\n\s*}/g, '<span class="server-error">Error del servidor</span>');
  const requests = [];
  await page.route('**/*', async route => {
    const request = route.request();
    if (request.url() === 'http://example.test/Login/Login') {
      requests.push({ method: request.method(), body: request.postData() });
      await route.fulfill({ status: 200, contentType: 'text/html', body: 'OK' });
    } else if (request.url() === 'http://example.test/') {
      await route.fulfill({ status: 200, contentType: 'text/html', body: '<html></html>' });
    } else await route.abort();
  });
  await page.goto('http://example.test/');
  await page.setContent(form + register);
  await page.locator('#loginForm').evaluate(form => {
    form.insertAdjacentHTML('beforeend', '<input type="hidden" name="__RequestVerificationToken" value="fictional-token">');
  });
  await page.addScriptTag({ path: path.resolve('wwwroot/js/login.js') });
  await page.addScriptTag({ path: path.resolve('wwwroot/js/form-validation.js') });
  await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
  await expect(page.locator('#registerForm .server-error').first()).toHaveText('Error del servidor');
  await expect(page.locator('#registerEmail')).not.toHaveAttribute('data-form-validation-initialized');
  await page.locator('#email').fill('test@example.com');
  await page.locator('#password').fill('   ');
  await page.locator('#loginForm').evaluate(form => form.requestSubmit());
  expect(requests).toHaveLength(0);
  await expect(page.locator('#email')).toHaveValue('test@example.com');
  await expect(page.locator('#password')).toHaveValue('   ');
  await expect(page.locator('#password-validation-error')).toHaveText('La contraseña es obligatoria');
  await page.locator('#email').fill('');
  await page.locator('#email').dispatchEvent('input');
  await expect(page.locator('#email-validation-error')).toHaveText('El correo electrónico es obligatorio');
  await page.locator('#email').fill('test@example.com');
  await expect(page.locator('#email-validation-error')).toHaveText('');
  await page.locator('#password').fill('x');
  await page.locator('#togglePasswordIcon').click();
  await expect(page.locator('#password')).toHaveAttribute('type', 'text');
  await page.locator('#loginForm').evaluate(form => form.requestSubmit());
  await expect.poll(() => requests.length).toBe(1);
  expect(requests[0].method).toBe('POST');
  expect(Object.fromEntries(new URLSearchParams(requests[0].body))).toEqual({ email: 'test@example.com', password: 'x', __RequestVerificationToken: 'fictional-token' });
});
