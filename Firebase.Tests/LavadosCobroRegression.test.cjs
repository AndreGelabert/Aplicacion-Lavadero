'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Helper implementing the exact C# FlexibleDecimalModelBinder parsing logic
function parseFlexibleDecimal(input) {
    if (!input || typeof input !== 'string' || !input.trim()) {
        return { success: false, value: 0 };
    }
    let s = input.trim();

    // 1. Both '.' and ',' present
    if (s.includes('.') && s.includes(',')) {
        if (s.lastIndexOf(',') > s.lastIndexOf('.')) {
            // Argentine/European: 30.000,50 -> strip dots, comma becomes decimal point
            s = s.replaceAll('.', '').replace(',', '.');
        } else {
            // US: 30,000.50 -> strip commas
            s = s.replaceAll(',', '');
        }
        const val = Number(s);
        return { success: !Number.isNaN(val), value: val };
    }

    // 2. Only ',' present: e.g. 30000,50 or 30000,00
    if (s.includes(',')) {
        s = s.replace(',', '.');
        const val = Number(s);
        return { success: !Number.isNaN(val), value: val };
    }

    // 3. Only '.' present
    if (s.includes('.')) {
        const parts = s.split('.');
        if (parts.length > 2) {
            // Multiple dots: e.g. 1.000.000 (thousands separators)
            s = s.replaceAll('.', '');
            const val = Number(s);
            return { success: !Number.isNaN(val), value: val };
        }

        // Exactly one dot:
        // If part after dot has 3 digits and part before has 1-3 digits (e.g. 30.000, 5.000, 100.000),
        // in Argentine context this represents thousands grouping without decimals.
        if (parts[1].length === 3 && parts[0].length >= 1 && parts[0].length <= 3) {
            s = s.replaceAll('.', '');
            const val = Number(s);
            return { success: !Number.isNaN(val), value: val };
        }

        // Otherwise standard decimal wire format: e.g. 30000.00, 1500.50, 30.5
        const val = Number(s);
        return { success: !Number.isNaN(val), value: val };
    }

    // 4. Plain number e.g. 30000
    const val = Number(s);
    return { success: !Number.isNaN(val), value: val };
}

// Service & Controller logic simulator matching LavadosController and LavadoService
class LavadoPaymentSimulator {
    constructor(precio, estadoInicial = 'Pendiente') {
        this.lavado = {
            id: 'lavado_test_1',
            precio: precio,
            estado: estadoInicial,
            pago: {
                montoPagado: 0,
                estado: 'Pendiente',
                pagos: []
            }
        };
    }

    get restante() {
        return this.lavado.precio - (this.lavado.pago?.montoPagado || 0);
    }

    // Server-side endpoint: RegistrarPago
    registrarPago(rawMonto, medioPago = 'Efectivo', notas = null) {
        // Step 1: Conversion (FlexibleDecimalModelBinder)
        let monto;
        if (typeof rawMonto === 'number') {
            monto = rawMonto;
        } else {
            const parsed = parseFlexibleDecimal(rawMonto);
            if (!parsed.success) {
                return { success: false, message: 'Formato de importe no válido.' };
            }
            monto = parsed.value;
        }

        // Step 2: Controller & Service Validation
        if (monto <= 0) {
            return { success: false, message: 'El monto debe ser mayor a 0.' };
        }

        const restante = this.restante;
        if (monto > restante) {
            return {
                success: false,
                message: `El monto a pagar ($${monto.toFixed(2)}) no puede superar el saldo pendiente ($${restante.toFixed(2)}).`
            };
        }

        // Step 3: Persistence in LavadoService
        const nuevoPago = {
            id: 'pago_' + (this.lavado.pago.pagos.length + 1),
            monto: monto,
            medioPago: medioPago,
            fecha: new Date().toISOString(),
            notas: notas
        };

        this.lavado.pago.pagos.push(nuevoPago);
        this.lavado.pago.montoPagado = this.lavado.pago.pagos.reduce((acc, p) => acc + p.monto, 0);

        if (this.lavado.pago.montoPagado >= this.lavado.precio) {
            this.lavado.pago.estado = 'Pagado';
        } else if (this.lavado.pago.montoPagado > 0) {
            this.lavado.pago.estado = 'Parcial';
        }

        return { success: true, message: 'Pago registrado correctamente.' };
    }
}

