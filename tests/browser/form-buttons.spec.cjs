const path = require('node:path');
const fs = require('node:fs');
const { test, expect } = require('@playwright/test');

const root = path.resolve(__dirname, '../..');
for (const name of ['Cliente', 'Rol', 'Vehiculo', 'PaqueteServicio', 'Servicio', 'Lavados', 'Configuracion']) {
  test(`${name}: acciones reales conservan envío y clic`, async ({ page }) => {
    const file = name === 'Configuracion' ? 'Views/Configuracion/Index.cshtml' : `Views/${name}/_${name === 'Lavados' ? 'Lavado' : name}Form.cshtml`;
    const source = fs.readFileSync(path.join(root, file), 'utf8');
    const formTag = source.match(/<form\b[^>]*>/)[0];
    const actions = [...source.matchAll(/<(?:button|a)\b[^>]*class="app-button app-button--(?:success|danger|primary|secondary)"[^>]*>[\s\S]*?<\/(?:button|a)>/g)].map(m => m[0]);
    // Resolve only the create-mode Razor expression; leave event handlers intact.
    const create = actions.join('').replace(/onclick="@\(edit \? "[^"]*" : "([^"]*)"\)"/g, 'onclick="$1"')
      .replace(/onclick="@\(Model == null \? "([^"]*)" : "[^"]*"\)"/g, 'onclick="$1"')
      .replace('href="@Url.Action("Index", "Lavados")"', 'href="/Lavados"');
    await page.setContent(`${formTag}<input name="example" value="original">${create}</form>`);
    await page.evaluate(() => {
      window.calls = [];
      for (const fn of ['submitClienteAjax', 'submitRolAjax', 'submitVehiculoAjax', 'submitPaqueteAjax', 'submitServicioAjax', 'cancelRolForm', 'limpiarFormularioPaquete', 'limpiarFormularioLavado']) window[fn] = () => { window.calls.push(fn); return false; };
      document.querySelector('form').addEventListener('submit', e => { window.calls.push('submit'); e.preventDefault(); });
    });
    const submit = page.locator('.app-button--success, .app-button--primary');
    const clear = page.locator('.app-button--danger, .app-button--secondary');
    if (name === 'Lavados') {
      await expect(submit).toBeDisabled();
      await submit.evaluate(el => el.click());
      expect(await page.evaluate(() => window.calls)).toEqual([]);
    } else {
      await submit.click();
      expect(await page.evaluate(() => window.calls)).toContain('submit');
    }
    if (name !== 'Configuracion') {
      await page.locator('input[name="example"]').fill('changed');
      await clear.click();
      const calls = await page.evaluate(() => window.calls);
      expect(calls.filter(x => x === 'submit')).toHaveLength(name === 'Lavados' ? 0 : 1);
      if (['Cliente', 'Vehiculo', 'Servicio'].includes(name)) await expect(page.locator('input[name="example"]')).toHaveValue('original');
      else expect(calls).toContain({ Rol: 'cancelRolForm', PaqueteServicio: 'limpiarFormularioPaquete', Lavados: 'limpiarFormularioLavado' }[name]);
    } else {
      await expect(clear).toHaveAttribute('href', '/Lavados');
    }
  });
}

for (const dark of [false, true]) {
  test(`botones comparten tamaño y colores ${dark ? 'oscuro' : 'claro'}`, async ({ page }) => {
    await page.setContent(`<html class="${dark ? 'dark' : ''}"><body>${['primary', 'secondary', 'danger', 'success'].map(v => `<button class="app-button app-button--${v}">Acción</button>`).join('')}<button disabled class="app-button app-button--success">Deshabilitado</button></body></html>`);
    await page.addStyleTag({ path: path.resolve(__dirname, '../../wwwroot/css/app.min.css') });
    const buttons = page.locator('button:not([disabled])');
    const sizes = await buttons.evaluateAll(nodes => nodes.map(n => { const s = getComputedStyle(n); return [s.padding, s.fontSize, s.minHeight]; }));
    expect(sizes.every(s => JSON.stringify(s) === JSON.stringify(sizes[0]))).toBe(true);
    expect(sizes[0][0]).not.toBe('0px');
    const colors = await buttons.evaluateAll(nodes => nodes.map(n => getComputedStyle(n).backgroundColor));
    expect(new Set(colors).size).toBe(4);
    await expect(page.locator('button:disabled')).toHaveCSS('opacity', '0.5');
    await buttons.first().focus();
    await expect(buttons.first()).toHaveCSS('outline-style', 'solid');
  });
}
