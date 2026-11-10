const { test, expect } = require('@playwright/test');

async function setup(page, module, html, helpers = true) {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route('**/*', route => route.request().url() === 'http://fixture.test/Personal/Index' ? route.fulfill({ status: 200, contentType: 'text/html', body: html }) : route.abort());
  await page.goto('http://fixture.test/Personal/Index');
  await page.evaluate(helpers => {
    Date.now = () => 123;
    window.trace = [];
    window.Peticiones = {
      filtrar: (name, callback) => { trace.push(['filter', name]); return callback(); },
      enviar: (url, options) => { trace.push(['request', url, options]); return Promise.resolve({ ok: true, text: () => Promise.resolve('<input id="current-page-value" value="3">') }); }
    };
    window.Notificaciones = { mostrar: (...args) => trace.push(['message', ...args]), aviso: (...args) => trace.push(['notice', ...args]) };
    if (helpers) {
      window.CommonUtils = { setupDefaultFilterForm: () => trace.push(['form']) };
      window.initDropdowns = () => trace.push(['dropdown']);
    }
  }, helpers);
  await page.addScriptTag({ path: `wwwroot/js/${module}.js` });
  return errors;
}

const request = (url) => ['request', url, { headers: { 'X-Requested-With': 'XMLHttpRequest', 'Cache-Control': 'no-cache' }, cache: 'no-store' }];

test('Roles public paging uses missing defaults and sort toggles', async ({ page }) => {
  const errors = await setup(page, 'roles', '<div id="rol-table-container"></div>');
  await page.evaluate(() => window.changeRolPage(2));
  await expect.poll(() => page.evaluate(() => trace.length)).toBe(1);
  expect(await page.evaluate(() => trace)).toEqual([request('/Rol/TablePartial?searchTerm=&estado=&sortBy=Nombre&sortOrder=asc&pageNumber=2&pageSize=10&_=123')]);
  await page.evaluate(() => { document.body.insertAdjacentHTML('beforeend', '<input id="current-sort-by" value="Nombre"><input id="current-sort-order" value="asc"><input id="rol-search" value="  Ana  "><input id="rol-estado" value="Activo">'); trace.length = 0; window.sortRolTable('Nombre'); });
  await expect.poll(() => page.evaluate(() => trace.length)).toBe(1);
  expect(await page.evaluate(() => [trace, document.querySelector('#current-sort-order').value])).toEqual([[request('/Rol/SearchPartial?searchTerm=Ana&estado=Activo&sortBy=Nombre&sortOrder=desc&pageNumber=1&pageSize=10&_=123')], 'desc']);
  await page.evaluate(() => { trace.length = 0; window.sortRolTable('Estado'); });
  expect(await page.evaluate(() => trace)).toEqual([request('/Rol/SearchPartial?searchTerm=Ana&estado=Activo&sortBy=Estado&sortOrder=asc&pageNumber=1&pageSize=10&_=123')]);
  expect(errors).toEqual([]);
});

test('Roles confirmation labels, null guards and protected role behavior', async ({ page }) => {
  const errors = await setup(page, 'roles', '<div id="rol-delete-modal" class="hidden"></div><input id="rol-delete-id"><span id="rol-delete-text"></span><span id="rol-state-title"></span><button id="rol-delete-confirm-btn"></button>');
  const result = await page.evaluate(() => {
    const button = document.createElement('button'); Object.assign(button.dataset, { id: '7', nombre: 'Operador' });
    const snapshot = () => [document.querySelector('#rol-state-title').textContent, document.querySelector('#rol-delete-confirm-btn').textContent, document.querySelector('#rol-delete-id').value, document.querySelector('#rol-delete-text').textContent, document.querySelector('#rol-delete-modal').dataset.action];
    window.openRolDeleteModal(button); const off = snapshot(); window.openRolReactivateModal(button); const on = snapshot();
    document.querySelector('#rol-state-title').remove(); document.querySelector('#rol-delete-confirm-btn').remove(); window.openRolDeleteModal(button); window.openRolReactivateModal(button);
    button.dataset.protegido = 'true'; window.openRolDeleteModal(button); window.openRolDeleteModal(null);
    return { off, on, trace };
  });
  expect(result).toEqual({ off: ['Confirmar desactivación', 'Confirmar desactivación', '7', '¿Está seguro de que desea desactivar el rol Operador? El rol se conservará como inactivo.', 'desactivar'], on: ['Confirmar reactivación', 'Confirmar reactivación', '7', '¿Está seguro de que desea reactivar el rol Operador?', 'reactivar'], trace: [['notice', 'Acción no permitida: Los roles predeterminados del sistema no pueden ser eliminados']] });
  expect(errors).toEqual([]);
});

test('Personal paging and clear preserve control order and query', async ({ page }) => {
  const errors = await setup(page, 'personal', '<form id="filterForm"><input name="roles" type="checkbox" value="2" checked><input name="estados" type="checkbox" value="Activo" checked></form><input id="simple-search" value=""><input id="current-sort-by" value="NombreCompleto"><input id="current-sort-order" value="asc"><div id="personal-table-container"></div>');
  await page.evaluate(() => window.changePage(2));
  expect(await page.evaluate(() => trace)).toEqual([['filter', 'personal'], request('/Personal/TablePartial?roles=2&estados=Activo&pageNumber=2&sortBy=NombreCompleto&sortOrder=asc&_=123'), ['form'], ['dropdown']]);
  await page.evaluate(() => { trace.length = 0; window.clearPersonalFilters(); });
  await expect.poll(() => page.evaluate(() => document.querySelector('#filterForm').dataset.clearing)).toBe('false');
  expect(await page.evaluate(() => trace)).toEqual([['form'], ['dropdown'], ['filter', 'personal'], request('/Personal/TablePartial?pageNumber=1&sortBy=NombreCompleto&sortOrder=asc&_=123'), ['form'], ['dropdown']]);
  expect(errors).toEqual([]);
});

test('Personal optional controls and failed request preserve error propagation', async ({ page }) => {
  const errors = await setup(page, 'personal', '<form id="filterForm"></form><div id="personal-table-container"></div>', false);
  await page.evaluate(() => window.changePage(1));
  expect(await page.evaluate(() => trace)).toEqual([['filter', 'personal'], request('/Personal/TablePartial?pageNumber=1&sortBy=NombreCompleto&sortOrder=asc&_=123')]);
  const error = await page.evaluate(async () => { window.Peticiones.enviar = () => Promise.resolve({ ok: false, status: 503 }); try { await window.changePage(4); } catch (e) { return e.message; } });
  expect(error).toBe('HTTP 503');
  expect(errors).toEqual([]);
});
