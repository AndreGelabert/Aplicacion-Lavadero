const fs = require('node:fs');
const path = require('node:path');
const { test, expect } = require('@playwright/test');
const root = path.resolve(__dirname, '../..');
const modules = ['Auditoria', 'Cliente', 'Lavados', 'PaqueteServicio', 'Personal', 'Rol', 'Servicio', 'Vehiculo'];
const read = p => fs.readFileSync(path.join(root, p), 'utf8');

for (const name of modules) {
  for (const width of [390, 1440]) for (const dark of [false, true]) {
    test(`${name} table ${width} ${dark ? 'dark' : 'light'}`, async ({ page }) => {
      const file = `Views/${name}/_${name === 'Lavados' ? 'Lavado' : name}Table.cshtml`;
      const source = read(file);
      // Synthetic geometry fixture: actual wrapper/table/nav opening tags and column count;
      // finite stand-in cells and one pagination state, not Razor-rendered output.
      const rawTable = source.match(/<table\b[\s\S]*?<\/table>/)[0];
      const tableTag = rawTable.match(/<table\b[^>]*>/)[0];
      const headers = rawTable.match(/<thead\b[\s\S]*?<\/thead>/)[0];
      const columns = [...headers.matchAll(/<th\b/g)].length;
      expect(columns).toBeGreaterThan(0);
      const cells = Array.from({ length: columns }, (_, i) => `<td class="px-4 py-3 whitespace-nowrap">Contenido ${i + 1}</td>`).join('');
      const table = `${tableTag}<thead><tr>${Array.from({ length: columns }, (_, i) => `<th class="px-4 py-3">Columna ${i + 1}</th>`).join('')}</tr></thead><tbody><tr>${cells}</tr></tbody></table>`;
      const navTag = source.match(/<nav\b[^>]*>/)[0];
      const nav = `${navTag}<span>Mostrando página 1 de 3</span><ul class="inline-flex"><li><button type="button">2</button></li></ul></nav>`;
      const before = source.slice(0, source.indexOf(rawTable));
      const after = source.slice(source.indexOf(rawTable) + rawTable.length);
      const wrapper = before.match(/<div[^>]*class="[^"]*overflow-x-auto[^"]*"[^>]*>\s*$/)?.[0] || '';
      const close = wrapper ? after.match(/^\s*<\/div>/)?.[0] || '' : '';
      await page.setViewportSize({ width, height: 900 });
      await page.setContent(`<html class="${dark ? 'dark' : ''}"><body><main style="padding:16px">${wrapper}${table}${close}${nav}</main></body></html>`);
      await page.addStyleTag({ path: path.join(root, 'wwwroot/css/app.min.css') });
      const scroll = page.locator('table').locator('..');
      expect(await scroll.getAttribute('class')).toMatch(/overflow-x-auto/);
      await expect(scroll).toHaveAttribute('tabindex', '0');
      await expect(scroll).toHaveAttribute('role', 'region');
      expect(await scroll.getAttribute('aria-label')).toBeTruthy();
      await expect(page.locator('thead th')).toHaveCount(columns);
      expect(await page.locator('nav').evaluate(el => !!el.closest('.overflow-x-auto'))).toBe(false);
      const metrics = await scroll.evaluate(el => ({ document: document.documentElement.scrollWidth, viewport: innerWidth, client: el.clientWidth, scroll: el.scrollWidth, left: el.getBoundingClientRect().left }));
      expect(metrics.document).toBe(width);
      expect(metrics.left).toBe(16);
      if (width === 390) {
        expect(metrics.scroll).toBeGreaterThan(metrics.client);
        await scroll.focus();
        await page.keyboard.press('ArrowRight');
        await expect.poll(() => scroll.evaluate(el => el.scrollLeft)).toBeGreaterThan(0);
      }
    });
  }
}
for (const name of modules.filter(x => x !== 'Rol')) {
  test(`${name} search fits mobile`, async ({ page }) => {
    const source = read(`Views/${name}/Index.cshtml`);
    const tag = source.match(/<div class="relative w-(?:96|full md:w-96)">/)[0];
    await page.setViewportSize({ width: 390, height: 844 });
    await page.setContent(`<main style="width:250px">${tag}<input></div></main>`);
    await page.addStyleTag({ path: path.join(root, 'wwwroot/css/app.min.css') });
    const sizes = await page.locator('main > div').evaluate(el => [el.offsetWidth, el.parentElement.clientWidth]);
    expect(sizes[0]).toBeLessThanOrEqual(sizes[1]);
  });
}
