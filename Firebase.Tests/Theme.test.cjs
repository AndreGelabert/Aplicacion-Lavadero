const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function page(saved, dark = false, blocked = false) {
  const classes = new Set();
  const listeners = {};
  const select = { value: '', addEventListener(type, fn) { listeners[type] = fn; } };
  const media = { matches: dark, addEventListener(type, fn) { listeners.media = fn; } };
  const storage = { value: saved, getItem() { if (blocked) throw Error(); return this.value; }, setItem(key, value) { if (blocked) throw Error(); this.value = value; } };
  const document = { documentElement: { classList: { toggle(name, enabled) { enabled ? classes.add(name) : classes.delete(name); } } }, readyState: 'loading', addEventListener(type, fn) { listeners[type] = fn; }, querySelectorAll() { return [select]; } };
  const window = { matchMedia() { return media; }, localStorage: storage };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/theme.js', 'utf8'), { window, document });
  return { classes, listeners, select, media, storage };
}

test('system initializes synchronously and follows operating system', () => {
  const p = page(null, true);
  assert.equal(p.classes.has('dark'), true);
  p.listeners.DOMContentLoaded();
  assert.equal(p.select.value, 'system');
  p.media.matches = false; p.listeners.media();
  assert.equal(p.classes.has('dark'), false);
});

test('explicit preference persists and overrides operating system', () => {
  const p = page('light', true);
  assert.equal(p.classes.has('dark'), false);
  p.listeners.DOMContentLoaded();
  p.select.value = 'dark'; p.listeners.change();
  assert.equal(p.storage.value, 'dark');
  assert.equal(p.classes.has('dark'), true);
  p.media.matches = false; p.listeners.media();
  assert.equal(p.classes.has('dark'), true);
  p.select.value = 'system'; p.listeners.change();
  assert.equal(p.storage.value, 'system');
  assert.equal(p.classes.has('dark'), false);
});

test('unavailable storage falls back to system and selection still works', () => {
  const p = page(null, false, true);
  p.listeners.DOMContentLoaded();
  p.select.value = 'dark'; p.listeners.change();
  assert.equal(p.classes.has('dark'), true);
});