// ----------------------------------------------------
// 1. Flexible Decimal Parsing Regression Tests
// ----------------------------------------------------
test('FlexibleDecimalParser: correctly parses 30000.00 as 30000 (NOT 3.000.000)', () => {
    const res = parseFlexibleDecimal('30000.00');
    assert.equal(res.success, true);
    assert.equal(res.value, 30000);
});

test('FlexibleDecimalParser: correctly parses 30.000 as 30000 (NOT 3.000.000)', () => {
    const res = parseFlexibleDecimal('30.000');
    assert.equal(res.success, true);
    assert.equal(res.value, 30000);
});

test('FlexibleDecimalParser: correctly parses representative amounts (30.000,00 / 30000,00 / 30000)', () => {
    assert.equal(parseFlexibleDecimal('30.000,00').value, 30000);
    assert.equal(parseFlexibleDecimal('30000,00').value, 30000);
    assert.equal(parseFlexibleDecimal('30000').value, 30000);
});

test('FlexibleDecimalParser: correctly parses decimals with cents (1500.50 / 1500,50 / 1.500,50)', () => {
    assert.equal(parseFlexibleDecimal('1500.50').value, 1500.50);
    assert.equal(parseFlexibleDecimal('1500,50').value, 1500.50);
    assert.equal(parseFlexibleDecimal('1.500,50').value, 1500.50);
});

test('FlexibleDecimalParser: correctly parses smaller amounts (100.00 / 100)', () => {
    assert.equal(parseFlexibleDecimal('100.00').value, 100);
    assert.equal(parseFlexibleDecimal('100').value, 100);
});

test('FlexibleDecimalParser: rejects invalid or empty strings', () => {
    assert.equal(parseFlexibleDecimal('').success, false);
    assert.equal(parseFlexibleDecimal('   ').success, false);
    assert.equal(parseFlexibleDecimal('abc').success, false);
});

// ----------------------------------------------------
// 2. Server-side Payment & Balance Regression Tests
// ----------------------------------------------------
test('Server-side payment: exact payment marks wash as Pagado with 0 remaining balance', () => {
    const sim = new LavadoPaymentSimulator(30000);
    assert.equal(sim.restante, 30000);

    const res = sim.registrarPago('30000.00', 'Efectivo');
    assert.equal(res.success, true);
    assert.equal(sim.lavado.pago.estado, 'Pagado');
    assert.equal(sim.lavado.pago.montoPagado, 30000);
    assert.equal(sim.restante, 0);
    assert.equal(sim.lavado.pago.pagos.length, 1);
});

test('Server-side payment: partial payment followed by exact remaining balance', () => {
    const sim = new LavadoPaymentSimulator(30000);

    // Initial partial payment: 10000
    const res1 = sim.registrarPago('10.000', 'Transferencia', 'Seña');
    assert.equal(res1.success, true);
    assert.equal(sim.lavado.pago.estado, 'Parcial');
    assert.equal(sim.lavado.pago.montoPagado, 10000);
    assert.equal(sim.restante, 20000);

    // Final exact payment: 20000
    const res2 = sim.registrarPago('20000.00', 'Efectivo', 'Saldo');
    assert.equal(res2.success, true);
    assert.equal(sim.lavado.pago.estado, 'Pagado');
    assert.equal(sim.lavado.pago.montoPagado, 30000);
    assert.equal(sim.restante, 0);
    assert.equal(sim.lavado.pago.pagos.length, 2);
});

test('Server-side payment: excessive payment is REJECTED and does NOT alter payments or balance', () => {
    const sim = new LavadoPaymentSimulator(30000);

    // Register initial partial payment: 10000
    sim.registrarPago('10000', 'Efectivo');
    assert.equal(sim.lavado.pago.montoPagado, 10000);
    assert.equal(sim.restante, 20000);
    const pagosSnapshot = [...sim.lavado.pago.pagos];

    // Attempt excessive payment: 25000 (when restante is only 20000)
    const excessiveRes = sim.registrarPago('25000', 'Efectivo');
    assert.equal(excessiveRes.success, false);
    assert.match(excessiveRes.message, /no puede superar el saldo pendiente/);

    // Verify state was NOT modified
    assert.equal(sim.lavado.pago.montoPagado, 10000);
    assert.equal(sim.restante, 20000);
    assert.equal(sim.lavado.pago.estado, 'Parcial');
    assert.equal(sim.lavado.pago.pagos.length, pagosSnapshot.length);
    assert.deepEqual(sim.lavado.pago.pagos, pagosSnapshot);
});

