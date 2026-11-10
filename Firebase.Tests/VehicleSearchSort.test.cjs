'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
assert.ok(fs.existsSync(require.resolve('@playwright/test')));
const { chromium } = require('@playwright/test');
assert.ok(fs.existsSync(chromium.executablePath()), 'cached Chromium missing');

async function fixture(run) {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    const requests = [], errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.route('**/*', async route => {
      const req = route.request();
      if (req.isNavigationRequest()) return route.fulfill({ contentType: 'text/html', body: '<!doctype html><form><input id="simple-search"></form><form id="filterForm"><input name="estados" value="Activo"><input name="estados" value="Inactivo"></form><div id="vehiculo-table-container"><input id="current-page-value" value="3"><input id="current-sort-by" value="Marca"><input id="current-sort-order" value="desc"></div>' });
      requests.push({ url: new URL(req.url()).pathname + new URL(req.url()).search, method: req.method(), headers: req.headers() });
      return route.fulfill({ contentType: req.url().includes('ObtenerTiposConFormatos') ? 'application/json' : 'text/html', body: req.url().includes('ObtenerTiposConFormatos') ? '[]' : '<span>result</span>' });
    });
    await page.goto('http://fixture.test/Vehiculo/Index');
    for (const file of ['site.js', 'notifications.js', 'requests.js', 'form-validation.js', 'vehiculo.js']) await page.addScriptTag({ path: `wwwroot/js/${file}` });
    await page.evaluate(() => window.PageModules.vehiculos.init());
    await page.waitForTimeout(100);
    requests.length = 0;
    await run(page, requests, errors);
  } finally { await browser.close(); }
}

test('public search sends duplicate filters and current sort', async () => fixture(async (page, requests, errors) => {
  await page.locator('#simple-search').fill(' placa ');
  await page.waitForTimeout(800);
  assert.deepEqual(errors, []);
  assert.equal(requests.length, 1);
  assert.equal(requests[0].url, '/Vehiculo/SearchPartial?estados=Activo&estados=Inactivo&searchTerm=placa&pageNumber=1&sortBy=Marca&sortOrder=desc');
  assert.equal(requests[0].method, 'GET');
  assert.equal(requests[0].headers['x-requested-with'], 'XMLHttpRequest');
}));

test('public existing sort reload uses closure toggle and preserves filters', async () => fixture(async (page, requests, errors) => {
  await page.evaluate(() => window.sortTable('Marca'));
  await page.waitForTimeout(100);
  assert.deepEqual(errors, []);
  assert.equal(requests.length, 1);
  assert.equal(requests[0].url, '/Vehiculo/TablePartial?estados=Activo&estados=Inactivo&searchTerm=&pageNumber=3&sortBy=Marca&sortOrder=asc');
  assert.equal(requests[0].method, 'GET');
  assert.equal(requests[0].headers['x-requested-with'], 'XMLHttpRequest');
  assert.equal(requests[0].headers['cache-control'], 'no-cache');
}));
