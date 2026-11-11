'use strict';

const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { createRequire } = require('node:module');
const { spawnSync } = require('node:child_process');

const usage = 'Usage: node scripts/verify-wash-modal-differential.cjs --base-root <existingExport> --candidate-root <existingExport> --scratch <existingOwnRuntimeDirectory>';
const ids = ['cancelarModal', 'pagoModal', 'finalizarModal', 'detalleModal', 'tiempoExcedidoModal'];

function directory(value, label) {
  if (!value) throw new Error(`Missing ${label}`);
  const real = fs.realpathSync(value);
  if (!fs.statSync(real).isDirectory()) throw new Error(`${label} must be a directory`);
  return real;
}
function file(root, name) {
  const target = path.join(root, name);
  if (!fs.statSync(target, { throwIfNoEntry: false })?.isFile()) throw new Error(`Missing file: ${target}`);
  return target;
}
function extract(source, startMarker, endMarker, label) {
  const start = source.indexOf(startMarker);
  const end = source.indexOf(endMarker, start + startMarker.length);
  if (start < 0 || end <= start || source.indexOf(startMarker, start + 1) !== -1 ||
      source.indexOf(endMarker, end + 1) !== -1) throw new Error(`${label}: missing, repeated or inverted boundaries`);
  const fragment = source.slice(start, end);
  new vm.Script(fragment, { filename: label });
  return fragment;
}
function prepare(root) {
  const source = fs.readFileSync(file(root, 'wwwroot/js/lavado.js'), 'utf8');
  const view = fs.readFileSync(file(root, 'Views/Lavados/Index.cshtml'), 'utf8');
  const setup = '    // SETUP DE BOTONES DE CIERRE';
  const helper = extract(source, '    function abrirModal(modalId)', setup, 'modal helper');
  const tail = source.slice(source.indexOf(setup));
  const handler = extract(tail, "    document.addEventListener('DOMContentLoaded', function () {", '    // MONITOREO DE TIEMPO', 'close handler');
  const startMarker = '<!-- Modal de Confirmación para Cancelar -->';
  const endMarker = '<!-- Modal de Pago -->';
  const start = view.indexOf(startMarker);
  const end = view.indexOf(endMarker, start + startMarker.length);
  if (start < 0 || view.indexOf(startMarker, start + 1) !== -1 || end <= start) throw new Error('Cancellation modal boundaries invalid');
  const section = view.slice(start, end);
  if ((section.match(/<div id="cancelarModal"/g) || []).length !== 1) throw new Error('Expected one cancellation modal within section');
  const forms = section.match(/<form id="formCancelar"[\s\S]*?<\/form>/g);
  if (!forms || forms.length !== 1 || !forms[0].includes('@Html.AntiForgeryToken()')) throw new Error('Expected one cancellation form with antiforgery token within modal section');
  const form = forms[0].replace('@Html.AntiForgeryToken()', '<input type="hidden" name="__RequestVerificationToken" value="fixture-token">');
  return { helper, handler, form };
}

