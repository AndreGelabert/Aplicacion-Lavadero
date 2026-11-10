'use strict';

// Auxiliary verifier: preparation is deliberately separate from the two probes.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { createRequire } = require('node:module');
const source = path.resolve(__dirname, '..');
const args = process.argv.slice(2);
if (args.length !== 5 || !['prepare', 'run'].includes(args[0]) || args[1] !== '--base' || args[3] !== '--scratch') {
  console.error('Usage: node scripts/verify-frontend-differentials.cjs prepare|run --base b70473d --scratch <runtimeOwnedExistingDir>');
  process.exit(2);
}
const [mode, , baseRef, , scratchArg] = args;
const scratch = fs.realpathSync(scratchArg);
const realSource = fs.realpathSync(source);
const relative = path.relative(scratch, realSource);
if (!fs.statSync(scratch).isDirectory() || relative === '' || (!relative.startsWith('..' + path.sep) && relative !== '..' && !path.isAbsolute(relative))) {
  throw new Error('scratch must be an existing directory that is neither source nor an ancestor of source');
}
// Also prohibit scratch inside source, to keep all generated output out of the worktree.
const insideSource = path.relative(realSource, scratch);
if (insideSource === '' || (!insideSource.startsWith('..' + path.sep) && insideSource !== '..' && !path.isAbsolute(insideSource))) {
  throw new Error('scratch must be outside source');
}
const base = path.join(scratch, 'BASE');
const candidate = path.join(scratch, 'candidate');
const manifestPath = path.join(scratch, 'manifest.json');
const cache = path.join(realSource, '.local', 'playwright-browsers');
const modules = path.join(realSource, 'node_modules');
const slash = value => value.split(String.fromCharCode(92)).join('/');
const excluded = name => {
  const parts = slash(name).split('/');
  return parts.some(part => ['.git', 'node_modules', 'bin', 'obj', '.local'].includes(part) ||
    /^appsettings.*\.json$/i.test(part) || /^\.env(?:\..*)?$/i.test(part) ||
    /^(?:credentials?|secrets?)(?:\..*)?$/i.test(part)) ||
    slash(name) === 'odd/tasks/google-authentication-safety.md';
};
function command(exe, argv, options = {}) {
  const result = spawnSync(exe, argv, { cwd: realSource, maxBuffer: 128 * 1024 * 1024, ...options });
  if (result.error || result.status !== 0) throw new Error(`${exe}: ${result.error || result.stderr?.toString() || `exit ${result.status}`}`);
  return result.stdout;
}
function requireFile(root, name) {
  if (!fs.statSync(path.join(root, name), { throwIfNoEntry: false })?.isFile()) throw new Error(`Missing required file: ${root}/${name}`);
}
function linkDependencies(root) {
  fs.mkdirSync(path.join(root, '.local'));
  const type = process.platform === 'win32' ? 'junction' : 'dir';
  fs.symlinkSync(modules, path.join(root, 'node_modules'), type);
  fs.symlinkSync(cache, path.join(root, '.local', 'playwright-browsers'), type);
}
function prepare() {
  if (!/^[a-fA-F0-9]{7,40}$/.test(baseRef)) throw new Error('base must be a hexadecimal commit id');
  if (fs.existsSync(base) || fs.existsSync(candidate) || fs.existsSync(manifestPath)) throw new Error('scratch already contains verifier output');
  for (const dir of [modules, cache]) if (!fs.statSync(dir, { throwIfNoEntry: false })?.isDirectory()) throw new Error(`Missing dependency directory: ${dir}`);
  const tree = command('git', ['ls-tree', '-r', '--name-only', '-z', baseRef]).toString().split('\0').filter(Boolean);
  const files = tree.filter(name => !excluded(name));
  const tests = files.filter(name => name.startsWith('Firebase.Tests/') && name.endsWith('.test.cjs')).sort();
  if (!tests.length) throw new Error('No historical Node test files');
  fs.mkdirSync(base);
  fs.mkdirSync(candidate);
  const archive = path.join(scratch, 'base.tar');
  if (fs.existsSync(archive)) throw new Error('base archive already exists');
  // Git archive writes raw tar bytes; no shell pipeline or repository copy.
  fs.writeFileSync(archive, command('git', ['archive', '--format=tar', baseRef, '--', ...files]));
  command('tar', ['-xf', 'base.tar', '-C', 'BASE'], { cwd: scratch });
  const current = command('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z']).toString().split('\0').filter(Boolean);
  for (const name of new Set(current)) {
    if (excluded(name)) continue;
    const src = path.join(realSource, name);
    const stat = fs.lstatSync(src, { throwIfNoEntry: false });
    if (!stat) continue; // tracked deletion
    if (!stat.isFile()) throw new Error(`Unsupported source entry: ${name}`);
    const dest = path.join(candidate, name);
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    fs.copyFileSync(src, dest);
  }
  for (const root of [base, candidate]) {
    linkDependencies(root);
    for (const name of [...tests, 'package.json', 'playwright.config.cjs', 'wwwroot/css/app.min.css', 'wwwroot/js/notifications.js', 'wwwroot/js/requests.js', 'wwwroot/js/lavado-detalle.js']) requireFile(root, name);
  }
  for (const name of ['Firebase.Tests/FormButtons.test.cjs', 'Firebase.Tests/Services/TableEmptyStateRenderingTests.cs', 'Views/Shared/_TableEmptyState.cshtml', 'tests/browser/form-buttons.spec.cjs']) requireFile(candidate, name);
  process.env.PLAYWRIGHT_BROWSERS_PATH = cache;
  const { chromium } = createRequire(path.join(realSource, 'package.json'))('@playwright/test');
  // Playwright's bundled executablePath points at the regular Chromium build. The
  // default headless launch uses its separate headless shell, so check both paths.
  const executable = chromium.executablePath();
  if (!fs.existsSync(executable)) throw new Error(`Missing Chromium: ${executable}`);
  const browserDirs = fs.readdirSync(cache).filter(name => name.startsWith('chromium_headless_shell-'));
  const shellNames = process.platform === 'win32' ? ['chrome-headless-shell-win64', 'chrome-headless-shell.exe'] : process.platform === 'darwin' ? ['chrome-headless-shell-mac', 'chrome-headless-shell'] : ['chrome-headless-shell-linux64', 'chrome-headless-shell'];
  if (!browserDirs.some(dir => fs.existsSync(path.join(cache, dir, ...shellNames)))) throw new Error('Missing Chromium headless shell');
  fs.writeFileSync(manifestPath, JSON.stringify({ ready: true, probes: 0, source: realSource, scratch, baseRef, base, candidate, tests, cache, executable }, null, 2));
  console.log(JSON.stringify({ ready: true, probes: 0, manifest: manifestPath, historicalTestFiles: tests.length }));
}

