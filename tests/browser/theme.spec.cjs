const fs = require('node:fs');
const path = require('node:path');
const { test, expect } = require('@playwright/test');
const root = path.resolve(__dirname, '../..');
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
/** Strip Razor server-side comments (@* ... *@) so raw .cshtml is valid HTML in fixture. */
const stripRazor = s => s.replace(/@\*[\s\S]*?\*@/g, '');
const selector = stripRazor(read('Views/Shared/_ThemeSelector.cshtml'));
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

/** Locate the theme toggle button by its accessible label. */
const toggle = page => page.getByRole('switch', { name: /cambiar tema/i });

/** Force the page into a given theme by clicking the toggle if needed. */
async function ensureTheme(page, theme) {
  const html = page.locator('html');
  const isDark = await html.evaluate(el => el.classList.contains('dark'));
  if ((theme === 'dark') !== isDark) await toggle(page).click();
}

for (const type of ['login', 'shell']) {
  test(`${type} toggle follows OS and persists explicit selection`, async ({ page }) => {
    // Start: OS dark, no saved preference → should be dark.
    await page.emulateMedia({ colorScheme: 'dark' });
    await setup(page, type);
    const btn = toggle(page);
    await expect(page.locator('html')).toHaveClass(/dark/);
    await expect(btn).toHaveAttribute('aria-checked', 'true');

    // Click → light.
    await btn.click();
    await expect(page.locator('html')).not.toHaveClass(/dark/);
    await expect(btn).toHaveAttribute('aria-checked', 'false');
    expect(await page.evaluate(() => localStorage.getItem('theme'))).toBe('light');

    // Reload — saved 'light' should survive; OS dark should not override.
    await setup(page, type);
    await expect(page.locator('html')).not.toHaveClass(/dark/);
    await expect(toggle(page)).toHaveAttribute('aria-checked', 'false');

    // Clear preference, return to OS tracking.
    await page.evaluate(() => localStorage.removeItem('theme'));
    await setup(page, type);
    // OS is still dark → dark class expected.
    await expect(page.locator('html')).toHaveClass(/dark/);
    // OS change → light → no dark class (no explicit preference).
    await page.emulateMedia({ colorScheme: 'light' });
    await expect(page.locator('html')).not.toHaveClass(/dark/);
  });
}

test('toggle is keyboard-focusable and has role switch', async ({ page }) => {
  await setup(page, 'shell');
  const btn = toggle(page);
  await btn.focus();
  await expect(btn).toBeFocused();
  await expect(btn).toHaveAttribute('role', 'switch');
});

test('Space key actuates toggle and updates aria-checked', async ({ page }) => {
  // Start light (OS light, no saved pref).
  await page.emulateMedia({ colorScheme: 'light' });
  await setup(page, 'shell');
  const btn = toggle(page);
  await btn.focus();
  await expect(page.locator('html')).not.toHaveClass(/dark/);
  await expect(btn).toHaveAttribute('aria-checked', 'false');
  // Space → dark.
  await page.keyboard.press('Space');
  await expect(page.locator('html')).toHaveClass(/dark/);
  await expect(btn).toHaveAttribute('aria-checked', 'true');
  // Space again → light.
  await page.keyboard.press('Space');
  await expect(page.locator('html')).not.toHaveClass(/dark/);
  await expect(btn).toHaveAttribute('aria-checked', 'false');
});

test('sun icon visible in light mode, moon icon visible in dark mode', async ({ page }) => {
  // OS light, no saved pref → light state.
  await page.emulateMedia({ colorScheme: 'light' });
  await setup(page, 'shell');
  // In light mode: sun should be visible, moon should be hidden.
  const svgs = page.locator('[data-theme-toggle] svg');
  const sun = svgs.first();  // first SVG = sun
  const moon = svgs.last();  // second SVG = moon
  await expect(sun).toBeVisible();
  await expect(moon).toBeHidden();
  // Click → dark.
  await toggle(page).click();
  // In dark mode: moon should be visible, sun should be hidden.
  await expect(moon).toBeVisible();
  await expect(sun).toBeHidden();
});

test('login rendered markup has explicit readable light and dark surfaces', async ({ page }) => {
  await setup(page, 'login');
  // Switch to explicit light.
  await ensureTheme(page, 'light');
  await expect(page.locator('#email')).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  // Switch to explicit dark.
  await ensureTheme(page, 'dark');
  await expect(page.locator('#email')).toHaveCSS('background-color', 'rgb(55, 65, 81)');
  await expect(page.locator('label[for="email"]')).toHaveCSS('color', 'rgb(255, 255, 255)');
  await expect(page.locator('#registerModal .relative.bg-white')).toHaveCSS('background-color', 'rgb(31, 41, 55)');
  await expect(page.locator('#nombreCompleto')).toHaveCSS('background-color', 'rgb(55, 65, 81)');
  await expect(page.locator('#forgotPasswordModal > div')).toHaveCSS('background-color', 'rgb(31, 41, 55)');
  await expect(page.locator('#google-login-button')).toHaveCSS('background-color', 'rgb(255, 255, 255)');
  await expect(page.locator('label[for="email"] span')).toHaveCSS('color', 'rgb(249, 128, 128)');
});