async function probe(root, dependencies) {
  const { helper, handler, form } = prepare(root);
  process.env.PLAYWRIGHT_BROWSERS_PATH = path.join(dependencies, '.local', 'playwright-browsers');
  const { chromium } = createRequire(path.join(dependencies, 'package.json'))('@playwright/test');
  const flowbite = file(dependencies, 'node_modules/flowbite/dist/flowbite.min.js');
  const browser = await chromium.launch({ headless: true });
  const behavior = [];
  const layers = [];
  try {
    for (const mode of ['absent', 'registered']) for (const id of ids) {
      const page = await browser.newPage({ viewport: { width: 1000, height: 700 } });
      const errors = [];
      const requests = [];
      page.on('pageerror', error => errors.push(String(error)));
      await page.route('**/*', route => { requests.push({ method: route.request().method(), url: route.request().url() }); return route.abort(); });
      try {
        const fixture = `<style>body{margin:0}.hidden{display:none!important}.fixed{position:fixed}.inset-0{inset:0}.z-40{z-index:40}.z-50{z-index:50}.nav{position:fixed;top:0;width:100%;height:60px;z-index:50}.panel{position:absolute;left:400px;top:250px;width:200px;height:180px;background:white}</style><nav class="nav">navbar</nav><button data-modal-target="${id}" data-modal-toggle="${id}">toggle</button><div id="${id}" tabindex="-1" aria-hidden="true" class="hidden fixed inset-0 z-50"><div class="panel"><button type="button" data-modal-hide="${id}">close</button>${id === 'cancelarModal' ? form : 'panel'}</div></div>`;
        await page.setContent(fixture);
        await page.addScriptTag({ content: helper });
        await page.addScriptTag({ content: handler });
        await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
        if (mode === 'registered') {
          await page.addScriptTag({ path: flowbite });
          await page.evaluate(() => window.initModals());
          const registered = await page.evaluate(modalId => window.FlowbiteInstances.instanceExists('Modal', modalId), id);
          if (!registered) throw new Error(`Flowbite did not register ${id}`);
        }
        await page.evaluate(() => {
          const formElement = document.querySelector('#formCancelar');
          if (formElement) { formElement.elements.id.value = 'wash'; formElement.elements.motivo.value = 'reason'; }
        });
        const snapshot = () => page.evaluate(modalId => {
          const modal = document.getElementById(modalId);
          const formElement = document.querySelector('#formCancelar');
          return {
            exports: { open: typeof window.abrirModal, close: typeof window.cerrarModal },
            hidden: modal.classList.contains('hidden'), ariaHidden: modal.getAttribute('aria-hidden'),
            ariaModal: modal.getAttribute('aria-modal'), role: modal.getAttribute('role'),
            locked: document.body.classList.contains('overflow-hidden'),
            backdrops: document.querySelectorAll('[modal-backdrop],#modal-backdrop').length,
            form: formElement ? { action: formElement.getAttribute('action'), method: formElement.method,
              payload: Array.from(new FormData(formElement).entries()) } : null,
          };
        }, id);
        const states = [{ step: 'initial', state: await snapshot() }];
        for (let cycle = 0; cycle < 2; cycle++) {
          await page.evaluate(modalId => window.abrirModal(modalId), id);
          states.push({ step: `open-${cycle}`, state: await snapshot() });
          layers.push({ mode, id, cycle, ...await page.evaluate(modalId => {
            const modal = document.getElementById(modalId);
            const backdrop = document.querySelector('[modal-backdrop],#modal-backdrop');
            const panel = document.elementFromPoint(410, 260);
            const actualNavHit = document.elementFromPoint(10, 10) === document.querySelector('nav');
            modal.style.pointerEvents = 'none';
            const top = document.elementFromPoint(10, 10);
            modal.style.pointerEvents = '';
            return { modalZ: getComputedStyle(modal).zIndex, backdropZ: backdrop ? getComputedStyle(backdrop).zIndex : null,
              panelHit: !!panel?.closest('.panel'), actualNavHit, diagnosticBackdropCoversNavWithModalPointerEventsDisabled: top !== document.querySelector('nav') };
          }, id) });
          await page.evaluate(modalId => document.getElementById(modalId).dispatchEvent(new MouseEvent('click', { bubbles: true })), id);
          await page.keyboard.press('Escape');
          states.push({ step: `outside-escape-${cycle}`, state: await snapshot() });
          await page.evaluate(modalId => document.querySelector(`[data-modal-hide="${modalId}"]`).click(), id);
          states.push({ step: `close-button-${cycle}`, state: await snapshot() });
        }
        behavior.push({ mode, id, states, requests, errors });
      } finally { await page.close(); }
    }
  } finally { await browser.close(); }
  return { behavior, layers };
}

