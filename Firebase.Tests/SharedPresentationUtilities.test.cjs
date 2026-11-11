'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');

const fixture = '<input id="pagoLavadoId"><span id="pagoTotal"></span><span id="pagoPagado"></span><span id="pagoRestante"></span><input id="montoInput"><input id="notasPago"><div id="pagoModal" class="hidden"></div>';

test('shared ARS formatter retains Intl behavior and owns both implementations', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent('');
    await page.addScriptTag({ path: 'wwwroot/js/site.js' });
    const values = await page.evaluate(() => [0, -1234.5, 1.235, '42.5', 'invalid', null, undefined].map(value => [window.SiteModule.formatCurrency(value), new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value)]));
    for (const [actual, expected] of values) assert.equal(actual, expected);
    for (const file of ['wwwroot/js/lavado.js', 'wwwroot/js/lavado-detalle.js']) {
      const source = fs.readFileSync(file, 'utf8');
      assert.doesNotMatch(source, /function formatCurrency\s*\(/);
      assert.match(source, /SiteModule\.formatCurrency/);
    }
  } finally { await browser.close(); }
});

test('shared DOM text escaper retains the three equivalent module contracts', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent('');
    await page.addScriptTag({ path: 'wwwroot/js/site.js' });
    const values = await page.evaluate(() => [
      window.SiteModule.escapeHtmlText('<script>alert("x")</script>'),
      window.SiteModule.escapeHtmlText(''),
      window.SiteModule.escapeHtmlText(null),
      window.SiteModule.escapeHtmlText(42)
    ]);
    assert.deepEqual(values, ['&lt;script&gt;alert("x")&lt;/script&gt;', '', '', '42']);
    for (const file of ['cliente.js', 'lavado.js', 'vehiculo.js']) {
      const source = fs.readFileSync(`wwwroot/js/${file}`, 'utf8');
      assert.doesNotMatch(source, /function escapeHtml\s*\(/);
      assert.match(source, /SiteModule\.escapeHtmlText/);
    }
  } finally { await browser.close(); }
});

test('shared form serialization preserves repeated values and sort defaults', async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.setContent('<form id="filters"><input name="estados" value="Activo"><input name="estados" value="Pendiente"><input name="q" value="lavado"></form>');
    await page.addScriptTag({ path: 'wwwroot/js/site.js' });
    const result = await page.evaluate(() => {
      const params = new URLSearchParams();
      window.SiteModule.appendFormDataToParams(params, document.getElementById('filters'));
      return {
        query: params.toString(),
        defaultSort: window.SiteModule.getSortState('Nombre', 'asc'),
        explicitSort: (() => {
          document.body.insertAdjacentHTML('beforeend', '<input id="current-sort-by" value="Fecha"><input id="current-sort-order" value="desc">');
          return window.SiteModule.getSortState('Nombre', 'asc');
        })()
      };
    });
    assert.equal(result.query, 'estados=Activo&estados=Pendiente&q=lavado');
    assert.deepEqual(result.defaultSort, { sortBy: 'Nombre', sortOrder: 'asc' });
    assert.deepEqual(result.explicitSort, { sortBy: 'Fecha', sortOrder: 'desc' });
  } finally { await browser.close(); }
});

for (const module of ['lavado.js', 'lavado-detalle.js']) test(`${module} payment modal preserves values and delegates currency`, async () => {
  const browser = await chromium.launch();
  try {
    const page = await browser.newPage();
    await page.route('**/*', route => route.abort());
    await page.setContent(fixture);
    await page.addScriptTag({ path: 'wwwroot/js/site.js' });
    await page.addScriptTag({ path: `wwwroot/js/${module}` });
    const result = await page.evaluate(() => {
      const original = window.SiteModule.formatCurrency;
      const calls = [];
      window.SiteModule.formatCurrency = value => { calls.push(value); return original(value); };
      window.abrirModalPago('wash', 1234.5, 34.25);
      const get = id => document.getElementById(id);
      return { calls, texts: ['pagoTotal', 'pagoPagado', 'pagoRestante'].map(id => get(id).textContent), id: get('pagoLavadoId').value, amount: get('montoInput').value, max: get('montoInput').max, remaining: get('montoInput').dataset.restante, hidden: get('pagoModal').classList.contains('hidden') };
    });
    const format = value => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value);
    assert.deepEqual(result, { calls: [1234.5, 34.25, 1200.25], texts: [format(1234.5), format(34.25), format(1200.25)], id: 'wash', amount: '1200.25', max: '1200.25', remaining: '1200.25', hidden: false });
  } finally { await browser.close(); }
});
