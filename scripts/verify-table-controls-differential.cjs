'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');
const { spawnSync } = require('node:child_process');

const usage = 'Usage: node scripts/verify-table-controls-differential.cjs --base-root <existingExport> --candidate-root <existingExport> --scratch <existingEmptyDirectory>';
function dir(value) {
  const real = fs.realpathSync(value);
  if (!fs.statSync(real).isDirectory()) throw new Error(`Not a directory: ${value}`);
  return real;
}
function file(root, name) {
  const real = fs.realpathSync(path.join(root, name));
  if (!fs.statSync(real).isFile()) throw new Error(`Not a file: ${real}`);
  return real;
}
function within(parent, child) {
  const rel = path.relative(parent, child);
  return rel === '' || (rel !== '..' && !rel.startsWith('..' + path.sep) && !path.isAbsolute(rel));
}
function dependencies(candidate) {
  const pkg = file(candidate, 'package.json');
  const modules = dir(path.join(candidate, 'node_modules'));
  const cache = dir(path.join(candidate, '.local/playwright-browsers'));
  process.env.PLAYWRIGHT_BROWSERS_PATH = cache;
  const requireCandidate = createRequire(pkg);
  const resolved = fs.realpathSync(requireCandidate.resolve('@playwright/test'));
  if (!within(modules, resolved)) throw new Error('Playwright resolves outside candidate dependencies');
  const { chromium } = requireCandidate('@playwright/test');
  const executable = fs.realpathSync(chromium.executablePath());
  if (!within(cache, executable) || !fs.statSync(executable).isFile()) throw new Error('Chromium executable is outside candidate browser cache');
  return chromium;
}