// Fixtures retain actual classes and elements from untouched controls:
// Views/Auditoria/Index.cshtml filter button and icon;
// Views/Auditoria/_AuditoriaTable.cshtml populated row/cell and detail button;
// Views/VehicleCatalogueReview/Index.cshtml back link.
const fixture = `<button id="filterDropdownButton" data-dropdown-toggle="filterDropdown" class="w-full md:w-auto flex items-center justify-center py-2 px-4 text-sm font-medium text-gray-900 focus:outline-none bg-white rounded-lg border border-gray-200 hover:bg-gray-100 hover:text-primary-700 focus:z-10 focus:ring-4 focus:ring-gray-200 dark:focus:ring-gray-700 dark:bg-gray-800 dark:text-gray-400 dark:border-gray-600 dark:hover:text-white dark:hover:bg-gray-700" type="button"><svg xmlns="http://www.w3.org/2000/svg" aria-hidden="true" class="h-4 w-4 mr-2 text-gray-400" viewbox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M3 3a1 1 0 011-1h12a1 1 0 011 1v3a1 1 0 01-.293.707L12 11.414V15a1 1 0 01-.293.707l-2 2A1 1 0 018 17v-5.586L3.293 6.707A1 1 0 013 6V3z" clip-rule="evenodd" /></svg>Filtros</button>
<a asp-controller="Vehiculo" asp-action="Index" class="text-blue-700 dark:text-blue-300 underline">Volver a vehículos</a>
<table class="w-full text-sm text-left text-gray-500 dark:text-gray-400"><tbody><tr class="border-b dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700"><td class="px-4 py-3 font-medium text-gray-900 whitespace-nowrap dark:text-white">01/01/2026 12:00:00</td><td class="px-4 py-3"><button type="button" data-audit-detail-id="example" class="text-blue-700 underline">Ver detalle</button></td></tr></tbody></table>`;
const properties = ['padding', 'fontSize', 'lineHeight', 'color', 'backgroundColor', 'borderWidth', 'borderColor', 'display'];
function normalizeTap(value) {
  return value.replace(/^(\s*(?:#\s*)?duration_ms)(?::|\s)\s*[^\r\n]*/gm, '$1 <timing>')
    .replace(/^(\s*(?:#\s*)?duration:)\s*[^\r\n]*/gm, '$1 <timing>');
}
function nodeProbe(root, tests, label) {
  const result = spawnSync(process.execPath, ['--test', '--test-reporter=tap', ...tests], {
    cwd: root, env: process.env, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024, timeout: 300000,
  });
  fs.writeFileSync(path.join(scratch, `${label}.stdout.log`), result.stdout || '');
  fs.writeFileSync(path.join(scratch, `${label}.stderr.log`), result.stderr || '');
  const meta = { status: result.status, signal: result.signal, error: result.error ? String(result.error) : null };
  fs.writeFileSync(path.join(scratch, `${label}.status.json`), JSON.stringify(meta, null, 2));
  return { ...meta, stdout: result.stdout || '', stderr: result.stderr || '' };
}
async function run() {
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  if (!manifest.ready || manifest.probes !== 0 || manifest.source !== realSource || manifest.scratch !== scratch || manifest.baseRef !== baseRef || manifest.base !== base || manifest.candidate !== candidate || manifest.cache !== cache || !Array.isArray(manifest.tests) || !manifest.tests.length) throw new Error('Invalid preparation manifest');
  const baseline = nodeProbe(base, manifest.tests, 'base-node');
  const current = nodeProbe(candidate, manifest.tests, 'candidate-node');
  const nodeMatch = baseline.status === 0 && current.status === 0 && !baseline.error && !current.error && !baseline.signal && !current.signal && normalizeTap(baseline.stdout) === normalizeTap(current.stdout) && normalizeTap(baseline.stderr) === normalizeTap(current.stderr);
  process.env.PLAYWRIGHT_BROWSERS_PATH = cache;
  const { chromium } = createRequire(path.join(realSource, 'package.json'))('@playwright/test');
  let browser;
  const diffs = [];
  try {
    browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    await page.route('**/*', route => route.abort());
    for (const theme of ['light', 'dark']) {
      const results = [];
      for (const root of [base, candidate]) {
        const css = fs.readFileSync(path.join(root, 'wwwroot/css/app.min.css'), 'utf8');
        await page.setContent(`<html class="${theme === 'dark' ? 'dark' : ''}"><head></head><body>${fixture}</body></html>`);
        await page.addStyleTag({ content: css });
        results.push(await page.evaluate(({ properties }) => {
          const selectors = ['#filterDropdownButton', '#filterDropdownButton svg', 'a', 'td', 'td button'];
          return Object.fromEntries(selectors.map(selector => {
            const style = getComputedStyle(document.querySelector(selector));
            return [selector, Object.fromEntries(properties.map(key => [key, style[key]]))];
          }));
        }, { properties }));
      }
      for (const selector of Object.keys(results[0])) for (const property of properties) {
        if (results[0][selector][property] !== results[1][selector][property]) diffs.push({ theme, selector, property, base: results[0][selector][property], candidate: results[1][selector][property] });
      }
    }
  } finally {
    if (browser) await browser.close();
  }
  fs.writeFileSync(path.join(scratch, 'style-diffs.json'), JSON.stringify(diffs, null, 2));
  console.log(JSON.stringify({ nodeMatch, baseStatus: baseline.status, candidateStatus: current.status, styleMatch: diffs.length === 0, diffs }));
  if (!nodeMatch || diffs.length) process.exitCode = 1;
}
try {
  if (mode === 'prepare') prepare();
  else run().catch(error => { console.error(error.stack || String(error)); process.exitCode = 1; });
} catch (error) {
  console.error(error.stack || String(error));
  process.exitCode = 1;
}
