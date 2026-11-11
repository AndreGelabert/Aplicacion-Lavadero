'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');
const { spawnSync } = require('node:child_process');
const assert = require('node:assert/strict');

const within = (parent, child) => {
  const rel = path.relative(parent, child);
  return rel === '' || (rel !== '..' && !rel.startsWith('..' + path.sep) && !path.isAbsolute(rel));
};
function directory(value) {
  const real = fs.realpathSync(value);
  if (!fs.statSync(real).isDirectory()) throw new Error(`Not a directory: ${value}`);
  return real;
}
function browserFor(root) {
  const cache = directory(path.join(root, '.local/playwright-browsers'));
  const modules = directory(path.join(root, 'node_modules'));
  process.env.PLAYWRIGHT_BROWSERS_PATH = cache;
  const localRequire = createRequire(path.join(root, 'package.json'));
  if (!within(modules, fs.realpathSync(localRequire.resolve('@playwright/test')))) throw new Error('Playwright outside dependency directory');
  const chromium = localRequire('@playwright/test').chromium;
  const executable = fs.realpathSync(chromium.executablePath());
  if (!within(cache, executable) || !fs.statSync(executable).isFile()) throw new Error('Chromium outside browser cache');
  return chromium;
}
const paymentHtml = '<input id="pagoLavadoId"><span id="pagoTotal"></span><span id="pagoPagado"></span><span id="pagoRestante"></span><input id="montoInput"><input id="notasPago"><div id="pagoModal" class="hidden"></div>';
const formHtml = '<form id="filterForm"><input name="roles" value="one"><input name="roles" value="two"><input name="estados" value="Activo"></form>';

