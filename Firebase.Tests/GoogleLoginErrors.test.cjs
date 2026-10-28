const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

async function login({ firebaseError, fetchError, response, brave = false } = {}) {
    let click;
    let errorContainer;
    let fetchCalls = 0;
    const errorLogs = [];
    const button = {
        addEventListener: (_, handler) => { click = handler; },
        parentNode: { insertBefore: element => { errorContainer = element; } }
    };
    const document = {
        addEventListener: (_, handler) => handler(),
        getElementById: () => button,
        querySelector: () => errorContainer,
        createElement: () => ({ style: {} })
    };
    const window = { location: { href: '' } };
    const source = fs.readFileSync(path.join(__dirname, '../wwwroot/js/GoogleConnectionModule.js'), 'utf8')
        .replace(/^import .*;\s*$/gm, '');
    vm.runInNewContext(source, {
        initializeApp: () => ({}), getAuth: () => ({}),
        GoogleAuthProvider: class { setCustomParameters() {} },
        signInWithPopup: async () => {
            if (firebaseError) throw firebaseError;
            return { user: { getIdToken: async () => 'mock-token' } };
        },
        fetch: async () => {
            fetchCalls++;
            if (fetchError) throw fetchError;
            return response;
        },
        document, window, navigator: brave ? { brave: { isBrave: async () => true } } : {},
        console: { log() {}, error(...args) { errorLogs.push(args); }, warn() {} }, setTimeout() {}
    });
    await click();
    return { message: errorContainer?.textContent, href: window.location.href, fetchCalls, errorLogs };
}

test('Firebase network error remains distinct from server connection failure', async () => {
    const firebase = await login({ firebaseError: { code: 'auth/network-request-failed', message: 'private Firebase detail' } });
    assert.equal(firebase.message, 'Error de conexión. Verifique su conexión a internet y intente de nuevo.');
    assert.equal(firebase.fetchCalls, 0);
    const server = await login({ fetchError: new Error('private server detail') });
    assert.equal(server.message, 'No se pudo conectar con el servidor. Por favor, intente de nuevo.');
    assert.equal(server.href, '');
});

test('invalid server JSON is reported safely for successful and unsuccessful responses', async () => {
    for (const ok of [true, false]) {
        const result = await login({ response: { ok, json: async () => { throw new Error('private response detail'); } } });
        assert.equal(result.message, 'Error en la respuesta del servidor. Por favor, intente de nuevo.');
        assert.equal(result.href, '');
        assert.equal(result.errorLogs.flat().some(value => String(value).includes('private response detail')), false);
        for (const value of [null, 'invalid', []]) {
            const malformed = await login({ response: { ok, json: async () => value } });
            assert.equal(malformed.message, 'Error en la respuesta del servidor. Por favor, intente de nuevo.');
            assert.equal(malformed.href, '');
        }
    }
});

test('unknown Firebase errors do not expose their message', async () => {
    const result = await login({ firebaseError: new Error('private authentication detail') });
    assert.equal(result.message, 'Error al iniciar sesión con Google. Por favor, intente de nuevo.');
    assert.equal(result.fetchCalls, 0);
    assert.equal(result.errorLogs.flat().some(value => String(value).includes('private authentication detail')), false);
});

test('existing popup messages and successful login remain unchanged', async () => {
    for (const [code, brave, message] of [
        ['auth/popup-closed-by-user', false, 'La ventana de autenticación fue cerrada. Por favor, intente de nuevo.'],
        ['auth/cancelled-popup-request', false, 'Solicitud de autenticación cancelada. Por favor, intente de nuevo.'],
        ['auth/popup-blocked', false, 'El navegador bloqueó el popup de autenticación. Por favor, permita popups para este sitio.'],
        ['auth/popup-blocked', true, 'Brave está bloqueando el popup de autenticación. Por favor, permita popups para este sitio en la configuración de Brave (ícono del escudo en la barra de direcciones).']
    ]) {
        assert.equal((await login({ firebaseError: { code }, brave })).message, message);
    }
    const result = await login({ response: { ok: true, json: async () => ({ redirectUrl: '/home' }) } });
    assert.equal(result.href, '/home');
    assert.equal(result.message, undefined);
});

test('existing server error and missing redirect messages remain unchanged', async () => {
    assert.equal((await login({ response: { ok: false, json: async () => ({ error: 'Sesión rechazada' }) } })).message, 'Sesión rechazada');
    assert.equal((await login({ response: { ok: false, json: async () => ({}) } })).message, 'Error desconocido al iniciar sesión con Google');
    assert.equal((await login({ response: { ok: true, json: async () => ({}) } })).message, 'Error en la respuesta del servidor. Por favor, intente de nuevo.');
});