async function probe(root, candidate) {
  const chromium = dependencies(candidate);
  const browser = await chromium.launch({ headless: true });
  const results = [];
  async function fixture(module, html, helpers, run) {
    const page = await browser.newPage();
    const errors = [], network = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.route('**/*', route => {
      const url = route.request().url();
      if (url === 'http://fixture.test/Personal/Index') return route.fulfill({ status: 200, contentType: 'text/html', body: html });
      network.push(url);
      return route.abort();
    });
    try {
      await page.goto('http://fixture.test/Personal/Index');
      await page.evaluate(enabled => {
        Date.now = () => 123;
        window.trace = [];
        window.Peticiones = {
          filtrar: (name, callback) => { trace.push(['filter', name]); return callback(); },
          enviar: (url, options) => { trace.push(['request', url, options]); return Promise.resolve({ ok: true, text: () => Promise.resolve('<input id="current-page-value" value="3">') }); }
        };
        window.Notificaciones = { mostrar: (...args) => trace.push(['message', ...args]), aviso: (...args) => trace.push(['notice', ...args]) };
        if (enabled) {
          window.CommonUtils = { setupDefaultFilterForm: () => trace.push(['form']) };
          window.initDropdowns = () => trace.push(['dropdown']);
        }
      }, helpers);
      await page.addScriptTag({ path: file(root, `wwwroot/js/${module}.js`) });
      const data = await run(page);
      results.push({ module, helpers, data, errors, network });
    } finally { await page.close(); }
  }
  try {
    await fixture('roles', '<div id="rol-table-container"></div>', true, async page => {
      await page.evaluate(() => window.changeRolPage(2));
      await page.waitForFunction(() => trace.length === 1);
      const defaults = await page.evaluate(() => trace.slice());
      await page.evaluate(() => {
        document.body.insertAdjacentHTML('beforeend', '<input id="current-sort-by" value="Nombre"><input id="current-sort-order" value="asc"><input id="rol-search" value="  Ana  "><input id="rol-estado" value="Activo">');
        trace.length = 0; window.sortRolTable('Nombre');
      });
      await page.waitForFunction(() => trace.length === 1);
      const toggle = await page.evaluate(() => ({ trace: trace.slice(), order: document.querySelector('#current-sort-order').value }));
      await page.evaluate(() => { trace.length = 0; window.sortRolTable('Estado'); });
      const column = await page.evaluate(() => ({ trace: trace.slice(), order: document.querySelector('#current-sort-order').value }));
      return { defaults, toggle, column };
    });
    await fixture('roles', '<div id="rol-delete-modal" class="hidden"></div><input id="rol-delete-id"><span id="rol-delete-text"></span><span id="rol-state-title"></span><button id="rol-delete-confirm-btn"></button>', true, page => page.evaluate(() => {
      const button = document.createElement('button'); Object.assign(button.dataset, { id: '7', nombre: 'Operador' });
      const snapshot = () => [document.querySelector('#rol-state-title').textContent, document.querySelector('#rol-delete-confirm-btn').textContent, document.querySelector('#rol-delete-id').value, document.querySelector('#rol-delete-text').textContent, document.querySelector('#rol-delete-modal').dataset.action];
      window.openRolDeleteModal(button); const off = snapshot();
      window.openRolReactivateModal(button); const on = snapshot();
      document.querySelector('#rol-state-title').remove(); document.querySelector('#rol-delete-confirm-btn').remove();
      window.openRolDeleteModal(button); window.openRolReactivateModal(button);
      button.dataset.protegido = 'true'; window.openRolDeleteModal(button); window.openRolDeleteModal(null);
      return { off, on, trace };
    }));
    const personalHtml = '<form id="filterForm"><input name="roles" type="checkbox" value="2" checked><input name="estados" type="checkbox" value="Activo" checked></form><input id="simple-search" value=""><input id="current-sort-by" value="NombreCompleto"><input id="current-sort-order" value="asc"><div id="personal-table-container"></div>';
    for (const helpers of [true, false]) await fixture('personal', personalHtml, helpers, async page => {
      await page.evaluate(() => window.changePage(2));
      const paging = await page.evaluate(() => trace.slice());
      await page.evaluate(() => { trace.length = 0; window.clearPersonalFilters(); });
      await page.waitForFunction(() => document.querySelector('#filterForm').dataset.clearing === 'false');
      const clearing = await page.evaluate(() => ({ trace: trace.slice(), entries: Array.from(new FormData(document.querySelector('#filterForm')).entries()) }));
      const failure = await page.evaluate(async () => {
        trace.length = 0;
        window.Peticiones.enviar = (url, options) => { trace.push(['request', url, options]); return Promise.resolve({ ok: false, status: 503 }); };
        try { await window.changePage(4); return null; } catch (error) { return error.message; }
      });
      return { paging, clearing, failure, failedTrace: await page.evaluate(() => trace.slice()) };
    });
  } finally { await browser.close(); }
  return results;
}
function differences(a, b, key = '$') {
  if (Object.is(a, b)) return [];
  if (a === null || b === null || typeof a !== 'object' || typeof b !== 'object' || Array.isArray(a) !== Array.isArray(b)) return [{ path: key, base: a, candidate: b }];
  return [...new Set([...Object.keys(a), ...Object.keys(b)])].flatMap(k => {
    if (!(k in a) || !(k in b)) return [{ path: `${key}.${k}`, base: k in a ? a[k] : '<missing>', candidate: k in b ? b[k] : '<missing>' }];
    return differences(a[k], b[k], `${key}.${k}`);
  });
}
function main(args) {
  if (args.length !== 6 || args[0] !== '--base-root' || args[2] !== '--candidate-root' || args[4] !== '--scratch') throw new Error(usage);
  const base = dir(args[1]), candidate = dir(args[3]), scratch = dir(args[5]);
  const worktree = dir(path.resolve(__dirname, '..'));
  if ([base, candidate, worktree].some(root => within(root, scratch) || within(scratch, root)) || within(base, candidate) || within(candidate, base)) throw new Error('Exports and scratch must be disjoint; scratch must also be outside worktree');
  if (fs.readdirSync(scratch).length) throw new Error('Scratch must be empty');
  for (const root of [base, candidate]) for (const name of ['wwwroot/js/roles.js', 'wwwroot/js/personal.js']) file(root, name);
  dependencies(candidate);
  const results = {};
  for (const [label, root] of [['base', base], ['candidate', candidate]]) {
    const result = spawnSync(process.execPath, [__filename, '--probe-root', root, '--dependencies', candidate], {
      cwd: root, env: { ...process.env, PLAYWRIGHT_BROWSERS_PATH: dir(path.join(candidate, '.local/playwright-browsers')) }, encoding: 'utf8', timeout: 300000, maxBuffer: 16 * 1024 * 1024
    });
    const stdout = result.stdout ?? '', stderr = result.stderr ?? '';
    fs.writeFileSync(path.join(scratch, `${label}.stdout.json`), stdout, { flag: 'wx' });
    fs.writeFileSync(path.join(scratch, `${label}.stderr.log`), stderr, { flag: 'wx' });
    let data = null, parseError = null;
    try { data = JSON.parse(stdout); } catch (error) { parseError = error.message; }
    results[label] = { status: result.status, signal: result.signal, error: result.error ? String(result.error) : null, stdout, stderr, data, parseError };
  }
  const diffs = differences(results.base, results.candidate);
  const valid = Object.values(results).every(r => r.status === 0 && !r.signal && !r.error && !r.parseError && Array.isArray(r.data) && r.data.every(item => !item.errors.length && !item.network.length));
  const report = { result: valid && !diffs.length ? 'PASS' : 'FAIL', differences: diffs, results };
  fs.writeFileSync(path.join(scratch, 'report.json'), JSON.stringify(report, null, 2), { flag: 'wx' });
  console.log(JSON.stringify(report, null, 2));
  process.exitCode = report.result === 'PASS' ? 0 : 1;
}
if (process.argv[2] === '--probe-root') {
  if (process.argv.length !== 6 || process.argv[4] !== '--dependencies') { console.error('Invalid internal probe arguments'); process.exitCode = 2; }
  else probe(process.argv[3], process.argv[5]).then(data => console.log(JSON.stringify(data))).catch(error => { console.error(error.stack || String(error)); process.exitCode = 1; });
} else {
  try { main(process.argv.slice(2)); } catch (error) { console.error(error.stack || String(error)); process.exitCode = 2; }
}