async function probe(root, candidateChecks, escapeOnly = false) {
  const browser = await browserFor(root).launch({ headless: true });
  const cases = [];
  async function fixture(module, site, html, run, expectedCalls, kind = 'standard') {
    const page = await browser.newPage();
    const errors = [], network = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.route('**/*', route => {
      const url = route.request().url();
      if (url === 'http://fixture.test/Personal/Index') return route.fulfill({ status: 200, contentType: 'text/html', body: html });
      network.push(url); return route.abort();
    });
    try {
      await page.goto('http://fixture.test/Personal/Index');
      await page.evaluate(() => {
        Date.now = () => 123;
        window.trace = [];
        window.publicErrors = [];
        window.addEventListener('error', event => window.publicErrors.push(`${event.error?.name}: ${event.message}`));
        window.Peticiones = {
          filtrar: (name, callback) => callback(),
          texto: (url, options) => { trace.push({ url, options: options || null }); return Promise.resolve('<input id="current-page-value" value="3">'); },
          enviar: (url, options) => {
            trace.push({ url, options });
            return Promise.resolve({ ok: true, text: () => Promise.resolve('<input id="current-page-value" value="3">') });
          }
        };
        window.Notificaciones = { mostrar: () => {}, aviso: () => {} };
      });
      if (site) await page.addScriptTag({ path: path.join(root, 'wwwroot/js/site.js') });
      if (site && candidateChecks) await page.evaluate(() => {
        window.delegation = {};
        for (const name of ['formatCurrency', 'appendFormDataToParams', 'getSortState', 'escapeHtmlText']) {
          const original = window.SiteModule[name];
          if (typeof original !== 'function') continue;
          window.SiteModule[name] = function (...args) {
            window.delegation[name] = (window.delegation[name] || 0) + 1;
            return original.apply(this, args);
          };
        }
      });
      await page.addScriptTag({ path: path.join(root, `wwwroot/js/${module}.js`) });
      const data = await run(page);
      const calls = candidateChecks && site ? await page.evaluate(() => window.delegation) : {};
      const failures = [];
      for (const [name, count] of Object.entries(expectedCalls)) {
        if (candidateChecks && site && (count === -1 ? !(calls[name] > 0) : (calls[name] || 0) !== count)) failures.push(`${name}: expected ${count === -1 ? 'at least 1' : count}, got ${calls[name] || 0}`);
      }
      if (errors.length) failures.push('page errors');
      if (network.length) failures.push('unexpected network');
      cases.push({ module, site, kind, data, errors, network, failures });
    } catch (error) {
      const trace = await page.evaluate(() => ({ requests: window.trace || [], errors: window.publicErrors || [] })).catch(() => null);
      cases.push({ module, site, kind, data: null, errors, network, failures: [String(error)], trace });
    } finally { await page.close(); }
  }
  try {
    if (!escapeOnly) {
    for (const module of ['lavado', 'lavado-detalle']) {
      await fixture(module, true, paymentHtml, page => page.evaluate(() => {
        const inputs = [[0, 0], [-1234.5, 2.25], [1.235, 0], ['42.5', '2'], ['invalid', null], [null, undefined]];
        return inputs.map(([total, paid]) => {
          window.abrirModalPago('wash', total, paid);
          const get = id => document.getElementById(id);
          return { texts: ['pagoTotal', 'pagoPagado', 'pagoRestante'].map(id => get(id).textContent), id: get('pagoLavadoId').value, amount: get('montoInput').value, max: get('montoInput').max, remaining: get('montoInput').dataset.restante, hidden: get('pagoModal').classList.contains('hidden') };
        });
      }), { formatCurrency: 18 });
    }
    for (const site of [true, false]) {
      await fixture('personal', site, formHtml + '<div id="personal-table-container"></div>', async page => {
        await page.evaluate(() => window.changePage(2));
        const first = await page.evaluate(() => ({ trace: trace.slice(), page: document.getElementById('personal-table-container').dataset.currentPage }));
        await page.evaluate(() => { document.getElementById('filterForm').remove(); trace.length = 0; });
        await page.evaluate(() => window.changePage(3));
        return { first, missing: await page.evaluate(() => ({ trace: trace.slice(), page: document.getElementById('personal-table-container').dataset.currentPage })) };
      }, { appendFormDataToParams: 1, getSortState: 2 });
      await fixture('roles', site, '<div id="rol-table-container"></div>', async page => {
        await page.evaluate(() => window.changeRolPage(2));
        await page.waitForFunction(() => trace.length === 1 && document.querySelector('#current-page-value'));
        const first = await page.evaluate(() => trace.slice());
        await page.evaluate(() => {
          document.body.insertAdjacentHTML('beforeend', '<input id="current-sort-by" value="Nombre"><input id="current-sort-order" value="asc">');
          trace.length = 0; window.sortRolTable('Nombre');
        });
        await page.waitForFunction(() => trace.length === 1 && document.querySelector('#current-page-value'));
        const same = await page.evaluate(() => ({ trace: trace.slice(), order: document.querySelector('#current-sort-order').value }));
        await page.evaluate(() => { trace.length = 0; window.sortRolTable('Estado'); });
        await page.waitForFunction(() => trace.length === 1 && document.querySelector('#current-page-value'));
        return { first, same, next: await page.evaluate(() => ({ trace: trace.slice(), order: document.querySelector('#current-sort-order').value })) };
      }, { getSortState: 5 });
    }
    const tableModules = [
      { module: 'auditoria', container: 'auditoria', reload: 'reloadAuditoriaTable', sort: 'Timestamp', order: 'desc', sharedSort: true },
      { module: 'cliente', container: 'cliente', reload: null, sort: 'Nombre', order: 'asc', sharedSort: false },
      { module: 'lavado', container: 'lavado', reload: 'reloadLavadoTable', sort: 'FechaCreacion', order: 'desc', sharedSort: false },
      { module: 'servicio', container: 'servicio', reload: 'reloadServicioTable', sort: 'Nombre', order: 'asc', sharedSort: true },
      { module: 'paqueteservicio', container: 'paquete', reload: 'changePage', sort: 'Nombre', order: 'asc', sharedSort: true }
    ];
    for (const site of [true, false]) for (const spec of tableModules) {
      const html = formHtml + `<div id="${spec.container}-table-container"></div>`;
      const calls = { appendFormDataToParams: 3 };
      if (spec.sharedSort) calls.getSortState = spec.module === 'auditoria' ? 5 : 3;
      await fixture(spec.module, site, html, async page => {
        async function invoke(action, value) {
          await page.evaluate(({ action, value }) => {
            trace.length = 0;
            if (action === 'reload') window[value](2);
            else window.sortTable(value);
          }, { action, value });
          await page.waitForFunction(() => trace.length === 1 && document.querySelector('#current-page-value'));
          return page.evaluate(() => ({ request: trace[0], page: document.querySelector('#current-page-value').value }));
        }
        const first = await invoke(spec.reload ? 'reload' : 'sort', spec.reload || spec.sort);
        await page.evaluate(({ sort, order }) => document.body.insertAdjacentHTML('beforeend', `<input id="current-sort-by" value="${sort}"><input id="current-sort-order" value="${order}">`), spec);
        const same = await invoke('sort', spec.sort);
        const next = await invoke('sort', 'Estado');
        return { first, same, next };
      }, calls);
    }
    }
    for (const site of [true, false]) for (const module of ['cliente', 'lavado', 'vehiculo']) {
      const html = module === 'lavado'
        ? '<input id="vehiculoId"><input id="patenteSearch"><div id="patenteResultados"></div><div id="vehiculoSeleccionado"></div><div id="vehiculoNombre"></div><div id="infoVehiculoSection"><div id="infoVehiculo"></div></div><div id="clientesAsociadosSection"></div><input id="clienteId"><select id="clienteTrajoSelect"></select><select id="clienteRetiraSelect"></select><div id="serviciosSection"><div id="serviciosPorVehiculo"></div></div><div id="resumenSection"></div>'
        : `<div id="${module}ConfirmMessage"></div>`;
      await fixture(module, site, html, async page => {
        return page.evaluate(async module => {
          const values = ['<b>& "quoted"</b>', '', null, 0];
          const snapshots = [];
          let initialized = null;
          if (module === 'lavado') {
            window.limpiarFormularioLavado();
            initialized = { vehicleId: document.getElementById('vehiculoId').value, selectedHidden: document.getElementById('vehiculoSeleccionado').classList.contains('hidden') };
            window.Peticiones.enviar = (url, options) => {
              trace.push({ url, options: options || null });
              const data = url.includes('ObtenerClientesVehiculo') ? [{ id: 'client', nombre: 'Cliente', documento: '1' }] : url.includes('ObtenerEmpleados') ? {} : [];
              return Promise.resolve({ json: () => Promise.resolve(data) });
            };
          } else if (module === 'cliente') {
            window.Peticiones.enviar = (url, options) => { trace.push({ url, options: options || null }); return Promise.resolve({ json: () => Promise.resolve({ success: true, vehiculos: [] }) }); };
          }
          for (const value of values) {
            if (module === 'cliente') await window.openClienteConfirmModal('desactivar', 'id', value);
            else if (module === 'vehiculo') window.openVehiculoConfirmModal('desactivar', 'id', value);
            else {
              document.getElementById('serviciosPorVehiculo').innerHTML = '';
              await window.seleccionarVehiculo('id', value, 'Auto', 'Marca', 'Modelo', 'Color');
            }
            const node = document.getElementById(module === 'lavado' ? 'infoVehiculo' : `${module}ConfirmMessage`);
            snapshots.push({ html: node.innerHTML, text: node.textContent, injected: node.querySelectorAll('b').length });
          }
          return { snapshots, requests: trace.slice(), initialized };
        }, module);
      }, { escapeHtmlText: -1 }, 'escape');
    }
    if (!escapeOnly) for (const site of [true, false]) {
      await fixture('vehiculo', site, formHtml + '<input id="simple-search"><div id="vehiculo-table-container"></div>', async page => {
        await page.evaluate(() => window.PageModules.vehiculos.init());
        const boundary = await page.evaluate(() => ({ requests: trace.length, errors: publicErrors.length }));
        await page.evaluate(() => { const input = document.querySelector('#simple-search'); input.value = 'placa'; input.dispatchEvent(new Event('input', { bubbles: true })); });
        await page.waitForFunction(({ requests, errors }) => (trace.length > requests && document.querySelector('#vehiculo-table-container #current-page-value')) || publicErrors.length > errors, boundary, { timeout: 3000 });
        return page.evaluate(({ requests, errors }) => ({ requests: trace.slice(requests), errors: publicErrors.slice(errors), table: document.querySelector('#vehiculo-table-container').innerHTML, page: document.querySelector('#vehiculo-table-container').dataset.currentPage || null, entries: Array.from(new FormData(document.querySelector('#filterForm')).entries()) }), boundary);
      }, {});
    }
  } finally { await browser.close(); }
  return cases;
}

