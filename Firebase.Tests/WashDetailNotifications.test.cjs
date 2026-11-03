const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

test('wash detail uses shared transport and notifications while preserving token and form body', async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage();
        await page.route('**/Lavados/IniciarServicio', async route => {
            const request = route.request();
            assert.equal(request.headers()['requestverificationtoken'], 'token');
            assert.match(request.postData(), /lavadoId/);
            await route.fulfill({ json: { success: false, message: 'No permitido' } });
        });
        await page.route('http://example.com/', route => route.fulfill({ body: '<html></html>', contentType: 'text/html' }));
        await page.goto('http://example.com/');
        await page.setContent('<input name="__RequestVerificationToken" value="token">');
        await page.addScriptTag({ path: 'wwwroot/js/notifications.js' });
        await page.addScriptTag({ path: 'wwwroot/js/requests.js' });
        await page.addScriptTag({ path: 'wwwroot/js/lavado-detalle.js' });
        await page.evaluate(() => window.iniciarServicioDesdeDetalle('lavado', 'servicio'));
        assert.equal(await page.locator('#app-notifications [data-notification-type="error"] p').textContent(), 'No permitido');
    } finally { await browser.close(); }
});
