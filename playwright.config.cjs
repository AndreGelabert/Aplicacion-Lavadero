const path = require('node:path');
const root = __dirname;
process.env.PLAYWRIGHT_BROWSERS_PATH = path.join(root, '.local', 'playwright-browsers');
const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: path.join(root, 'tests', 'browser'),
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: path.join(root, 'playwright-report') }]],
  outputDir: path.join(root, 'test-results'),
  use: {
    baseURL: 'http://127.0.0.1:41783',
    browserName: 'chromium',
    headless: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: `node "${path.join(root, 'scripts', 'playwright.cjs')}" serve`,
    url: 'http://127.0.0.1:41783/fixture',
    reuseExistingServer: false,
    timeout: 10000,
  },
});