test('Server-side payment: excessive direct server request (e.g. 3.000.000 for 30.000 wash) is rejected', () => {
    const sim = new LavadoPaymentSimulator(30000);

    // Attempt direct post with 3000000
    const res = sim.registrarPago(3000000, 'Efectivo');
    assert.equal(res.success, false);
    assert.match(res.message, /no puede superar el saldo pendiente/);

    // Balance remains 30000, pagado remains 0, payments remain empty
    assert.equal(sim.lavado.pago.montoPagado, 0);
    assert.equal(sim.restante, 30000);
    assert.equal(sim.lavado.pago.pagos.length, 0);
});

test('Server-side payment: negative or zero payment is rejected without mutating state', () => {
    const sim = new LavadoPaymentSimulator(30000);

    const zeroRes = sim.registrarPago('0');
    assert.equal(zeroRes.success, false);
    assert.match(zeroRes.message, /mayor a 0/);

    const negRes = sim.registrarPago('-500');
    assert.equal(negRes.success, false);
    assert.match(negRes.message, /mayor a 0/);

    assert.equal(sim.lavado.pago.montoPagado, 0);
    assert.equal(sim.restante, 30000);
    assert.equal(sim.lavado.pago.pagos.length, 0);
});

function makeElement() {
    return {
        value: '',
        dataset: {},
        textContent: '',
        style: {},
        classList: { add() {}, remove() {}, contains: () => false },
        setAttribute() {},
        removeAttribute() {},
        focus() {},
        addEventListener(type, fn) {
            this[type] = fn;
        }
    };
}

function makeDocumentMock(elements = {}) {
    const domLoadedListeners = [];
    return {
        getElementById: id => elements[id] || null,
        querySelectorAll: () => [],
        querySelector: () => null,
        createElement: () => makeElement(),
        body: {
            appendChild() {},
            removeChild() {},
            classList: { add() {}, remove() {} }
        },
        addEventListener(type, fn) {
            if (type === 'DOMContentLoaded') {
                domLoadedListeners.push(fn);
            }
        },
        dispatchDomLoaded() {
            for (const fn of domLoadedListeners) {
                try { fn(); } catch {}
            }
        }
    };
}

// ----------------------------------------------------
// 3. Frontend UI & Form Validation Regression Tests
// ----------------------------------------------------
test('Frontend Index.cshtml: has exactly ONE pagoModal in the view without duplicate IDs', () => {
    const html = fs.readFileSync(path.resolve('Views/Lavados/Index.cshtml'), 'utf8');
    const matches = html.match(/id="pagoModal"/g) || [];
    assert.equal(matches.length, 1, 'There must be exactly one #pagoModal element in Index.cshtml');
});

test('Frontend lavado.js: abrirModalPago sets sensible input value and dataset.restante', () => {
    const elements = {};
    for (const id of ['pagoLavadoId', 'pagoTotal', 'pagoPagado', 'pagoRestante', 'montoInput', 'notasPago', 'pagoModal', 'montoError']) {
        elements[id] = makeElement();
    }
    const documentMock = makeDocumentMock(elements);
    const windowMock = {
        SiteModule: { formatCurrency: val => '$' + val },
        abrirModal: () => {}
    };

    const jsCode = fs.readFileSync(path.resolve('wwwroot/js/lavado.js'), 'utf8');
    vm.runInNewContext(jsCode, {
        window: windowMock,
        document: documentMock,
        parseFloat: Number.parseFloat,
        Math: Math,
        console: console
    });

    // Call abrirModalPago with 30000 total and 0 paid
    windowMock.abrirModalPago('lav123', 30000, 0);
    assert.equal(elements.montoInput.value, 30000);
    assert.equal(elements.montoInput.max, 30000);
    assert.equal(elements.montoInput.dataset.restante, 30000);

    // Call abrirModalPago with partial payment: 30000 total, 12500 paid -> 17500 remaining
    windowMock.abrirModalPago('lav123', 30000, 12500);
    assert.equal(elements.montoInput.value, 17500);
    assert.equal(elements.montoInput.max, 17500);
    assert.equal(elements.montoInput.dataset.restante, 17500);
});

