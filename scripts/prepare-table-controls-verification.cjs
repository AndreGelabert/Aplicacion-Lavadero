'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');
const { spawnSync } = require('node:child_process');

const baseline = 'c7d49ec7ad60a31e7d7636aad01667cf91b74318';
const shared = ['wwwroot/js/roles.js', 'wwwroot/js/personal.js', 'package.json'];
const candidateOnly = ['Firebase.Tests/TableControlsConsistency.test.cjs', 'tests/browser/table-controls-consistency.spec.cjs', 'scripts/verify-table-controls-differential.cjs'];
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
function main(args) {
  if (args.length !== 2 || args[0] !== '--scratch') throw new Error('Usage: node scripts/prepare-table-controls-verification.cjs --scratch <existingEmptyRuntimeDir>');
  const cwd = directory(path.resolve(__dirname, '..'));
  const scratch = directory(args[1]);
  if (within(cwd, scratch) || within(scratch, cwd)) throw new Error('Scratch and project must be disjoint');
  if (fs.readdirSync(scratch).length) throw new Error('Scratch must be empty');
  const modules = directory(path.join(cwd, 'node_modules'));
  const cache = directory(path.join(cwd, '.local', 'playwright-browsers'));
  const base = path.join(scratch, 'base');
  const candidate = path.join(scratch, 'candidate');
  const reports = path.join(scratch, 'reports');
  for (const root of [base, candidate, reports]) fs.mkdirSync(root);
  for (const name of shared) {
    const result = spawnSync('git', ['show', `${baseline}:${name}`], { cwd, shell: false, maxBuffer: 16 * 1024 * 1024 });
    if (result.error || result.status !== 0) throw new Error(`git show failed for ${name}: ${result.error || String(result.stderr)}`);
    write(base, name, result.stdout);
  }
  for (const name of [...shared, ...candidateOnly]) write(candidate, name, fs.readFileSync(path.join(cwd, name)));
  for (const root of [base, candidate]) {
    fs.mkdirSync(path.join(root, '.local'));
    for (const [source, target] of [[modules, path.join(root, 'node_modules')], [cache, path.join(root, '.local', 'playwright-browsers')]]) {
      fs.symlinkSync(source, target, 'junction');
      if (directory(target) !== source) throw new Error(`Incorrect dependency link: ${target}`);
    }
  }
  const requireCandidate = createRequire(path.join(candidate, 'package.json'));
  const resolved = fs.realpathSync(requireCandidate.resolve('@playwright/test'));
  if (!within(modules, resolved)) throw new Error('Playwright resolves outside project dependencies');
  process.env.PLAYWRIGHT_BROWSERS_PATH = cache;
  const { chromium } = requireCandidate('@playwright/test');
  const executable = fs.realpathSync(chromium.executablePath());
  if (!within(cache, executable) || !fs.statSync(executable).isFile()) throw new Error('Chromium executable is outside browser cache');
  const cli = fs.realpathSync(requireCandidate.resolve('@playwright/test/cli'));
  if (!within(modules, cli) || !fs.statSync(cli).isFile()) throw new Error('Playwright CLI is outside project dependencies');
  const config = path.join(scratch, 'playwright.config.cjs');
  fs.writeFileSync(config, `'use strict';\nmodule.exports = ${JSON.stringify({ testDir: path.join(candidate, 'tests', 'browser'), outputDir: path.join(scratch, 'browser-output') }, null, 2)};\n`, { flag: 'wx' });
  const commands = [
    { cwd: candidate, argv: [process.execPath, '--test', path.join(candidate, 'Firebase.Tests', 'TableControlsConsistency.test.cjs')] },
    { cwd: candidate, env: { PLAYWRIGHT_BROWSERS_PATH: cache }, argv: [process.execPath, cli, 'test', '--config', config, 'table-controls-consistency.spec.cjs'] },
    { cwd: candidate, env: { PLAYWRIGHT_BROWSERS_PATH: cache }, argv: [process.execPath, path.join(candidate, 'scripts', 'verify-table-controls-differential.cjs'), '--base-root', base, '--candidate-root', candidate, '--scratch', reports] }
  ];
  console.log(JSON.stringify({ cwd, base, candidate, reports, executable, cli, config, commands }, null, 2));
}
try { main(process.argv.slice(2)); } catch (error) { console.error(error.stack || String(error)); process.exitCode = 1; }
