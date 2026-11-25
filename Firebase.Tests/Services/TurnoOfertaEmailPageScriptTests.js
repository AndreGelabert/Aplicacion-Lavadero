'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/turno-oferta-email.js'), 'utf8');
const secret = 'private-token-123';
const fragment = `CascadeId=cascade&OfferId=offer&ClientId=client&Token=${secret}`;
const error = 'No pudimos procesar su respuesta. Consulte con el lavadero.';

function setup(hash = `#${fragment}`, response = async () => ({ ok: true })) {
  const elements = Object.fromEntries(['aceptar', 'rechazar', 'estado'].map(id => [id, {
    disabled: id !== 'estado', textContent: '', listeners: {},
    addEventListener(type, callback) { this.listeners[type] = callback; }
  }]));
  const calls = [], historyCalls = [], logs = [];
  const location = { hash, pathname: '/turno-oferta-email' };
  const history = { replaceState(...args) { historyCalls.push(args); location.hash = ''; } };
  const fetch = (...args) => { calls.push(args); return response(...args); };
  const console = Object.fromEntries(['log', 'warn', 'error', 'info'].map(k => [k, (...args) => logs.push(args)]));
  vm.runInNewContext(script, { location, history, URLSearchParams, document: { getElementById: id => elements[id] }, fetch, console });
  return { elements, calls, historyCalls, location, logs };
}
const tick = () => new Promise(resolve => setImmediate(resolve));
const click = (c, id) => c.elements[id].listeners.click?.();
function noDisclosure(c) {
  assert.equal(JSON.stringify([c.historyCalls, c.logs, Object.values(c.elements).map(e => e.textContent)]).includes(secret), false);
}

test('clears fragment immediately without submitting and enables explicit choice', () => {
  const c = setup();
  assert.deepEqual(c.historyCalls, [[null, '', '/turno-oferta-email']]);
  assert.equal(c.location.hash, '');
  assert.equal(c.calls.length, 0);
  assert.equal(c.elements.aceptar.disabled, false);
  assert.equal(c.elements.rechazar.disabled, false);
  noDisclosure(c);
});

for (const [button, choice] of [['aceptar', true], ['rechazar', false]]) {
  test(`${button} sends the explicit choice and extracted decoded values`, async () => {
    const c = setup('#CascadeId=cascade%20one&OfferId=offer&ClientId=client&Token=private-token-123');
    click(c, button); await tick();
    assert.equal(c.calls.length, 1);
    const [url, options] = c.calls[0];
    assert.equal(url, '/api/turno-oferta-email/responder');
    assert.equal(options.method, 'POST'); assert.equal(options.credentials, 'omit');
    assert.equal(options.redirect, 'error'); assert.equal(options.referrerPolicy, 'no-referrer');
    assert.equal(options.headers['Content-Type'], 'application/json');
    assert.deepEqual(JSON.parse(options.body), { CascadeId: 'cascade one', OfferId: 'offer', ClientId: 'client', Token: secret, Accept: choice });
    assert.equal(c.elements.estado.textContent, 'Su respuesta fue registrada.');
    assert.equal(c.elements.aceptar.disabled, true); assert.equal(c.elements.rechazar.disabled, true);
    noDisclosure(c);
  });
}

test('suppresses repeated and opposing clicks while pending and after completion', async () => {
  let resolve;
  const c = setup(undefined, () => new Promise(r => { resolve = r; }));
  click(c, 'aceptar'); click(c, 'aceptar'); click(c, 'rechazar');
  assert.equal(c.calls.length, 1);
  assert.equal(c.elements.aceptar.disabled, true); assert.equal(c.elements.rechazar.disabled, true);
  resolve({ ok: true }); await tick(); click(c, 'rechazar');
  assert.equal(c.calls.length, 1); noDisclosure(c);
});

test('missing, blank and duplicate fragment fields fail closed', () => {
  for (const hash of ['', '#CascadeId=cascade&OfferId=offer&ClientId=client', `#${fragment}&Token=other`, `#${fragment.replace('OfferId=offer', 'OfferId=%20%20')}`]) {
    const c = setup(hash);
    assert.equal(c.location.hash, ''); assert.equal(c.elements.estado.textContent, error);
    assert.equal(c.elements.aceptar.disabled, true); assert.equal(c.elements.rechazar.disabled, true);
    click(c, 'aceptar'); click(c, 'rechazar'); assert.equal(c.calls.length, 0); noDisclosure(c);
  }
});

test('explicit invalid response is terminal', async () => {
  const c = setup(undefined, async () => ({ ok: false, status: 400 }));
  click(c, 'rechazar'); await tick();
  assert.equal(c.elements.estado.textContent, error);
  assert.equal(c.elements.aceptar.disabled, true); assert.equal(c.elements.rechazar.disabled, true);
  click(c, 'rechazar'); assert.equal(c.calls.length, 1); noDisclosure(c);
});

for (const [button, opposite] of [['aceptar', 'rechazar'], ['rechazar', 'aceptar']]) {
  for (const failure of ['network', 'server']) {
    test(`${button} retries only the identical action after ${failure} uncertainty`, async () => {
      let count = 0, resolve;
      const c = setup(undefined, () => {
        if (++count === 1) return failure === 'network' ? Promise.reject(new Error(secret)) : Promise.resolve({ ok: false, status: 503 });
        return new Promise(r => { resolve = r; });
      });
      click(c, button); await tick();
      assert.match(c.elements.estado.textContent, /no sabemos/i);
      assert.equal(c.elements[button].disabled, false); assert.equal(c.elements[opposite].disabled, true);
      click(c, opposite); assert.equal(c.calls.length, 1);
      click(c, button); click(c, button); click(c, opposite);
      assert.equal(c.calls.length, 2); assert.equal(c.calls[1][1].body, c.calls[0][1].body);
      assert.equal(c.elements[button].disabled, true);
      resolve({ ok: true }); await tick();
      assert.equal(c.elements.estado.textContent, 'Su respuesta fue registrada.');
      assert.equal(c.elements[button].disabled, true); click(c, button); assert.equal(c.calls.length, 2);
      noDisclosure(c);
    });
  }
}

test('invalid response after uncertainty does not claim the original action failed', async () => {
  let count = 0;
  const c = setup(undefined, async () => ++count === 1 ? Promise.reject(new Error(secret)) : ({ ok: false, status: 400 }));
  click(c, 'aceptar'); await tick(); click(c, 'aceptar'); await tick();
  assert.match(c.elements.estado.textContent, /no sabemos/i);
  assert.equal(c.elements.aceptar.disabled, true); assert.equal(c.elements.rechazar.disabled, true);
  click(c, 'aceptar'); assert.equal(c.calls.length, 2); noDisclosure(c);
});
