'use strict';

// Export only the public presentation inputs. This prepares prerequisites, not proof.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { spawnSync } = require('node:child_process');
const { createRequire } = require('node:module');

const baseline = 'c7d49ec7ad60a31e7d7636aad01667cf91b74318';
const files = ['package.json', ...['site', 'lavado', 'lavado-detalle', 'cliente', 'vehiculo', 'auditoria', 'paqueteservicio', 'personal', 'roles', 'servicio'].map(name => `wwwroot/js/${name}.js`)];
const within = (parent, child) => {
  const rel = path.relative(parent, child);
  return rel === '' || (rel !== '..' && !rel.startsWith('..' + path.sep) && !path.isAbsolute(rel));
};
function directory(value) {
  const real = fs.realpathSync(value);
  if (!fs.statSync(real).isDirectory()) throw new Error(`Not a directory: ${value}`);
  return real;
}
function write(root, name, data) {
  const target = path.join(root, name);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, data, { flag: 'wx' });
}
function main() {
  if (process.argv.length !== 2) throw new Error('Usage: node scripts/prepare-shared-presentation-verification.cjs');
  const project = directory(path.resolve(__dirname, '..'));
  const temp = directory(os.tmpdir());
  if (within(project, temp)) throw new Error('System temp must be outside project');
  const modules = directory(path.join(project, 'node_modules'));
  const cache = directory(path.join(project, '.local', 'playwright-browsers'));
  const scratch = fs.mkdtempSync(path.join(temp, 'shared-presentation-'));
  console.error(`Runtime scratch: ${scratch}`);
  if (within(project, scratch) || within(scratch, project)) throw new Error('Scratch and project must be disjoint');
  const base = path.join(scratch, 'base');
  const candidate = path.join(scratch, 'candidate');
  const reports = path.join(scratch, 'reports');
  for (const root of [base, candidate, reports]) fs.mkdirSync(root);
  for (const name of files) {
    const result = spawnSync('git', ['show', `${baseline}:${name}`], { cwd: project, shell: false, maxBuffer: 16 * 1024 * 1024 });
    if (result.error || result.status !== 0) throw new Error(`git show failed for ${name}: ${result.error || String(result.stderr)}`);
    write(base, name, result.stdout);
    write(candidate, name, fs.readFileSync(path.join(project, name)));
  }
  for (const root of [base, candidate]) {
    fs.mkdirSync(path.join(root, '.local'));
    for (const [source, target] of [[modules, path.join(root, 'node_modules')], [cache, path.join(root, '.local', 'playwright-browsers')]]) {
      fs.symlinkSync(source, target, 'junction');
      if (directory(target) !== source) throw new Error(`Incorrect dependency link: ${target}`);
    }
  }
  process.env.PLAYWRIGHT_BROWSERS_PATH = cache;
  const requireCandidate = createRequire(path.join(candidate, 'package.json'));
  const resolved = fs.realpathSync(requireCandidate.resolve('@playwright/test'));
  if (!within(modules, resolved)) throw new Error('Playwright resolves outside project dependencies');
  const { chromium } = requireCandidate('@playwright/test');
  const executable = fs.realpathSync(chromium.executablePath());
  if (!within(cache, executable) || !fs.statSync(executable).isFile()) throw new Error('Chromium executable is outside browser cache');
  const manifest = { baseline, project, scratch, base, candidate, reports, executable, files, probes: 0 };
  fs.writeFileSync(path.join(scratch, 'manifest.json'), JSON.stringify(manifest, null, 2), { flag: 'wx' });
  console.log(JSON.stringify(manifest, null, 2));
}
try { main(); } catch (error) { console.error(error.stack || String(error)); process.exitCode = 1; }
