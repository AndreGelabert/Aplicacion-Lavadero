'use strict';

// Preparation only: payload bytes are read from explicit Git commits, never the worktree.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const { createRequire } = require('node:module');

const changed = [
  ...['dynamic-validation', 'shared-presentation', 'table-controls', 'vehicle-search', 'wash-modals'].map(n => `Documentacion/frontend-${n}.md`),
  ...['DynamicFormValidation', 'ModuleValidation', 'SharedPresentationConsumers', 'SharedPresentationUtilities', 'TableControlsConsistency', 'VehicleSearchSort', 'WashModalLayering'].map(n => `Firebase.Tests/${n}.test.cjs`),
  ...['prepare-shared-presentation-verification', 'prepare-table-controls-verification', 'verify-shared-presentation-differential', 'verify-table-controls-differential', 'verify-wash-modal-differential'].map(n => `scripts/${n}.cjs`),
  ...['dynamic-form-validation', 'module-validation', 'table-controls-consistency', 'wash-modal-layering'].map(n => `tests/browser/${n}.spec.cjs`),
  ...['auditoria', 'cliente-validation-integration', 'cliente', 'lavado-detalle', 'lavado', 'paqueteservicio', 'personal', 'roles', 'servicio', 'site', 'vehiculo'].map(n => `wwwroot/js/${n}.js`),
];
const shared = ['package.json', 'Views/Lavados/Index.cshtml', ...['site', 'lavado', 'lavado-detalle', 'cliente', 'vehiculo', 'auditoria', 'paqueteservicio', 'personal', 'roles', 'servicio'].map(n => `wwwroot/js/${n}.js`)];
const extra = ['playwright.config.cjs', 'scripts/playwright.cjs', 'Views/Auditoria/_AuditoriaDetalle.cshtml', 'Views/Rol/_RolForm.cshtml', ...['Cliente', 'Vehiculo', 'Servicio', 'PaqueteServicio', 'Lavados', 'Configuracion'].map(n => `Views/${n}/Index.cshtml`), ...['Cliente', 'Vehiculo', 'Servicio', 'PaqueteServicio', 'Lavado'].map(n => `Views/${n === 'Lavado' ? 'Lavados' : n}/_${n}Form.cshtml`), ...['form-validation', 'notifications', 'requests'].map(n => `wwwroot/js/${n}.js`)];
const candidateFiles = [...new Set([...changed, ...shared, ...extra])];
const hash = data => crypto.createHash('sha256').update(data).digest('hex');
const within = (a, b) => { const r = path.relative(a, b); return r === '' || (r !== '..' && !r.startsWith(`..${path.sep}`) && !path.isAbsolute(r)); };
function dir(p) { const r = fs.realpathSync(p); if (!fs.statSync(r).isDirectory()) throw Error(`Not a directory: ${p}`); return r; }
function git(args, project, encoding = 'utf8') {
  const r = spawnSync('git', args, { cwd: project, encoding, shell: false, maxBuffer: 16 * 1024 * 1024 });
  if (r.error || r.status !== 0) throw Error(`git ${args[0]} failed: ${r.error || String(r.stderr)}`);
  return r.stdout;
}
function main(args) {
  if (args.length !== 4 || args[0] !== '--base' || args[2] !== '--candidate' || !args.slice(1).filter((_, i) => i % 2 === 0).every(v => /^[0-9a-f]{40}$/.test(v))) throw Error('Usage: node scripts/prepare-frontend-integration-verification.cjs --base <full-commit> --candidate <full-commit>');
  const [, baseCommit, , candidateCommit] = args;
  const project = dir(path.resolve(__dirname, '..')), temp = dir(os.tmpdir());
  if (within(project, temp) || within(temp, project)) throw Error('System temp and project must be disjoint');
  for (const commit of [baseCommit, candidateCommit]) if (git(['rev-parse', `${commit}^{commit}`], project).trim() !== commit) throw Error('Commit must resolve exactly');
  const diff = git(['diff', '--name-only', baseCommit, candidateCommit, '--'], project).trim().split(/\r?\n/).filter(Boolean).sort();
  if (JSON.stringify(diff) !== JSON.stringify([...changed].sort())) throw Error('Candidate changed paths do not match the explicit 32-path boundary');
  const modules = dir(path.join(project, 'node_modules')), cache = dir(path.join(project, '.local/playwright-browsers'));
  const inputs = [];
  for (const [label, commit, files] of [['base', baseCommit, shared], ['candidate', candidateCommit, candidateFiles]]) for (const name of files) {
    if (!/^[A-Za-z0-9_./-]+$/.test(name) || name.split('/').includes('..') || /(^|\/)(appsettings|\.env|credentials?|secrets?)/i.test(name)) throw Error(`Unsafe path: ${name}`);
    const entry = git(['ls-tree', commit, '--', name], project).trim();
    if (!/^100644 blob [0-9a-f]{40}\t/.test(entry) || !entry.endsWith(`\t${name}`)) throw Error(`Not an exact regular Git file: ${name}`);
    const data = git(['show', `${commit}:${name}`], project, null);
    if (data.length > 2 * 1024 * 1024) throw Error(`Input exceeds limit: ${name}`);
    inputs.push({ label, name, data, size: data.length, sha256: hash(data) });
  }
  const totalBytes = inputs.reduce((n, item) => n + item.size, 0);
  if (totalBytes > 8 * 1024 * 1024) throw Error('Payload exceeds 8 MiB');
  const stat = fs.statfsSync(temp, { bigint: true });
  if (stat.bavail * stat.bsize < BigInt(totalBytes + 64 * 1024 * 1024)) throw Error('Insufficient scratch capacity');
  const scratch = fs.mkdtempSync(path.join(temp, 'frontend-integration-'));
  console.error(`Scratch: ${scratch}`);
  const base = path.join(scratch, 'base'), candidate = path.join(scratch, 'candidate');
  for (const root of [base, candidate]) fs.mkdirSync(root);
  for (const item of inputs) {
    const target = path.join(item.label === 'base' ? base : candidate, item.name);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, item.data, { flag: 'wx' });
    if (!fs.lstatSync(target).isFile() || hash(fs.readFileSync(target)) !== item.sha256) throw Error(`Export hash mismatch: ${item.name}`);
  }
  for (const root of [base, candidate]) {
    fs.mkdirSync(path.join(root, '.local'));
    for (const [source, target] of [[modules, path.join(root, 'node_modules')], [cache, path.join(root, '.local/playwright-browsers')]]) {
      fs.symlinkSync(source, target, 'junction');
      if (dir(target) !== source) throw Error(`Incorrect junction: ${target}`);
    }
  }
  process.env.PLAYWRIGHT_BROWSERS_PATH = cache;
  const localRequire = createRequire(path.join(candidate, 'package.json'));
  const resolved = fs.realpathSync(localRequire.resolve('@playwright/test'));
  const cli = fs.realpathSync(localRequire.resolve('@playwright/test/cli'));
  if (!within(modules, resolved) || !within(modules, cli)) throw Error('Playwright resolves outside dependency directory');
  const executable = fs.realpathSync(localRequire('@playwright/test').chromium.executablePath());
  if (!within(cache, executable) || !fs.statSync(executable).isFile()) throw Error('Chromium outside browser cache');
  const flowbite = fs.realpathSync(path.join(modules, 'flowbite/dist/flowbite.min.js'));
  if (!within(modules, flowbite) || !fs.statSync(flowbite).isFile()) throw Error('Flowbite outside dependencies');
  const env = { PLAYWRIGHT_BROWSERS_PATH: cache };
  const nodeTests = changed.filter(n => n.startsWith('Firebase.Tests/')).map(n => path.join(candidate, n));
  const specs = changed.filter(n => n.startsWith('tests/browser/')).map(n => path.basename(n));
  const commands = [
    { cwd: candidate, env, argv: [process.execPath, '--test', ...nodeTests] },
    { cwd: candidate, env: { ...env, PLAYWRIGHT_HTML_REPORT: path.join(scratch, 'html-report') }, argv: [process.execPath, path.join(candidate, 'scripts/playwright.cjs'), 'test', ...specs, `--output=${path.join(scratch, 'browser-output')}`] },
  ];
  for (const name of ['shared-presentation', 'table-controls', 'wash-modal']) {
    const report = path.join(scratch, `report-${name}`); fs.mkdirSync(report);
    commands.push({ cwd: candidate, env, argv: [process.execPath, path.join(candidate, `scripts/verify-${name}-differential.cjs`), '--base-root', base, '--candidate-root', candidate, '--scratch', report] });
  }
  const manifest = { baseCommit, candidateCommit, scratch, base, candidate, changed, totalBytes, modules, cache, executable, cli, flowbite, probes: 0, files: inputs.map(({ data, ...item }) => item), commands };
  const manifestPath = path.join(scratch, 'manifest.json');
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2), { flag: 'wx' });
  console.log(JSON.stringify({ manifestPath, ...manifest }, null, 2));
}
try { main(process.argv.slice(2)); } catch (error) { console.error(error.stack || String(error)); process.exitCode = 1; }