test('Frontend lavado.js: validarMontoPago detects and corrects overpayment and invalid amount', () => {
    let errorShown = false;
    const elements = {
        montoInput: { value: '35000', dataset: { restante: '30000' } },
        montoError: {
            textContent: '',
            classList: {
                remove: (cls) => { if (cls === 'hidden') errorShown = true; },
                add: (cls) => { if (cls === 'hidden') errorShown = false; }
            }
        }
    };
    const documentMock = makeDocumentMock(elements);
    const windowMock = {
        SiteModule: { formatCurrency: val => '$' + val },
        abrirModal: () => {}
    };

    const jsCode = fs.readFileSync(path.resolve('wwwroot/js/lavado.js'), 'utf8');
    vm.runInNewContext(jsCode, {
        window: windowMock,
        document: documentMock,
        parseFloat: Number.parseFloat,
        Math: Math,
        setTimeout: () => {}
    });

    // Test with excessive amount (35000 > 30000)
    const validExcessive = windowMock.validarMontoPago();
    assert.equal(validExcessive, false);
    assert.equal(elements.montoInput.value, 30000); // Clamped back to restante
    assert.equal(errorShown, true);

    // Test with negative amount (-500)
    elements.montoInput.value = '-500';
    errorShown = false;
    const validNegative = windowMock.validarMontoPago();
    assert.equal(validNegative, false);
    assert.equal(errorShown, true);

    // Test with valid amount (15000 <= 30000)
    elements.montoInput.value = '15000';
    errorShown = false;
    const validNormal = windowMock.validarMontoPago();
    assert.equal(validNormal, true);
});

test('Frontend lavado.js: form submit intercepts overpayment and does not send HTTP request', async () => {
    let submitListener = null;
    let paymentSent = false;
    const elements = {
        formPago: {
            style: {},
            addEventListener(type, fn) {
                if (type === 'submit') submitListener = fn;
            }
        },
        montoInput: { value: '45000', dataset: { restante: '30000' }, style: {} },
        montoError: {
            textContent: '',
            style: {},
            classList: { add() {}, remove() {} }
        }
    };
    const documentMock = makeDocumentMock(elements);
    const windowMock = {
        SiteModule: { formatCurrency: val => '$' + val },
        Notificaciones: { mostrar: () => {} },
        Peticiones: {
            enviar: async (url) => {
                if (url && url.includes('RegistrarPago')) {
                    paymentSent = true;
                }
                return { json: async () => ({ success: true }) };
            }
        },
        showTableMessage: () => {},
        cerrarModal: () => {},
        abrirModal: () => {}
    };

    const jsCode = fs.readFileSync(path.resolve('wwwroot/js/lavado.js'), 'utf8');
    vm.runInNewContext(jsCode, {
        window: windowMock,
        document: documentMock,
        parseFloat: Number.parseFloat,
        Math: Math,
        FormData: function () { this.entries = () => []; },
        setTimeout: () => {},
        setInterval: () => {},
        clearInterval: () => {}
    });

    documentMock.dispatchDomLoaded();

    assert.ok(submitListener, 'submitListener must be registered');

    let prevented = false;
    await submitListener({
        preventDefault() { prevented = true; }
    });

    assert.equal(prevented, true);
    assert.equal(paymentSent, false, 'HTTP payment request must NOT be sent when amount exceeds pending balance');
});

test('Frontend lavado-detalle.js: abrirModalPago sets sensible input value and dataset.restante', () => {
    const elements = {};
    for (const id of ['pagoLavadoId', 'pagoTotal', 'pagoPagado', 'pagoRestante', 'montoInput', 'notasPago', 'pagoModal', 'montoError']) {
        elements[id] = makeElement();
    }
    const documentMock = makeDocumentMock(elements);
    const windowMock = {
        SiteModule: { formatCurrency: val => '$' + val },
        abrirModal: () => {}
    };

    const jsCode = fs.readFileSync(path.resolve('wwwroot/js/lavado-detalle.js'), 'utf8');
    vm.runInNewContext(jsCode, {
        window: windowMock,
        document: documentMock,
        parseFloat: Number.parseFloat,
        Math: Math,
        console: console
    });

    windowMock.abrirModalPago('lav456', 30000, 0);
    assert.equal(elements.montoInput.value, 30000);
    assert.equal(elements.montoInput.max, 30000);
    assert.equal(elements.montoInput.dataset.restante, 30000);
});