function differences(base, candidate, prefix = '$') {
  if (Object.is(base, candidate)) return [];
  if (!base || !candidate || typeof base !== 'object' || typeof candidate !== 'object' || Array.isArray(base) !== Array.isArray(candidate)) {
    return [{ path: prefix, base: base === undefined ? null : base, candidate: candidate === undefined ? null : candidate }];
  }
  const keys = new Set([...Object.keys(base), ...Object.keys(candidate)]);
  return [...keys].flatMap(key => differences(base[key], candidate[key], `${prefix}.${key}`));
}
function isWithin(parent, child) {
  const relative = path.relative(parent, child);
  return relative === '' || (relative !== '..' && !relative.startsWith('..' + path.sep) && !path.isAbsolute(relative));
}
function main(args) {
  if (args.length === 1 && args[0] === '--help') {
    console.log(`${usage}\nRequires two existing exports, candidate Playwright/Flowbite dependencies and browser cache, and an existing empty runtime-owned scratch directory outside the exports and worktree. Runs two browser probes; writes raw worker output and a JSON report. Exit 0: behavior matches; 1: mismatch or probe failure; 2: invalid setup. Layer results are reported separately, not compared as preservation.`);
    return;
  }
  if (args.length !== 6 || args[0] !== '--base-root' || args[2] !== '--candidate-root' || args[4] !== '--scratch') throw new Error(usage);
  const base = directory(args[1], 'base root');
  const candidate = directory(args[3], 'candidate root');
  const scratch = directory(args[5], 'scratch');
  const worktree = fs.realpathSync(path.resolve(__dirname, '..'));
  if ([base, candidate, worktree].some(root => isWithin(root, scratch) || isWithin(scratch, root))) throw new Error('scratch must be disjoint from exports and worktree');
  if (fs.readdirSync(scratch).length) throw new Error('scratch must be empty');
  for (const root of [base, candidate]) prepare(root);
  file(candidate, 'package.json');
  file(candidate, 'node_modules/flowbite/dist/flowbite.min.js');
  directory(path.join(candidate, '.local/playwright-browsers'), 'browser cache');
  createRequire(path.join(candidate, 'package.json')).resolve('@playwright/test');
  const results = {};
  for (const [label, root] of [['base', base], ['candidate', candidate]]) {
    const result = spawnSync(process.execPath, [__filename, '--probe-root', root, '--dependencies', candidate], { encoding: 'utf8', timeout: 300000, maxBuffer: 16 * 1024 * 1024 });
    const stdout = result.stdout || '';
    const stderr = result.stderr || '';
    fs.writeFileSync(path.join(scratch, `${label}.stdout.json`), stdout, { flag: 'wx' });
    fs.writeFileSync(path.join(scratch, `${label}.stderr.log`), stderr, { flag: 'wx' });
    let data = null;
    try { data = JSON.parse(stdout); } catch { /* reported as a failed probe */ }
    results[label] = { status: result.status, signal: result.signal, error: result.error ? String(result.error) : null, stderr, data };
  }
  const a = results.base, b = results.candidate;
  const behaviorDiffs = a.data && b.data ? differences(a.data.behavior, b.data.behavior) : [{ path: '$.behavior', base: 'unavailable', candidate: 'unavailable' }];
  const processDiffs = differences({ status: a.status, signal: a.signal, error: a.error, stderr: a.stderr }, { status: b.status, signal: b.signal, error: b.error, stderr: b.stderr });
  const passed = a.status === 0 && b.status === 0 && !a.signal && !b.signal && !a.error && !b.error && behaviorDiffs.length === 0 && processDiffs.length === 0;
  const report = { result: passed ? 'PASS' : 'FAIL', behaviorDiffs, processDiffs, layers: { base: a.data?.layers ?? null, candidate: b.data?.layers ?? null }, rawPaths: { baseStdout: path.join(scratch, 'base.stdout.json'), baseStderr: path.join(scratch, 'base.stderr.log'), candidateStdout: path.join(scratch, 'candidate.stdout.json'), candidateStderr: path.join(scratch, 'candidate.stderr.log') } };
  const reportPath = path.join(scratch, 'report.json');
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2), { flag: 'wx' });
  console.log(JSON.stringify({ ...report, reportPath }, null, 2));
  process.exitCode = passed ? 0 : 1;
}

if (process.argv[2] === '--probe-root') {
  if (process.argv.length !== 6 || process.argv[4] !== '--dependencies') {
    console.error('Invalid internal probe arguments'); process.exitCode = 2;
  } else {
    probe(process.argv[3], process.argv[5]).then(result => console.log(JSON.stringify(result))).catch(error => { console.error(error.stack || String(error)); process.exitCode = 1; });
  }
} else {
  try { main(process.argv.slice(2)); }
  catch (error) { console.error(error.stack || String(error)); process.exitCode = 2; }
}
