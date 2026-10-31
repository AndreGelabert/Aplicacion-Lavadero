const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

test('customer and vehicle creation checkboxes stay synchronized in both directions', () => {
    const handlers = {};
    class Input { constructor(value) { this.name = 'Permisos'; this.value = value; this.checked = false; this.disabled = false; } }
    const boxes = [new Input('Clientes.Crear'), new Input('Vehiculos.Crear')];
    const document = {
        addEventListener: (type, fn) => { handlers[type] = fn; },
        getElementById: () => null,
        querySelectorAll: () => boxes,
        querySelector: selector => boxes.find(box => selector.includes(`value="${box.value}"`)) || null
    };
    const window = {};
    vm.runInNewContext(fs.readFileSync('wwwroot/js/roles.js', 'utf8'), { window, document, HTMLInputElement: Input });
    window.PageModules.roles.init();
    for (const box of boxes) {
        box.checked = true; handlers.change({ target: box });
        assert.ok(boxes.every(item => item.checked));
        box.checked = false; handlers.change({ target: box });
        assert.ok(boxes.every(item => !item.checked));
    }
});
