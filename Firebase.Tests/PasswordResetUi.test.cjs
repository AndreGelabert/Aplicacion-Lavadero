'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

async function run(response, value = ' person@example.com ') {
  const messages = [];
  const requests = [];
  const feedback = { classList: { add() {}, remove() {} }, textContent: '' };
  const input = { value };
  const document = { getElementById: id => id === 'resetEmail' ? input : id === 'forgotPasswordFeedback' ? feedback : null, addEventListener() {} };
  const window = {
    Notificaciones: { cargando: () => () => {}, exito: s => messages.push(['success', s]), error: s => messages.push(['error', s]) },
    Peticiones: { enviar: async (url, options) => { requests.push([url, JSON.parse(options.body)]); if (response instanceof Error) throw response; return response; } }
  };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/login.js', 'utf8'), { window, document, setTimeout() {} });
  await window.sendPasswordResetEmail();
  return { messages, requests, feedback: feedback.textContent };
}

test('accepted reset displays the non-enumerating message and sends trimmed email', async () => {
  const result = await run({ ok: true, json: async () => ({ message: 'ignored' }) });
  assert.deepEqual(result.messages, [['success', 'Si existe una cuenta asociada a ese correo, recibirás instrucciones para restablecer tu contraseña.']]);
  assert.equal(result.requests.length, 1);
  assert.equal(result.requests[0][0], '/Login/ForgotPassword');
  assert.equal(result.requests[0][1].email, 'person@example.com');
});

test('provider and network errors never expose response or exception details', async () => {
  for (const response of [{ ok: false, json: async () => ({ error: 'private account detail' }) }, new Error('private network detail')]) {
    const result = await run(response);
    assert.deepEqual(result.messages, [['error', 'Error al enviar el correo de recuperación. Por favor, intente de nuevo.']]);
    assert.equal(result.requests.length, 1);
  }
});

test('invalid email still prevents submission', async () => {
  const result = await run({}, 'bad');
  assert.equal(result.feedback, 'Por favor, ingrese un correo electrónico válido.');
  assert.equal(result.requests.length, 0);
});
