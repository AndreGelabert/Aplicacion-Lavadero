const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const root = path.resolve(__dirname, '..');
test('config fixes browser storage and fixture origin regardless of cwd and environment', () => {
  const result = spawnSync(process.execPath, ['-e', `
    const config = require(${JSON.stringify(path.join(root, 'playwright.config.cjs'))});
    console.log(JSON.stringify({ browserPath: process.env.PLAYWRIGHT_BROWSERS_PATH, use: config.use, server: config.webServer }));
  `], { cwd: path.parse(root).root, env: { ...process.env, PLAYWRIGHT_BROWSERS_PATH: '0', BASE_URL: 'https://example.com' }, encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  const resultConfig = JSON.parse(result.stdout);
  assert.equal(resultConfig.browserPath, path.join(root, '.local', 'playwright-browsers'));
  assert.equal(resultConfig.use.baseURL, 'http://127.0.0.1:41783');
  assert.equal(resultConfig.server.reuseExistingServer, false);
});
