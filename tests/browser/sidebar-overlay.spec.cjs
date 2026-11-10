const fs = require('node:fs');
const path = require('node:path');
const { test, expect } = require('@playwright/test');

const root = path.resolve(__dirname, '../..');
const layout = fs.readFileSync(path.join(root, 'Views/Shared/_Layout.cshtml'), 'utf8');
const one = (pattern) => {
  const matches = [...layout.matchAll(pattern)];
  if (matches.length !== 1) throw new Error(`Expected one layout match: ${pattern}`);
  return matches[0][0];
};
const nav = one(/<nav\b[^>]*>/g);
const toggle = one(/<button\b[^>]*data-drawer-toggle="drawer-navigation"[^>]*>[\s\S]*?<\/button>/g);
const aside = one(/<aside\b[^>]*id="drawer-navigation"[^>]*>/g);
const main = one(/<main\b[^>]*role="main"[^>]*>/g);

for (const viewport of [{ width: 1440, height: 900 }, { width: 390, height: 844 }]) {
  for (const dark of [false, true]) {
    test(`sidebar overlay ${viewport.width} ${dark ? 'dark' : 'light'}`, async ({ page }) => {
      await page.setViewportSize(viewport);
      await page.setContent(`<html class="${dark ? 'dark' : ''}"><body>${nav}${toggle}</nav>${aside}<span>Menu</span></aside>${main}<div id="content">Content</div></main></body></html>`);
      await page.addStyleTag({ path: path.join(root, 'wwwroot/css/app.min.css') });
      await page.addScriptTag({ path: require.resolve('flowbite/dist/flowbite.min.js') });
      await page.evaluate(() => window.initFlowbite());
      const drawer = page.locator('#drawer-navigation');
      const button = page.locator('[data-drawer-toggle="drawer-navigation"]');
      const geometry = () => page.locator('main').evaluate(el => {
        const r = el.getBoundingClientRect();
        return { x: r.x, width: r.width, margin: getComputedStyle(el).marginLeft, contentX: document.querySelector('#content').getBoundingClientRect().x, overflow: document.documentElement.scrollWidth > innerWidth };
      });
      const expected = { x: 0, width: viewport.width, margin: '0px', contentX: 16, overflow: false };
      expect(await geometry()).toEqual(expected);
      await expect.poll(async () => (await drawer.boundingBox()).x).toBeLessThanOrEqual(-256);
      for (let i = 0; i < 2; i++) {
        await button.click();
        await expect.poll(async () => (await drawer.boundingBox()).x).toBe(0);
        await expect(drawer).toHaveCSS('position', 'fixed');
        await expect(page.locator('[drawer-backdrop]')).toHaveCount(1);
        expect(await geometry()).toEqual(expected);
        await button.click();
        await expect.poll(async () => (await drawer.boundingBox()).x).toBeLessThanOrEqual(-256);
        expect(await geometry()).toEqual(expected);
      }
      await button.click();
      await expect(page.locator('[drawer-backdrop]')).toHaveCount(1);
      await page.locator('[drawer-backdrop]').click({ position: { x: viewport.width - 10, y: viewport.height - 10 } });
      await expect(page.locator('[drawer-backdrop]')).toHaveCount(0);
      expect(await geometry()).toEqual(expected);
    });
  }
}
