const path = require('node:path');
const { test, expect } = require('@playwright/test');
const root = path.resolve(__dirname, '../..');

async function setup(page) {
  await page.goto('/fixture');
  await page.setContent('<html><body><form><input name="__RequestVerificationToken" value="fixture-token"></form><div id="table">Anterior</div></body></html>');
  await page.addScriptTag({ path: path.join(root, 'wwwroot/js/notifications.js') });
  await page.addScriptTag({ path: path.join(root, 'wwwroot/js/requests.js') });
}

test('request helper preserves responses and body, adds same-origin AJAX header, never retries', async ({ page }) => {
  await setup(page);
  let calls = 0;
  await page.route('**/probe', async route => {
    calls++;
    expect(route.request().headers()['x-requested-with']).toBe('XMLHttpRequest');
    expect(route.request().postData()).toBe('example=value');
    await route.fulfill({ status: 422, json: { success: false, message: 'Revisar', errors: { Nombre: 'Duplicado' } } });
  });
  const result = await page.evaluate(async () => {
    const response = await Peticiones.enviar('/probe', { method: 'POST', body: 'example=value' });
    return { status: response.status, data: await response.json() };
  });
  expect(result).toEqual({ status: 422, data: { success: false, message: 'Revisar', errors: { Nombre: 'Duplicado' } } });
  expect(calls).toBe(1);
  await expect(page.locator('#app-notifications')).toHaveCount(0);
});

test('filter lifecycle waits for DOM update and replaces applying notice with success', async ({ page }) => {
  await setup(page);
  await page.evaluate(() => {
    window.finishFilter = null;
    window.filterPromise = Peticiones.filtrar('table', async () => {
      await new Promise(resolve => { window.finishFilter = resolve; });
      document.getElementById('table').textContent = 'Actualizada';
    });
  });
  await expect(page.locator('[data-notification-type="loading"]')).toContainText('Aplicando filtros…');
  await expect(page.locator('[data-notification-type="success"]')).toHaveCount(0);
  await page.evaluate(async () => { finishFilter(); await filterPromise; });
  await expect(page.locator('#table')).toHaveText('Actualizada');
  await expect(page.locator('[data-notification-type="loading"]')).toHaveCount(0);
  await expect(page.locator('[data-notification-type="success"]')).toContainText('Filtros aplicados.');
});

test('filter rejection removes progress and shows failure without changing table', async ({ page }) => {
  await setup(page);
  const result = await page.evaluate(() => Peticiones.filtrar('table', async () => { throw new Error('HTTP 500'); }));
  expect(result).toBe(false);
  await expect(page.locator('#table')).toHaveText('Anterior');
  await expect(page.locator('[data-notification-type="error"]')).toContainText('No se pudieron aplicar los filtros.');
  await expect(page.locator('[data-notification-type="success"], [data-notification-type="loading"]')).toHaveCount(0);
});

test('cancelled filters do not report success or failure', async ({ page }) => {
  await setup(page);
  await page.evaluate(() => Peticiones.filtrar('table', async () => { throw new DOMException('Cancelled', 'AbortError'); }));
  await expect(page.locator('#app-notifications > *')).toHaveCount(0);
});

test('HTTP helper does not forward AJAX credentials or headers to external endpoints', async ({ page }) => {
  await setup(page);
  await page.route('https://external.invalid/probe', async route => {
    expect(route.request().headers()['x-requested-with']).toBeUndefined();
    expect(route.request().headers()['requestverificationtoken']).toBeUndefined();
    await route.fulfill({ json: { ok: true }, headers: { 'Access-Control-Allow-Origin': '*' } });
  });
  expect(await page.evaluate(async () => (await Peticiones.enviar('https://external.invalid/probe')).ok)).toBe(true);
});
