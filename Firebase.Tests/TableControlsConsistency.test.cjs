'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

test('Roles public sort defaults remain intact without hidden controls', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.setContent('<div id="rol-table-container"></div>');
    await page.evaluate(() => {
      Date.now = () => 123;
      window.calls = [];
      window.Peticiones = { enviar: (url, options) => { calls.push([url, options]); return Promise.resolve({ ok: true, text: () => Promise.resolve('') }); } };
    });
    await page.addScriptTag({ path: 'wwwroot/js/roles.js' });
    await page.evaluate(() => window.sortRolTable('Nombre'));
    assert.deepEqual(await page.evaluate(() => calls), [['/Rol/TablePartial?searchTerm=&estado=&sortBy=Nombre&sortOrder=asc&pageNumber=1&pageSize=10&_=123', { headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' }, cache: 'no-store' }]]);
    assert.deepEqual(errors, []);
  } finally { await browser.close(); }
});
