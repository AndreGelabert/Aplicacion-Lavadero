const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
function read(file) {
    return fs.readFileSync(path.join(root, file), 'utf8');
}

test('Views/Insumos/Proveedores.cshtml contiene input con ID provider-tax-id y ayuda de formato', () => {
    const view = read('Views/Insumos/Proveedores.cshtml');
    assert.match(view, /id="provider-tax-id"/, 'Debe tener id provider-tax-id para registrar el validador');
    assert.match(view, /name="IdentificacionTributaria"/, 'Debe enlazar el modelo con IdentificacionTributaria');
    assert.match(view, /placeholder="20-12345678-9"/, 'Debe mostrar placeholder ejemplar');
    assert.match(view, /Formato: XX-XXXXXXXX-X o 11 dígitos/, 'Debe brindar texto de ayuda al usuario');
});

test('wwwroot/js/insumos-secondary.js implementa validador de CUIT Módulo 11 y formato en blur', () => {
    const js = read('wwwroot/js/insumos-secondary.js');
    assert.match(js, /registerCustomValidator\('provider-tax-id'/, 'Debe registrar el validador provider-tax-id');
    assert.match(js, /validarCuitArgentino/, 'Debe contener la función de validación de CUIT');
    assert.match(js, /\[5,\s*4,\s*3,\s*2,\s*7,\s*6,\s*5,\s*4,\s*3,\s*2\]/, 'Debe utilizar los coeficientes ponderadores de Módulo 11');
    assert.match(js, /document\.getElementById\('provider-tax-id'\)/, 'Debe obtener el elemento provider-tax-id');
    assert.match(js, /taxInput\.addEventListener\('blur'/, 'Debe auto-formatear con guiones en evento blur');
});

test('Services/InsumosService.cs valida el CUIT antes de persistir en GuardarConAuditoria', () => {
    const service = read('Services/InsumosService.cs');
    assert.match(service, /CuitHelper\.EsValido\(proveedor\.IdentificacionTributaria/, 'Debe invocar CuitHelper.EsValido');
    assert.match(service, /CuitHelper\.Formatear\(proveedor\.IdentificacionTributaria\)/, 'Debe normalizar a formato canónico');
    assert.match(service, /CuitHelper\.Limpiar\(x\.IdentificacionTributaria\)/, 'Debe verificar unicidad limpiando guiones');
});

test('Utils/CuitHelper.cs define lógica matemática oficial completa', () => {
    const helper = read('Utils/CuitHelper.cs');
    assert.match(helper, /class CuitHelper/, 'Debe definir la clase CuitHelper');
    assert.match(helper, /PrefijosValidos/, 'Debe validar prefijos oficiales');
    assert.match(helper, /Multiplicadores/, 'Debe contener el vector ponderador de Módulo 11');
    assert.match(helper, /EsValido/, 'Debe exponer EsValido con mensaje de error');
    assert.match(helper, /Formatear/, 'Debe exponer Formatear');
    assert.match(helper, /Limpiar/, 'Debe exponer Limpiar');
});

test('Validación matemática Módulo 11 en JavaScript (simulación)', () => {
    function validarCuit(cuit) {
        if (!cuit) return { isValid: false, message: 'El CUIT es obligatorio' };
        const clean = cuit.replace(/[-\s.]/g, '');
        if (!/^\d{11}$/.test(clean)) return { isValid: false, message: 'Debe contener exactamente 11 dígitos numéricos' };
        if (cuit.includes('-') && !/^\d{2}-\d{8}-\d{1}$/.test(cuit.trim())) {
            return { isValid: false, message: 'Formato inválido' };
        }
        const prefix = clean.substring(0, 2);
        const validPrefixes = ['20', '23', '24', '27', '30', '33', '34', '50', '51', '55'];
        if (!validPrefixes.includes(prefix)) return { isValid: false, message: 'Prefijo inválido' };

        const multipliers = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        let sum = 0;
        for (let i = 0; i < 10; i++) sum += parseInt(clean[i], 10) * multipliers[i];
        const mod = sum % 11;
        let expected = 11 - mod;
        if (expected === 11) expected = 0;
        else if (expected === 10) expected = 9;

        return { isValid: parseInt(clean[10], 10) === expected };
    }

    // Casos válidos
    assert.equal(validarCuit('20-27362459-8').isValid, true);
    assert.equal(validarCuit('20273624598').isValid, true);
    assert.equal(validarCuit('27-28033514-8').isValid, true);
    assert.equal(validarCuit('30-50001091-2').isValid, true);
    assert.equal(validarCuit('20-00000006-0').isValid, true); // Suma % 11 = 0
    assert.equal(validarCuit('20-00000497-9').isValid, true); // Suma % 11 = 1 -> delta 10 -> DV 9

    // Casos inválidos
    assert.equal(validarCuit('123').isValid, false);
    assert.equal(validarCuit('20-27362459-9').isValid, false); // DV incorrecto
    assert.equal(validarCuit('99-27362459-8').isValid, false); // Prefijo incorrecto
    assert.equal(validarCuit('201-2345678-9').isValid, false); // Formato guiones incorrecto
});