const searchEntries = [['roles', 'one'], ['roles', 'two'], ['estados', 'Activo']];
const searchUrl = '/Vehiculo/SearchPartial?roles=one&roles=two&estados=Activo&searchTerm=placa&pageNumber=1&sortBy=Patente&sortOrder=asc';
const expectedKeys = [
  ['lavado', true, 'standard'], ['lavado-detalle', true, 'standard'],
  ...[true, false].flatMap(site => ['personal', 'roles'].map(module => [module, site, 'standard'])),
  ...[true, false].flatMap(site => ['auditoria', 'cliente', 'lavado', 'servicio', 'paqueteservicio'].map(module => [module, site, 'standard'])),
  ...[true, false].flatMap(site => ['cliente', 'lavado', 'vehiculo'].map(module => [module, site, 'escape'])),
  ...[true, false].map(site => ['vehiculo', site, 'standard'])
];
const equal = (left, right) => JSON.stringify(left) === JSON.stringify(right);
function requestedComparison(baseCases, candidateCases) {
  const evidence = [];
  if (!Array.isArray(baseCases) || !Array.isArray(candidateCases) || baseCases.length !== 24 || candidateCases.length !== 24) {
    return { passed: false, preservedCases: 0, authorizedDifferences: 0, total: 24, evidence, error: 'Expected exactly 24 cases in each root' };
  }
  let preservedCases = 0, authorizedDifferences = 0;
  for (let i = 0; i < expectedKeys.length; i++) {
    const [module, site, kind] = expectedKeys[i];
    const base = baseCases[i], candidate = candidateCases[i];
    const keyMatches = item => item && item.module === module && item.site === site && item.kind === kind;
    const search = module === 'vehiculo' && kind === 'standard';
    let passed = false;
    let expectedBase, expectedCandidate;
    if (search) {
      expectedBase = { module, site, kind, data: { requests: [], errors: ['ReferenceError: Uncaught ReferenceError: getCurrentSort is not defined'], table: '', page: null, entries: searchEntries }, errors: ['getCurrentSort is not defined'], network: [], failures: ['page errors'] };
      expectedCandidate = { module, site, kind, data: { requests: [{ url: searchUrl, options: null }], errors: [], table: '<input id="current-page-value" value="3">', page: '3', entries: searchEntries }, errors: [], network: [], failures: [] };
      passed = keyMatches(base) && keyMatches(candidate) && equal(base, expectedBase) && equal(candidate, expectedCandidate) && equal(base.data.entries, candidate.data.entries);
      if (passed) authorizedDifferences++;
    } else {
      passed = keyMatches(base) && keyMatches(candidate) && equal(base, candidate) && equal(base.errors, []) && equal(base.network, []) && equal(base.failures, []);
      if (passed) preservedCases++;
    }
    evidence.push({ key: { module, site, kind }, passed, ...(search ? { allowedChange: 'S11: requested vehicle search historical repair', expectedBase, expectedCandidate, base, candidate } : passed ? {} : { base, candidate }) });
  }
  return { passed: preservedCases === 22 && authorizedDifferences === 2, preservedCases, authorizedDifferences, total: 24, evidence };
}

