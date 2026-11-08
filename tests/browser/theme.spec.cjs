const fs = require('node:fs');
const path = require('node:path');
const { test, expect } = require('@playwright/test');
const root = path.resolve(__dirname, '../..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const selector = read('Views/Shared/_ThemeSelector.cshtml');
const login = read('Views/Login/Index.cshtml').split('<section')[1].split('</section>')[0];

async function setup(page, type) {
  await page.goto('/fixture');
  const content = type === 'login'
    ? '<section' + login.replace('@await Html.PartialAsync("_ThemeSelector")', selector) + '</section>'
    : `<nav class="bg-white dark:bg-gray-800">${selector}</nav>`;
  await page.setContent(`<!doctype html><html lang="es"><head></head><body>${content}</body></html>`);
  await page.addStyleTag({ path: path.join(root, 'wwwroot/css/app.min.css') });
  await page.addScriptTag({ path: path.join(root, 'wwwroot/js/theme.js') });
}

for (const type of ['login', 'shell']) {
  test(`${type} selector follows system and persists explicit selection`, async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await setup(page, type);
    const control = page.getByLabel('Tema');
    await expect(control).toHaveValue('system');
    await expect(page.locator('html')).toHaveClass(/dark/);
    await control.selectOption('light');
    await expect(page.locator('html')).not.toHaveClass(/dark/);
    expect(await page.evaluate(() => localStorage.getItem('theme'))).toBe('light');
    await setup(page, type);
    await expect(page.getByLabel('Tema')).toHaveValue('light');
    await page.getByLabel('Tema').selectOption('system');
    await expect(page.locator('html')).toHaveClass(/dark/);
    await page.emulateMedia({ colorScheme: 'light' });
    await expect(page.locator('html')).not.toHaveClass(/dark/);
  });
}

test('login rendered markup has explicit readable light and dark surfaces', async ({ page }) => {
  await setup(page, 'login');
  const control = page.getByLabel('Tema');
  await control.selectOption('light');
  await expect(page.locator('#email')).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await control.selectOption('dark');
  await expect(page.locator('#email')).toHaveCSS('background-color', 'rgb(55, 65, 81)');
  await expect(page.locator('label[for="email"]')).toHaveCSS('color', 'rgb(255, 255, 255)');
  await expect(page.locator('#registerModal .relative.bg-white')).toHaveCSS('background-color', 'rgb(31, 41, 55)');
  await expect(page.locator('#nombreCompleto')).toHaveCSS('background-color', 'rgb(55, 65, 81)');
  await expect(page.locator('#forgotPasswordModal > div')).toHaveCSS('background-color', 'rgb(31, 41, 55)');
  await expect(page.locator('#google-login-button')).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await expect(page.locator('label[for="email"] span')).toHaveCSS('color', 'rgb(249, 128, 128)');
});
