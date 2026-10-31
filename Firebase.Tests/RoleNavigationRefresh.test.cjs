const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

for (const action of ['CrearRolAjax', 'ActualizarRolAjax']) {
    test(`refreshes navigation after successful ${action}, but not after rejection`, async () => {
        let reloads = 0;
        let success = true;
        const button = { disabled: false };
        const form = { action: `/Rol/${action}`, querySelector: () => button, querySelectorAll: () => [] };
        const document = {
            addEventListener() {},
            getElementById: () => null,
            body: { contains: () => true }
        };
        const window = { location: { reload: () => reloads++ } };
        const context = { window, document, console, FormData: class {}, fetch: async () => ({
            ok: true, json: async () => ({ success })
        }) };
        vm.runInNewContext(fs.readFileSync('wwwroot/js/roles.js', 'utf8'), context);
        window.closeRolForm = () => {};
        window.submitRolAjax(form, { preventDefault() {} });
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(reloads, 1);
        success = false;
        window.submitRolAjax(form, { preventDefault() {} });
        await new Promise(resolve => setImmediate(resolve));
        assert.equal(reloads, 1);
    });
}
