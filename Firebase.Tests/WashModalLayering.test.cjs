'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
process.env.PLAYWRIGHT_BROWSERS_PATH = path.resolve('.local/playwright-browsers');
const { chromium } = require('@playwright/test');
const source = fs.readFileSync('wwwroot/js/lavado.js', 'utf8');
const helperStart = source.indexOf('    function abrirModal(modalId)');
const closeSetup = source.indexOf('    // SETUP DE BOTONES DE CIERRE');
assert.ok(helperStart >= 0 && closeSetup > helperStart, 'wash modal helper boundaries missing or inverted');
const helper = source.slice(helperStart, closeSetup);
const closeStart = source.indexOf("    document.addEventListener('DOMContentLoaded', function () {", closeSetup);
const closeEnd = source.indexOf('    // MONITOREO DE TIEMPO', closeSetup);
assert.ok(closeStart > closeSetup && closeEnd > closeStart, 'wash modal close handler boundaries missing or inverted');
const closeHandler = source.slice(closeStart, closeEnd);

test('unregistered Flowbite preserves fallback Escape behavior', async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage();
        await page.setContent('<div id="cancelarModal" class="hidden">cancelar</div>');
        await page.addScriptTag({ path: 'node_modules/flowbite/dist/flowbite.min.js' });
        await page.addScriptTag({ content: helper });
        await page.evaluate(() => window.abrirModal('cancelarModal'));
        await page.keyboard.press('Escape');
        assert.equal(await page.locator('#cancelarModal').evaluate(el => el.classList.contains('hidden')), false);
    } finally { await browser.close(); }
});

test('auto-initialized Flowbite and actual close handler preserve two public cycles', async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage({ viewport: { width: 1000, height: 700 } });
        const view = fs.readFileSync('Views/Lavados/Index.cshtml', 'utf8');
        const form = view.match(/<form id="formCancelar"[\s\S]*?<\/form>/)[0].replace('@Html.AntiForgeryToken()', '<input name="__RequestVerificationToken" value="token">');
        await page.setContent(`<style>body{margin:0}.hidden{display:none!important}.fixed{position:fixed}.inset-0{inset:0}.nav{position:fixed;top:0;width:100%;height:60px;z-index:50}.panel{position:absolute;left:400px;top:250px;width:200px;height:180px;background:white}</style><nav class="nav" onclick="window.navClicks++">navbar</nav><button data-modal-target="cancelarModal" data-modal-toggle="cancelarModal">toggle</button><div id="cancelarModal" tabindex="-1" aria-hidden="true" class="hidden fixed inset-0"><div class="panel"><button id="panel" type="button" onclick="window.panelClicks++">panel</button><button id="close" type="button" data-modal-hide="cancelarModal">close</button>${form}</div></div>`);
        await page.addScriptTag({ content: helper });
        await page.addScriptTag({ content: closeHandler });
        await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
        await page.addScriptTag({ path: 'node_modules/flowbite/dist/flowbite.min.js' });
        await page.evaluate(() => { window.navClicks = 0; window.panelClicks = 0; window.initModals(); });
        assert.equal(await page.evaluate(() => window.FlowbiteInstances.instanceExists('Modal', 'cancelarModal')), true);
        let posts = 0; page.on('request', req => { if (req.method() === 'POST') posts++; });
        const before = await page.evaluate(() => { const f = document.querySelector('#formCancelar'); f.elements.id.value = 'wash'; f.elements.motivo.value = 'reason'; return [f.getAttribute('action'), f.method, [...new FormData(f)]]; });
        for (let i = 0; i < 2; i++) {
            await page.evaluate(() => window.abrirModal('cancelarModal'));
            const opened = await page.evaluate(() => ({ hidden: document.querySelector('#cancelarModal').classList.contains('hidden'), aria: document.querySelector('#cancelarModal').getAttribute('aria-modal'), backdrops: document.querySelectorAll('[modal-backdrop],#modal-backdrop').length, locked: document.body.classList.contains('overflow-hidden') }));
            assert.deepEqual(opened, { hidden: false, aria: null, backdrops: 1, locked: true });
            await page.mouse.click(10, 10);
            await page.keyboard.press('Escape');
            assert.equal(await page.evaluate(() => window.navClicks), 0);
            assert.equal(await page.locator('#cancelarModal').evaluate(el => el.classList.contains('hidden')), false);
            await page.locator('#panel').click({ timeout: 3000 });
            await page.locator('#close').click({ timeout: 3000 });
            assert.deepEqual(await page.evaluate(() => ({ hidden: document.querySelector('#cancelarModal').classList.contains('hidden'), aria: document.querySelector('#cancelarModal').getAttribute('aria-hidden'), backdrops: document.querySelectorAll('[modal-backdrop],#modal-backdrop').length, locked: document.body.classList.contains('overflow-hidden') })), { hidden: true, aria: 'true', backdrops: 0, locked: false });
        }
        assert.equal(await page.evaluate(() => window.panelClicks), 2);
        assert.deepEqual(await page.evaluate(() => { const f = document.querySelector('#formCancelar'); return [f.getAttribute('action'), f.method, [...new FormData(f)]]; }), before);
        assert.equal(posts, 0);
    } finally { await browser.close(); }
});

test('wash detail fallback raises a reused backdrop and modal', async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage();
        const detail = fs.readFileSync('wwwroot/js/lavado-detalle.js', 'utf8');
        await page.setContent('<div id="modal-backdrop"></div><div id="retiroModal" class="hidden"></div>');
        await page.addScriptTag({ content: detail });
        await page.evaluate(() => window.abrirModal('retiroModal'));
        assert.deepEqual(await page.evaluate(() => [document.getElementById('modal-backdrop').style.zIndex, document.getElementById('retiroModal').style.zIndex, document.querySelectorAll('#modal-backdrop').length]), ['51', '52', 1]);
        await page.evaluate(() => window.cerrarModal('retiroModal'));
        assert.equal(await page.locator('#modal-backdrop').count(), 0);
    } finally { await browser.close(); }
});

for (const flowbite of [false, true]) test(`wash backdrop covers navbar (${flowbite ? 'installed Flowbite' : 'fallback'})`, async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage({ viewport: { width: 1000, height: 700 } });
        await page.setContent('<style>body{margin:0}.hidden{display:none}.nav{position:fixed;inset:0 0 auto;height:60px;z-index:50}.fixed{position:fixed}.inset-0{inset:0}.z-40{z-index:40}.z-50{z-index:50}.panel{position:absolute;left:400px;top:250px;width:200px;height:100px}</style><nav class="nav">navbar</nav><div id="cancelarModal" class="hidden fixed inset-0 z-50"><div class="panel">panel</div></div>');
        if (flowbite) await page.addScriptTag({ path: 'node_modules/flowbite/dist/flowbite.min.js' });
        await page.addScriptTag({ content: helper });
        await page.evaluate(() => window.abrirModal('cancelarModal'));
        const result = await page.evaluate(() => { const panel = document.elementFromPoint(450, 280).className; const modal = document.getElementById('cancelarModal'); modal.style.pointerEvents = 'none'; const top = document.elementFromPoint(10, 10).className; modal.style.pointerEvents = ''; return { top, panel }; });
        assert.notEqual(result.top, 'nav', 'backdrop must cover navbar');
        assert.equal(result.panel, 'panel');
    } finally { await browser.close(); }
});