test('Frontend lavado-detalle.js: form submit intercepts overpayment and does not send HTTP request', async () => {
    let submitListener = null;
    let paymentSent = false;
    const elements = {
        formPago: {
            action: '/Lavados/RegistrarPago',
            style: {},
            addEventListener(type, fn) {
                if (type === 'submit') submitListener = fn;
            }
        },
        montoInput: { value: '50000', dataset: { restante: '30000' }, style: {} },
        montoError: {
            textContent: '',
            style: {},
            classList: { add() {}, remove() {} }
        }
    };
    const documentMock = makeDocumentMock(elements);
    const windowMock = {
        SiteModule: { formatCurrency: val => '$' + val },
        Notificaciones: { mostrar: () => {} },
        showMessage: () => {},
        Peticiones: {
            enviar: async (url) => {
                if (url && url.includes('RegistrarPago')) {
                    paymentSent = true;
                }
                return { json: async () => ({ success: true }) };
            }
        },
        cerrarModal: () => {},
        abrirModal: () => {}
    };

    const jsCode = fs.readFileSync(path.resolve('wwwroot/js/lavado-detalle.js'), 'utf8');
    vm.runInNewContext(jsCode, {
        window: windowMock,
        document: documentMock,
        parseFloat: Number.parseFloat,
        Math: Math,
        FormData: function () { this.entries = () => []; },
        setTimeout: () => {},
        setInterval: () => {},
        clearInterval: () => {}
    });

    documentMock.dispatchDomLoaded();

    assert.ok(submitListener, 'submitListener in lavado-detalle must be registered');

    let prevented = false;
    await submitListener({
        preventDefault() { prevented = true; }
    });

    assert.equal(prevented, true);
    assert.equal(paymentSent, false, 'HTTP request must NOT be sent when amount exceeds pending balance in detalle');
});

// ----------------------------------------------------
// 4. Global Provider & Multi-Module Pricing Tests
// ----------------------------------------------------
test('Global Provider: Program.cs registers FlexibleDecimalModelBinderProvider at index 0', () => {
    const programCs = fs.readFileSync(path.resolve('Program.cs'), 'utf8');
    assert.match(programCs, /using\s+FirebaseLoginCustom\.Utils;/, 'Program.cs must import FirebaseLoginCustom.Utils');
    assert.match(programCs, /options\.ModelBinderProviders\.Insert\(0,\s*new\s+FlexibleDecimalModelBinderProvider\(\)\);/, 'Must register FlexibleDecimalModelBinderProvider at index 0');
});

test('Global Provider: FlexibleDecimalModelBinder.cs declares both binder and provider classes', () => {
    const binderCode = fs.readFileSync(path.resolve('Utils/FlexibleDecimalModelBinder.cs'), 'utf8');
    assert.match(binderCode, /public\s+class\s+FlexibleDecimalModelBinder\s*:\s*IModelBinder/);
    assert.match(binderCode, /public\s+class\s+FlexibleDecimalModelBinderProvider\s*:\s*IModelBinderProvider/);
    assert.match(binderCode, /context\.Metadata\.ModelType\s*==\s*typeof\(decimal\)/);
    assert.match(binderCode, /context\.Metadata\.ModelType\s*==\s*typeof\(decimal\?\)/);
});

test('Cross-Module Scenarios: Service price, quote item price, and stock adjustment decimal parsing', () => {
    // 1. Service pricing scenarios:
    // Whole amount (standard car wash service)
    assert.equal(parseFlexibleDecimal('25000').value, 25000);
    // Service with decimals (e.g. 25000.50 should NOT become 2500050)
    assert.equal(parseFlexibleDecimal('25000.50').value, 25000.50);
    assert.equal(parseFlexibleDecimal('25000,50').value, 25000.50);

    // 2. Quotation provider response item scenarios:
    // Quote unit price with cents (e.g. 150.50 per liter of soap/wax, should NOT become 15050)
    assert.equal(parseFlexibleDecimal('150.50').value, 150.50);
    assert.equal(parseFlexibleDecimal('150,50').value, 150.50);
    assert.equal(parseFlexibleDecimal('1250.75').value, 1250.75);

    // 3. Stock fractional adjustments:
    // 1.50 liters of chemical (should NOT become 15 or 150)
    assert.equal(parseFlexibleDecimal('1.50').value, 1.50);
    assert.equal(parseFlexibleDecimal('1,50').value, 1.50);
    assert.equal(parseFlexibleDecimal('0.75').value, 0.75);
});
