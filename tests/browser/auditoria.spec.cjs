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

test('fixture: metadata is inline with bold labels', async ({ page }) => {
  await page.locator('[data-audit-detail-id="change"]').click();
  const label = page.locator('#audit-detail-meta dt').filter({ hasText: 'Usuario' });
  await expect(label).toHaveText('Usuario:');
  await expect(label).toHaveCSS('font-weight', '700');
  const value = label.locator('xpath=following-sibling::dd[1]');
  await expect(value).toHaveText('fixture@example.invalid');
  const a = await label.boundingBox(), b = await value.boundingBox();
  expect(Math.abs(a.y - b.y)).toBeLessThan(3);
});

test('fixture: dialog backdrop dims the page', async ({ page }) => {
  await page.locator('[data-audit-detail-id="change"]').click();
  await expect.poll(() => page.locator('#audit-detail-dialog').evaluate(el => getComputedStyle(el, '::backdrop').backgroundColor)).toBe('rgba(17, 24, 39, 0.5)');
});

test('fixture: close control is an accessible X in the header', async ({ page }) => {
  await page.locator('[data-audit-detail-id="change"]').click();
  const close = page.getByRole('button', { name: 'Cerrar modal', exact: true });
  await expect(close.locator('svg')).toBeVisible();
  const header = await page.locator('#audit-detail-title').boundingBox();
  const button = await close.boundingBox();
  expect(Math.abs(header.y - button.y)).toBeLessThan(12);
  await close.click();
  await expect(page.locator('#audit-detail-dialog')).not.toBeVisible();
  await expect(page.locator('[data-audit-detail-id="change"]')).toBeFocused();
});

test('fixture: comparison cells have visible grid borders', async ({ page }) => {
  await page.locator('[data-audit-detail-id="change"]').click();
  await expect(page.locator('#audit-detail-rows tr')).toHaveCount(2);
  for (const cell of await page.locator('#audit-detail-dialog th, #audit-detail-dialog td').all()) {
    await expect(cell).toHaveCSS('border-top-width', '1px');
    await expect(cell).toHaveCSS('border-right-style', 'solid');
  }
});

test('fixture: background scroll locks only while dialog is open, including repeated opens', async ({ page }) => {
  await page.evaluate(() => { document.body.style.minHeight = '3000px'; });
  for (const closeWithEscape of [false, true]) {
    await page.locator('[data-audit-detail-id="change"]').click();
    await expect(page.locator('#audit-detail-rows tr')).toHaveCount(2);
    await expect(page.locator('html')).toHaveCSS('overflow-y', 'hidden');
    await expect(page.locator('body')).toHaveCSS('overflow-y', 'hidden');
    const before = await page.evaluate(() => window.scrollY);
    await page.mouse.move(5, 5);
    await page.mouse.wheel(0, 600);
    await page.evaluate(() => new Promise(resolve => {
      let frames = 0;
      function next() { if (++frames === 10) resolve(); else requestAnimationFrame(next); }
      requestAnimationFrame(next);
    }));
    expect(await page.evaluate(() => window.scrollY)).toBe(before);
    await page.locator('#audit-detail-meta').evaluate(el => { el.style.height = '1500px'; });
    const dialog = page.locator('#audit-detail-dialog');
    await expect(dialog).toHaveCSS('overflow-y', 'auto');
    await dialog.hover();
    await page.mouse.wheel(0, 400);
    await expect.poll(() => dialog.evaluate(el => el.scrollTop)).toBeGreaterThan(0);
    if (closeWithEscape) await page.keyboard.press('Escape');
    else await page.locator('#audit-detail-close').click();
    await expect(dialog).not.toBeVisible();
    await expect(page.locator('html')).not.toHaveCSS('overflow-y', 'hidden');
    await expect(page.locator('body')).not.toHaveCSS('overflow-y', 'hidden');
    await page.mouse.move(5, 5);
    await page.mouse.wheel(0, 300);
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(before);
    await page.evaluate(() => { window.scrollTo(0, 0); document.querySelector('#audit-detail-meta').style.height = ''; });
  }
});

test('fixture: replaced AJAX table retains delegated detail click', async ({ page }) => {
  await page.locator('#filterForm button').click();
  const replacement = page.locator('[data-audit-detail-id="replacement"]');
  await expect(replacement).toBeVisible();
  await expect(page.locator('#auditoria-table-container')).toHaveAttribute('data-current-page', '2');
  await replacement.click();
  await expect(page.locator('#audit-detail-rows tr')).toHaveText(['Campounodos']);
});
