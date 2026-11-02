const { test, expect } = require('@playwright/test');

// This is a local DOM fixture, not an authenticated or Razor integration test.
test.beforeEach(async ({ page }) => {
  await page.route('**/*', async route => {
    const url = new URL(route.request().url());
    if (url.origin !== 'http://127.0.0.1:41783') return route.abort();
    if (url.pathname !== '/Auditoria/Detalle') return route.continue();
    const id = url.searchParams.get('id');
    if (id === 'error') return route.fulfill({ status: 500, body: 'unavailable' });
    const details = {
      change: { userEmail: 'fixture@example.invalid', action: 'Editar', antes: { Nombre: 'Anterior', Extra: '<img src=x onerror=alert(1)>' }, despues: { Nombre: 'Nuevo', Extra: null } },
      replacement: { antes: { Campo: 'uno' }, despues: { Campo: 'dos' } },
      legacy: { antes: null, despues: null },
      session: { antes: {}, despues: {}, comparacionNoAplica: true },
    };
    return route.fulfill({ json: details[id] });
  });
  await page.goto('/fixture');
});

test('fixture: native dialog renders values as text and Escape restores focus', async ({ page }) => {
  const trigger = page.locator('[data-audit-detail-id="change"]');
  await trigger.click();
  const dialog = page.locator('#audit-detail-dialog');
  await expect(dialog).toHaveJSProperty('open', true);
  await expect(page.locator('#audit-detail-meta')).toContainText('fixture@example.invalid');
  const rows = page.locator('#audit-detail-rows tr');
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0)).toHaveText('NombreAnteriorNuevo');
  await expect(rows.nth(1)).toContainText('<img src=x onerror=alert(1)>');
  await expect(rows.locator('img')).toHaveCount(0);
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveJSProperty('open', false);
  await expect(trigger).toBeFocused();
});

for (const [id, message] of [
  ['legacy', 'Detalle histórico no disponible para este registro.'],
  ['session', 'La comparación no aplica a eventos de sesión.'],
  ['error', 'No se pudo cargar el detalle de auditoría.'],
]) {
  test(`fixture: ${id} displays status`, async ({ page }) => {
    await page.locator('[data-audit-detail-id]').evaluate((el, value) => { el.dataset.auditDetailId = value; }, id);
    await page.locator('[data-audit-detail-id]').click();
    await expect(page.locator('#audit-detail-status')).toHaveText(message);
    await expect(page.locator('#audit-detail-rows tr')).toHaveCount(0);
    await page.locator('#audit-detail-close').click();
    await expect(page.locator('[data-audit-detail-id]')).toBeFocused();
  });
}

test('fixture: replaced AJAX table retains delegated detail click', async ({ page }) => {
  await page.locator('#filterForm button').click();
  const replacement = page.locator('[data-audit-detail-id="replacement"]');
  await expect(replacement).toBeVisible();
  await expect(page.locator('#auditoria-table-container')).toHaveAttribute('data-current-page', '2');
  await replacement.click();
  await expect(page.locator('#audit-detail-rows tr')).toHaveText(['Campounodos']);
});
