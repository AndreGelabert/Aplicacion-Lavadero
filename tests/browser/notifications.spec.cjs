const path = require('node:path');
const fs = require('node:fs');
const { test, expect } = require('@playwright/test');
const root = path.resolve(__dirname, '../..');

async function setup(page, roles = false) {
  await page.goto('/fixture');
  await page.setContent(`<!doctype html><html lang="es"><head></head><body>
    <main style="overflow:hidden;transform:translate(0)">
      <form id="rol-form" action="/Rol/CrearRolAjax"><input name="Nombre" value="Operador"><span data-valmsg-for="Nombre"></span><button type="submit">Guardar</button></form>
      <div id="rol-form-container"></div><span id="rol-form-title"></span>
      <div id="rol-table-container" data-current-page="1"></div>
    </main><div id="overlay" style="position:fixed;inset:0;z-index:60;pointer-events:none"></div>
    </body></html>`);
  await page.addStyleTag({ path: path.join(root, 'wwwroot/css/notifications.css') });
  await page.addScriptTag({ path: path.join(root, 'wwwroot/js/notifications.js') });
  await page.addScriptTag({ path: path.join(root, 'wwwroot/js/requests.js') });
  if (roles) {
    await page.addScriptTag({ path: path.join(root, 'wwwroot/js/roles.js') });
    await page.evaluate(() => window.PageModules.roles.init());
  }
}

test('global notifications stack safely above views and can be dismissed', async ({ page }) => {
  await setup(page);
  for (const [method, type] of [['exito', 'success'], ['error', 'error'], ['aviso', 'warning'], ['info', 'info']]) {
    await page.evaluate(({ method }) => window.Notificaciones[method]('<img src=x onerror=alert(1)>'), { method });
    await expect(page.locator(`[data-notification-type="${type}"]`)).toContainText('<img src=x onerror=alert(1)>');
  }
  await expect(page.locator('#app-notifications > *')).toHaveCount(4);
  await expect(page.locator('#app-notifications img')).toHaveCount(0);
  expect(await page.locator('#app-notifications').evaluate(el => el.parentElement === document.body)).toBe(true);
  const card = page.locator('[data-notification-type="error"]');
  await expect(card).toHaveAttribute('role', 'alert');
  const box = await card.boundingBox();
  expect(await page.evaluate(({ x, y }) => document.elementFromPoint(x, y)?.closest('[data-notification-type]')?.dataset.notificationType, { x: box.x + 10, y: box.y + 10 })).toBe('error');
  await card.getByRole('button', { name: 'Cerrar notificación' }).click();
  await expect(card).toHaveCount(0);
});

test('success expires while errors and warnings remain; mobile does not overflow', async ({ page }) => {
  await setup(page);
  await page.clock.install();
  await page.setViewportSize({ width: 375, height: 700 });
  await page.evaluate(() => {
    Notificaciones.exito('Guardado');
    Notificaciones.error('Error importante');
    Notificaciones.aviso('Revisar permisos');
    Notificaciones.info('Información');
  });
  await page.clock.fastForward(6000);
  await expect(page.locator('[data-notification-type="success"]')).toHaveCount(0);
  await expect(page.locator('[data-notification-type="info"]')).toHaveCount(0);
  await expect(page.locator('#app-notifications > *')).toHaveCount(2);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test('roles keeps field validation and displays the server message globally', async ({ page }) => {
  await setup(page, true);
  await page.route('**/Rol/CrearRolAjax', route => route.fulfill({ json: { success: false, message: 'Revise los errores del formulario.', errors: { Nombre: 'El rol ya existe.' } } }));
  await page.evaluate(() => submitRolAjax(document.getElementById('rol-form')));
  await expect(page.locator('[role="alert"]')).toHaveText(/Revise los errores del formulario\./);
  await expect(page.locator('[data-valmsg-for="Nombre"]')).toHaveText('El rol ya existe.');
  await expect(page.locator('[type="submit"]')).toBeEnabled();
});

test('server flash messages render once as text on the next page', async ({ page }) => {
  await setup(page);
  await page.evaluate(() => {
    const server = document.createElement('div');
    server.id = 'server-notifications';
    server.hidden = true;
    const entry = document.createElement('span');
    entry.dataset.notificationType = 'success';
    entry.textContent = 'Rol actualizado <script>no ejecutar</script>';
    server.appendChild(entry);
    document.body.appendChild(server);
  });
  await page.addScriptTag({ path: path.join(root, 'wwwroot/js/notifications.js') });
  await expect(page.locator('[data-notification-type="success"]')).toContainText('Rol actualizado <script>no ejecutar</script>');
  await expect(page.locator('#server-notifications > *')).toHaveCount(0);
  await page.addScriptTag({ path: path.join(root, 'wwwroot/js/notifications.js') });
  await expect(page.locator('#app-notifications > *')).toHaveCount(1);
});

test('roles load failures use global errors and protected-role guards still stop the action', async ({ page }) => {
  await setup(page, true);
  await page.route('**/Rol/TablePartial?*', route => route.fulfill({ status: 500 }));
  await page.evaluate(() => changeRolPage(2));
  await expect(page.locator('[role="alert"]')).toContainText('Error al cargar los roles.');
  await page.evaluate(() => {
    const button = document.createElement('button');
    button.dataset.protegido = 'true';
    openRolDeleteModal(button);
  });
  await expect(page.locator('[data-notification-type="warning"]')).toContainText('Los roles predeterminados del sistema no pueden ser eliminados');
});

test('shared layout loads the component before page modules and roles has no private notice containers', async () => {
  const layout = fs.readFileSync(path.join(root, 'Views/Shared/_Layout.cshtml'), 'utf8');
  expect(layout.indexOf('~/js/notifications.js')).toBeLessThan(layout.indexOf('RenderSectionAsync'));
  expect(layout).toContain('~/css/notifications.css');
  const view = fs.readFileSync(path.join(root, 'Views/Rol/Index.cshtml'), 'utf8');
  expect(view).not.toContain('rol-toast-container');
  expect(view).not.toContain('rol-block-modal');
  expect(fs.readFileSync(path.join(root, 'Views/Rol/_RolForm.cshtml'), 'utf8')).not.toContain('rol-form-message');
});