function main(args) {
  if (args.length !== 6 || args[0] !== '--base-root' || args[2] !== '--candidate-root' || args[4] !== '--scratch') throw new Error('Usage: --base-root <dir> --candidate-root <dir> --scratch <empty-dir>');
  const base = directory(args[1]), candidate = directory(args[3]), scratch = directory(args[5]);
  const project = directory(path.resolve(__dirname, '..'));
  for (const [a, b] of [[base, candidate], [base, scratch], [candidate, scratch], [project, scratch]]) {
    if (within(a, b) || within(b, a)) throw new Error('Roots and scratch must be disjoint');
  }
  if (fs.readdirSync(scratch).length) throw new Error('Scratch must be empty');
  const results = {};
  for (const [label, root] of [['base', base], ['candidate', candidate]]) {
    const child = spawnSync(process.execPath, [__filename, '--probe-root', root, '--candidate-checks', String(label === 'candidate')], {
      cwd: root, env: { ...process.env, PLAYWRIGHT_BROWSERS_PATH: directory(path.join(root, '.local/playwright-browsers')) }, shell: false, encoding: 'utf8', timeout: 120000, maxBuffer: 16 * 1024 * 1024
    });
    results[label] = { status: child.status, signal: child.signal, error: child.error ? String(child.error) : null, stdout: child.stdout || '', stderr: child.stderr || '' };
  }
  const a = results.base, b = results.candidate;
  let baseCases, candidateCases;
  try { baseCases = JSON.parse(a.stdout); candidateCases = JSON.parse(b.stdout); } catch (_) { /* invalid output fails closed */ }
  const comparison = requestedComparison(baseCases, candidateCases);
  const processesClean = [a, b].every(item => item.status === 0 && item.signal === null && item.error === null && item.stderr === '');
  const passed = processesClean && comparison.passed;
  const report = { result: passed ? 'PASS' : 'FAIL', classification: passed ? 'S11_REQUESTED_HISTORICAL_REPAIR' : null, processesClean, comparison, results };
  fs.writeFileSync(path.join(scratch, 'report.json'), JSON.stringify(report, null, 2), { flag: 'wx' });
  console.log(JSON.stringify(report, null, 2));
  process.exitCode = passed ? 0 : 1;
}
if (require.main === module) {
  if (process.argv[2] === '--probe-root') {
    if (process.argv.length !== 6 || process.argv[4] !== '--candidate-checks' || !['true', 'false'].includes(process.argv[5])) { console.error('Invalid probe arguments'); process.exitCode = 2; }
    else probe(directory(process.argv[3]), process.argv[5] === 'true').then(data => console.log(JSON.stringify(data))).catch(error => { console.error(error.stack || String(error)); process.exitCode = 1; });
  } else {
    try { main(process.argv.slice(2)); } catch (error) { console.error(error.stack || String(error)); process.exitCode = 2; }
  }
}
module.exports = { probe };
