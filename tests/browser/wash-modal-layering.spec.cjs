const fs = require('node:fs');
const { test, expect } = require('@playwright/test');
const source = fs.readFileSync('wwwroot/js/lavado.js', 'utf8');
const helper = source.slice(source.indexOf('    function abrirModal(modalId)'), source.indexOf('    // SETUP DE BOTONES DE CIERRE'));
const view = fs.readFileSync('Views/Lavados/Index.cshtml', 'utf8');
const form = view.match(/<form id="formCancelar"[\s\S]*?<\/form>/)[0].replace('@Html.AntiForgeryToken()', '<input type="hidden" name="__RequestVerificationToken" value="token">');

for (const flowbite of [false, true]) for (const id of ['cancelarModal', 'pagoModal', 'finalizarModal', 'detalleModal', 'tiempoExcedidoModal']) {
  test(`${id}: layering and lifecycle, ${flowbite ? 'Flowbite' : 'fallback'}`, async ({ page }) => {
    await page.setViewportSize({ width: 1000, height: 700 });
    await page.setContent(`<style>body{margin:0}.hidden{display:none!important}.fixed{position:fixed}.inset-0{inset:0}.z-40{z-index:40}.z-50{z-index:50}.nav{position:fixed;top:0;width:100%;height:60px;z-index:50}.panel{position:absolute;left:400px;top:250px;width:200px;height:100px;background:white}</style><nav class="nav">navbar</nav><div id="${id}" class="hidden fixed inset-0 z-50"><div class="panel">${id === 'cancelarModal' ? form : 'panel'}</div></div>`);
    if (flowbite) await page.addScriptTag({ path: 'node_modules/flowbite/dist/flowbite.min.js' });
    await page.addScriptTag({ content: helper });
    const before = await page.evaluate(() => { const f = document.querySelector('form'); if (!f) return null; f.querySelector('[name=id]').value = 'wash'; f.querySelector('[name=motivo]').value = 'reason'; return [f.getAttribute('action'), f.method, [...new FormData(f)]]; });
    for (let cycle = 0; cycle < 2; cycle++) {
      await page.evaluate(id => window.abrirModal(id), id);
      const layers = await page.evaluate(id => { const modal = document.getElementById(id); const panel = document.elementFromPoint(410, 260); modal.style.pointerEvents = 'none'; const backdrop = document.elementFromPoint(10, 10); modal.style.pointerEvents = ''; return { panel: panel.closest('.panel') !== null, backdrop: backdrop !== document.querySelector('nav'), count: [...document.body.children].filter(x => x !== modal && x.tagName === 'DIV').length, locked: document.body.classList.contains('overflow-hidden') }; }, id);
      expect(layers).toEqual({ panel: true, backdrop: true, count: 1, locked: true });
      await page.evaluate(id => window.cerrarModal(id), id);
      expect(await page.evaluate(id => ({ hidden: document.getElementById(id).classList.contains('hidden'), count: [...document.body.children].filter(x => x.id !== id && x.tagName === 'DIV').length, locked: document.body.classList.contains('overflow-hidden') }), id)).toEqual({ hidden: true, count: 0, locked: false });
    }
    const after = await page.evaluate(() => { const f = document.querySelector('form'); return f ? [f.getAttribute('action'), f.method, [...new FormData(f)]] : null; });
    expect(after).toEqual(before);
  });
}
