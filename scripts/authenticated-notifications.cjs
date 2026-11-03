'use strict';
// Runs only against an already seeded, disposable local emulator application.
const assert = require('node:assert/strict');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');
(async () => {
  assert.equal(process.env.ISOLATED_NOTIFICATION_TESTS, '1', 'Use the disposable PowerShell runner');
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.route('**/*', route => {
      const url = new URL(route.request().url());
      if (url.href === 'https://cdn.jsdelivr.net/npm/flowbite@2.5.2/dist/flowbite.min.js')
        return route.fulfill({ path: path.resolve('.local/notification-proof/flowbite.min.js'), contentType: 'application/javascript' });
      return url.hostname === '127.0.0.1' ? route.continue() : route.abort();
    });
    await page.goto('http://127.0.0.1:5200/Login');
    await page.evaluate(async password => {
      const body = new URLSearchParams({ Email: 'admin@local.test', Password: password });
      const response = await fetch('/Login/Login', { method: 'POST', body });
      if (!response.ok || response.url.includes('/Login')) throw Error('Local login failed');
    }, process.env.LOCAL_EMULATOR_PASSWORD);
    await page.goto('http://127.0.0.1:5200/Rol');
    assert.equal(await page.locator('a[href="/Configuracion"]').count() > 0, true);
    async function save(remove) {
      await page.evaluate(() => window.openRolForm('local-seed-rol'));
      await page.locator('#rol-form[data-edit="true"]').waitFor();
      await page.evaluate(remove => {
        for (const input of document.querySelectorAll('#rol-form input[name="Permisos"]')) {
          if (input.value.startsWith('Configuracion.') || (remove && input.value.startsWith('Roles.'))) input.checked = false;
        }
      }, remove);
      let observed;
      await page.route('**/Rol/ActualizarRolAjax', async route => {
        const response = await route.fetch();
        observed = { status: response.status(), data: await response.json() };
        await route.fulfill({ response });
      }, { times: 1 });
      const navigation = page.waitForEvent('framenavigated', frame => frame === page.mainFrame());
      await Promise.all([navigation, page.locator('#rol-form button[type="submit"]').click()]);
      await page.waitForLoadState('domcontentloaded');
      assert.equal(observed.status, 200);
      assert.equal(observed.data.success, true);
    }
    await save(false);
    assert.equal(new URL(page.url()).pathname, '/Rol');
    assert.equal(await page.locator('a[href="/Configuracion"]').count(), 0, 'Menu regenerated after removing configuration permission');
    await page.getByText('Rol actualizado exitosamente', { exact: true }).first().waitFor();
    await page.reload();
    assert.equal(await page.getByText('Rol actualizado exitosamente', { exact: true }).count(), 0, 'TempData consumed once');
    await save(true);
    assert.equal(new URL(page.url()).pathname, '/Home/AccessDenied');
    const denied = await page.request.get('http://127.0.0.1:5200/Rol', { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
    assert.equal(denied.status(), 403);
    const final = await page.request.get('http://127.0.0.1:5200/Home/AccessDenied');
    assert.equal(final.status(), 403);
    console.log('PASS authenticated role save, real reload, regenerated sidebar, one-shot TempData, self-revocation redirect and HTTP 403');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
