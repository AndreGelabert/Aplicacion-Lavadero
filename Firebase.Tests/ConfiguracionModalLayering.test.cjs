'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');
assert.ok(fs.existsSync(chromium.executablePath()), 'cached Chromium executable missing');
const view = fs.readFileSync('Views/Configuracion/Index.cshtml', 'utf8');
const modal = view.slice(view.indexOf('<div id="modalHorarios"'), view.indexOf('@section Scripts'));
assert.ok(modal.startsWith('<div id="modalHorarios"') && modal.includes('guardarHorarios()'));
for (const flowbite of [false, true]) test(`schedule modal preserves two cycles with ${flowbite ? 'installed Flowbite' : 'fallback'}`, async () => {
  const browser = await chromium.launch({headless:true});
  try {
    const page = await browser.newPage({viewport:{width:1000,height:700}});
    await page.route('**/*', route => route.abort());
    await page.setContent(`<style>body{margin:0}.hidden{display:none!important}.fixed{position:fixed}.z-50{z-index:50}.z-40{z-index:40}.inset-0{inset:0}.top-0{top:0}.left-0{left:0}.right-0{right:0}.w-full{width:100%}.nav{position:fixed;top:0;width:100%;height:60px;z-index:50}</style><nav class="nav">navigation</nav><form id="configuracion-form"><input name="other" value="unchanged"><input type="hidden" id="horario-Lunes" name="HorariosOperacion[Lunes]" value="09:00-18:00"></form>${modal}`);
    if (flowbite) await page.addScriptTag({path:'node_modules/flowbite/dist/flowbite.min.js'});
    await page.addScriptTag({path:'wwwroot/js/configuracion.js'});
    const before = await page.evaluate(() => [...new FormData(document.querySelector('form'))]);
    for (let i=0;i<2;i++) {
      await page.evaluate(() => window.abrirModalHorarios('individual','Lunes'));
      const state = await page.evaluate(() => { const m=document.querySelector('#modalHorarios'); const b=document.querySelector('[modal-backdrop], [class*="bg-gray-900/50"]'); return {hidden:m.classList.contains('hidden'), modal:Number(getComputedStyle(m).zIndex), backdrop:b ? Number(getComputedStyle(b).zIndex) : null, navbar:Number(getComputedStyle(document.querySelector('nav')).zIndex), opening:document.querySelector('.horario-apertura').value, closing:document.querySelector('.horario-cierre').value}; });
      assert.equal(state.hidden,false); assert.equal(state.opening,'09:00'); assert.equal(state.closing,'18:00');
      if (flowbite) { assert.ok(state.backdrop > state.navbar, JSON.stringify(state)); assert.ok(state.modal > state.backdrop, JSON.stringify(state)); }
      await page.keyboard.press('Escape');
      assert.equal(await page.locator('#modalHorarios').evaluate(e=>e.classList.contains('hidden')),false);
      await page.evaluate(() => window.cerrarModalHorarios());
      assert.equal(await page.locator('#modalHorarios').evaluate(e=>e.classList.contains('hidden')),true);
      assert.deepEqual(await page.evaluate(() => [...new FormData(document.querySelector('form'))]),before);
    }
  } finally { await browser.close(); }
});
