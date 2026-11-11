const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

/**
 * Minimal browser environment for theme.js.
 *
 * @param {string|null} saved   - value in localStorage (null = absent)
 * @param {boolean}     dark    - whether OS prefers dark
 * @param {boolean}     blocked - whether localStorage throws
 * @param {boolean}     noMedia - whether matchMedia is absent (simulates fallback)
 */
function page(saved, dark = false, blocked = false, noMedia = false) {
  const classes = new Set();
  const listeners = {};

  // The toggle button exposed by the new _ThemeSelector markup.
  const toggle = {
    checked: false,
    ariaChecked: 'false',
    setAttribute(name, value) { if (name === 'aria-checked') this.ariaChecked = value; },
    addEventListener(type, fn) { listeners[type] = fn; },
  };

  const media = noMedia ? null : {
    matches: dark,
    addEventListener(type, fn) { listeners.media = fn; },
  };

  const storage = {
    value: saved,
    getItem()          { if (blocked) throw Error(); return this.value; },
    setItem(k, v)      { if (blocked) throw Error(); this.value = v; },
  };

  const document = {
    documentElement: {
      classList: {
        toggle(name, enabled) { enabled ? classes.add(name) : classes.delete(name); },
      },
    },
    readyState: 'loading',
    addEventListener(type, fn) { listeners[type] = fn; },
    querySelectorAll() { return [toggle]; },
  };

  const window = {
    matchMedia: noMedia ? null : () => media,
    localStorage: storage,
  };

  vm.runInNewContext(fs.readFileSync('wwwroot/js/theme.js', 'utf8'), { window, document });
  return { classes, listeners, toggle, media, storage };
}

// ── New behaviour: two-state toggle (light / dark) ───────────────────────────

test('no saved preference: OS dark → dark class applied immediately', () => {
  const p = page(null, true);
  assert.equal(p.classes.has('dark'), true);
});

test('no saved preference: OS light → no dark class applied immediately', () => {
  const p = page(null, false);
  assert.equal(p.classes.has('dark'), false);
});

test('no saved preference: matchMedia absent → light fallback', () => {
  const p = page(null, false, false, true);
  assert.equal(p.classes.has('dark'), false);
});

test('no saved preference: OS changes are reflected while no explicit choice is set', () => {
  const p = page(null, false);
  assert.equal(p.classes.has('dark'), false);
  p.media.matches = true;
  p.listeners.media();
  assert.equal(p.classes.has('dark'), true);
  p.media.matches = false;
  p.listeners.media();
  assert.equal(p.classes.has('dark'), false);
});

test('explicit saved light overrides OS dark', () => {
  const p = page('light', true);
  assert.equal(p.classes.has('dark'), false);
});

test('explicit saved dark overrides OS light', () => {
  const p = page('dark', false);
  assert.equal(p.classes.has('dark'), true);
});

test('explicit saved preference stops OS changes from overriding', () => {
  const p = page('light', true);
  assert.equal(p.classes.has('dark'), false);
  // OS changes should NOT override an explicit preference.
  if (p.media) { p.media.matches = true; p.listeners.media?.(); }
  assert.equal(p.classes.has('dark'), false);
});

test('toggle click: light → dark saves dark and applies class', () => {
  const p = page(null, false); // starts light (OS)
  p.listeners.DOMContentLoaded();
  p.listeners.click();
  assert.equal(p.storage.value, 'dark');
  assert.equal(p.classes.has('dark'), true);
});

test('toggle click: dark → light saves light and removes class', () => {
  const p = page(null, true); // starts dark (OS)
  p.listeners.DOMContentLoaded();
  p.listeners.click();
  assert.equal(p.storage.value, 'light');
  assert.equal(p.classes.has('dark'), false);
});

test('toggle click with blocked storage: in-memory flip still applied', () => {
  const p = page(null, false, true); // storage blocked
  p.listeners.DOMContentLoaded();
  p.listeners.click(); // should flip to dark without throwing
  assert.equal(p.classes.has('dark'), true);
});

test('saved dark: toggle click flips to light and persists', () => {
  const p = page('dark', false);
  p.listeners.DOMContentLoaded();
  p.listeners.click();
  assert.equal(p.storage.value, 'light');
  assert.equal(p.classes.has('dark'), false);
});

test('legacy saved system value treated as absent (follows OS)', () => {
  // 'system' is not 'light'|'dark', so it must be ignored → follow OS.
  const p = page('system', true);
  assert.equal(p.classes.has('dark'), true); // OS wins
});

test('unknown saved value treated as absent (follows OS)', () => {
  const p = page('banana', false);
  assert.equal(p.classes.has('dark'), false); // OS wins (light)
});

test('matchMedia throws → light fallback applied synchronously', () => {
  // matchMedia exists but throws when called — must not propagate, must fall back to light.
  const classes = new Set();
  const listeners = {};
  const toggle = {
    ariaChecked: 'false',
    setAttribute(name, value) { if (name === 'aria-checked') this.ariaChecked = value; },
    addEventListener(type, fn) { listeners[type] = fn; },
  };
  const storage = { value: null, getItem() { return null; }, setItem() {} };
  const document = {
    documentElement: { classList: { toggle(name, enabled) { enabled ? classes.add(name) : classes.delete(name); } } },
    readyState: 'loading',
    addEventListener(type, fn) { listeners[type] = fn; },
    querySelectorAll() { return [toggle]; },
  };
  const window = {
    matchMedia() { throw new Error('not supported'); },
    localStorage: storage,
  };
  const vm = require('node:vm');
  const fs = require('node:fs');
  vm.runInNewContext(fs.readFileSync('wwwroot/js/theme.js', 'utf8'), { window, document });
  // matchMedia threw → no media object → fallback light.
  assert.equal(classes.has('dark'), false);
  assert.equal(toggle.ariaChecked, 'false');
});
