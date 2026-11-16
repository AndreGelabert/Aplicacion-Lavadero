const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
test('las variantes del botón superan el reset de Flowbite cargado después', () => {
  const css = fs.readFileSync(path.join(root, 'wwwroot/css/app.css'), 'utf8');
  for (const variant of ['primary', 'secondary', 'danger', 'success']) {
    assert.match(css, new RegExp(`\\.app-button\\.app-button--${variant}\\s*\\{`));
  }
});
