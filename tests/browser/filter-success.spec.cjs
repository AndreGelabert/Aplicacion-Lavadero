const { test, expect } = require('@playwright/test');
const path = require('node:path');
const views = [['Auditoria','auditoria','auditoria'], ['Cliente','cliente','cliente'], ['Vehiculo','vehiculo','vehiculo'], ['Personal','personal','personal'], ['Servicio','servicio','servicio'], ['PaqueteServicio','paqueteservicio','paquete'], ['Lavados','lavado','lavado']];
for (const [view, module, container] of views) {
  test(`${view}: filter success follows content update and repeated submit still works`, async ({ page }) => {
    await page.goto('/fixture');
    await page.setContent(`<input id="simple-search"><form id="filterForm"><input name="pageNumber" value="1"><button>Aplicar</button></form><div id="${container}-table-container">Anterior</div>`);
    for (const script of ['notifications','requests',module]) await page.addScriptTag({ path: path.resolve(`wwwroot/js/${script}.js`) });
    await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
    let attempt = 0;
    await page.route(`**/${view}/*Partial*`, route => route.fulfill({ body: `<p>Resultado ${++attempt}</p>` }));
    for (let n = 1; n <= 2; n++) {
      await page.locator('#filterForm button').click();
      await expect(page.locator(`#${container}-table-container`)).toHaveText(`Resultado ${n}`);
      await expect(page.locator('[data-notification-type="success"]')).toHaveCount(n);
      await expect(page.locator('[data-notification-type="loading"]')).toHaveCount(0);
    }
  });
}
