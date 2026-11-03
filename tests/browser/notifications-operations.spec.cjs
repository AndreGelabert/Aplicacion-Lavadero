const { test, expect } = require('@playwright/test');
const path = require('node:path');

for (const [view, module] of [['Personal', 'personal'], ['Servicio', 'servicio'], ['PaqueteServicio', 'paquete'], ['Lavados', 'lavado']]) {
  test(`${view}: filter waits for the table and reports HTTP failure`, async ({ page }) => {
    await page.goto('/fixture');
    await page.setContent(`<input id="simple-search"><form id="filterForm"><input name="pageNumber" value="1"><button>Aplicar</button></form><div id="${module}-table-container">original</div>`);
    await page.addScriptTag({ path: path.resolve('wwwroot/js/notifications.js') });
    await page.addScriptTag({ path: path.resolve('wwwroot/js/requests.js') });
    await page.addScriptTag({ path: path.resolve(`wwwroot/js/${({paquete: "paqueteservicio"})[module] || module}.js`) });
    await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
    let release;
    const pending = new Promise(resolve => { release = resolve; });
    await page.route(`**/${view}/*Partial*`, async route => {
      await pending;
      await route.fulfill({ status: 500, body: 'failed' });
    });
    await page.locator('#filterForm button').click();
    await expect(page.getByText('Aplicando filtros…')).toBeVisible();
    await expect(page.getByText('Filtros aplicados.')).toHaveCount(0);
    release();
    await expect(page.getByText('No se pudieron aplicar los filtros.')).toBeVisible();
    await expect(page.locator(`#${module}-table-container`)).toHaveText('original');
  });
}
