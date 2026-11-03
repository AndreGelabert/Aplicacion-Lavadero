const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

test('configuration schedule warnings use shared notices without native alert', () => {
    const code = fs.readFileSync('wwwroot/js/configuracion.js', 'utf8');
    assert.equal(/\balert\(/.test(code), false);
    assert.ok(code.includes('Notificaciones.aviso('));
});

test('login resend uses shared transport and notices on success and failure', async () => {
    let fail = false;
    const messages = [];
    const window = {
        Peticiones: { enviar: async () => ({ ok: !fail }) },
        Notificaciones: {
            cargando: () => () => {}, exito: text => messages.push(['success', text]),
            error: text => messages.push(['error', text])
        }
    };
    const document = { addEventListener() {}, getElementById: () => null, querySelectorAll: () => [] };
    const context = { window, document, console, setTimeout };
    vm.runInNewContext(fs.readFileSync('wwwroot/js/login.js', 'utf8'), context);
    await context.resendVerificationEmail();
    fail = true;
    await context.resendVerificationEmail();
    assert.deepEqual(messages, [['success', 'Correo de verificación reenviado.'], ['error', 'No se pudo reenviar el correo.']]);
});

test('flash bridge consumes only presentation keys and is loaded in both layouts', () => {
    const bridge = fs.readFileSync('Views/Shared/_NotificationMessages.cshtml', 'utf8');
    for (const key of ['Success', 'Error', 'SuccessMessage', 'ErrorMessage', 'ReviewMessage', 'ReviewError']) assert.ok(bridge.includes(`"${key}"`));
    assert.equal(bridge.includes('RoleChangeEvent'), false);
    for (const file of ['Views/Login/Index.cshtml', 'Views/Shared/_Layout.cshtml']) {
        const code = fs.readFileSync(file, 'utf8');
        assert.ok(code.includes('PartialAsync("_NotificationMessages")'));
        assert.ok(code.indexOf('~/js/notifications.js') < code.indexOf('~/js/requests.js'));
    }
    const controller = fs.readFileSync('Controllers/RolController.cs', 'utf8');
    assert.ok(controller.includes('TempData["Success"] = "Rol actualizado exitosamente"'));
});
