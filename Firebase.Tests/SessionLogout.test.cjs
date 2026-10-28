const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

function setup({ firebaseFails = false, serverFails = false, redirected = false, responseType, responseStatus = 204, storageFails = false, channel = false, firebasePromise, timer = setTimeout } = {}) {
    const handlers = {};
    const requests = [];
    const broadcasts = [];
    const alerts = [];
    let signouts = 0;
    const form = { action: '/Login/Logout', addEventListener: (_, fn) => { handlers.submit = fn; },
        querySelector: () => ({ value: 'token' }) };
    const window = { location: { href: '' }, alert(message) { alerts.push(message); }, addEventListener: (name, fn) => { handlers[name] = fn; },
        localStorage: { setItem: (key, value) => { if (storageFails) throw Error('storage'); broadcasts.push([key, value]); } },
        firebaseSignOut: async () => { signouts++; if (firebaseFails) throw Error('firebase'); if (firebasePromise) await firebasePromise; } };
    if (channel) window.BroadcastChannel = class { constructor() { handlers.channel = this; } postMessage(value) { broadcasts.push(['channel', value]); } };
    const document = { getElementById: () => form, querySelectorAll: () => [] };
    const fetch = async (url, options) => { requests.push({ url, options }); if (serverFails) throw Error('server'); return { ok: responseStatus === 204, status: responseStatus, redirected, type: responseType }; };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../wwwroot/js/session-logout.js'), 'utf8'),
        { window, document, fetch, console: { error() {} }, URLSearchParams, setTimeout: timer, clearTimeout });
    return { handlers, window, requests, broadcasts, alerts, get signouts() { return signouts; } };
}

test('explicit logout clears both authentications and broadcasts using protected POST', async () => {
    const s = setup();
    await s.handlers.submit({ preventDefault() {} });
    assert.equal(s.requests.length, 1);
    assert.equal(s.requests[0].url, '/Login/Logout');
    assert.equal(s.requests[0].options.method, 'POST');
    assert.equal(s.requests[0].options.headers['X-Requested-With'], 'XMLHttpRequest');
    assert.equal(s.requests[0].options.body.get('__RequestVerificationToken'), 'token');
    assert.equal(s.signouts, 1);
    assert.equal(s.broadcasts.length, 1);
    assert.equal(s.window.location.href, '/Login/Index');
});

test('Firebase failure does not prevent server logout', async () => {
    const s = setup({ firebaseFails: true });
    await s.handlers.submit({ preventDefault() {} });
    assert.equal(s.requests.length, 1);
    assert.equal(s.window.location.href, '/Login/Index');
    assert.match(s.alerts[0], /Firebase/);
});

test('server failure does not falsely report logout or broadcast', async () => {
    const s = setup({ serverFails: true });
    await s.handlers.submit({ preventDefault() {} });
    assert.equal(s.signouts, 1);
    assert.equal(s.broadcasts.length, 0);
    assert.equal(s.window.location.href, '');
});

test('other tabs react to logout and expiry preserves the reason', async () => {
    const s = setup();
    await s.handlers.storage({ key: 'app-logout', newValue: 'logout' });
    assert.equal(s.window.location.href, '/Login/Index');
    const e = setup();
    await e.window.appLogout('duration');
    assert.equal(e.requests.length, 1);
    assert.equal(e.window.location.href, '/Login/Index?expired=duration');
    const i = setup();
    await i.window.appLogout('inactivity');
    assert.equal(i.requests.length, 1);
    assert.equal(i.window.location.href, '/Login/Index?expired=inactivity');
});

test('redirected 200 cannot prove server logout', async () => {
    const s = setup({ redirected: true });
    await s.handlers.submit({ preventDefault() {} });
    assert.equal(s.requests[0].options.redirect, 'manual');
    assert.equal(s.broadcasts.length, 0);
    assert.equal(s.window.location.href, '');
    assert.equal(s.alerts.length, 1);
});

test('expired-session redirect is treated as ended and notifies other tabs', async () => {
    const s = setup({ redirected: false, responseType: 'opaqueredirect', responseStatus: 0 });
    await s.window.appLogout('inactivity');
    assert.equal(s.requests[0].options.redirect, 'manual');
    assert.equal(s.broadcasts.length, 1);
    assert.equal(s.window.location.href, '/Login/Index?expired=inactivity');
    assert.equal(s.alerts.length, 0);
});

test('unavailable storage uses broadcast channel or reports synchronization failure', async () => {
    const supported = setup({ storageFails: true, channel: true });
    await supported.handlers.submit({ preventDefault() {} });
    assert.equal(supported.broadcasts.length, 1);
    assert.equal(supported.window.location.href, '/Login/Index');
    const unavailable = setup({ storageFails: true });
    await unavailable.handlers.submit({ preventDefault() {} });
    assert.equal(unavailable.requests.length, 1);
    assert.equal(unavailable.broadcasts.length, 0);
    assert.equal(unavailable.alerts.length, 1);
    assert.match(unavailable.alerts[0], /otras pestañas/);
});

test('receiving tab waits for Firebase cleanup before navigating', async () => {
    let resolve;
    const firebasePromise = new Promise(r => { resolve = r; });
    const s = setup({ firebasePromise });
    const processing = s.handlers.storage({ key: 'app-logout', newValue: 'logout' });
    assert.equal(s.window.location.href, '');
    resolve();
    await processing;
    assert.equal(s.window.location.href, '/Login/Index');
});

test('broadcast channel receiver waits and reports Firebase timeout', async () => {
    const s = setup({ channel: true, firebasePromise: new Promise(() => {}), timer: callback => { callback(); return 0; } });
    await s.handlers.channel.onmessage({ data: 'logout' });
    assert.match(s.alerts[0], /Firebase/);
    assert.equal(s.window.location.href, '/Login/Index');
});

test('a stalled Firebase operation never blocks server logout', async () => {
    const s = setup({ firebasePromise: new Promise(() => {}), timer: callback => { callback(); return 0; } });
    await s.handlers.submit({ preventDefault() {} });
    assert.equal(s.requests.length, 1);
    assert.equal(s.broadcasts.length, 1);
    assert.match(s.alerts[0], /Firebase/);
    assert.equal(s.window.location.href, '/Login/Index');
});
