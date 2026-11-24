const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const root = path.join(__dirname, '..');
const script = fs.readFileSync(path.join(root, 'wwwroot/js/lavado-detalle.js'), 'utf8');

function detail(response) {
    const requests = [], notifications = [], timers = [];
    const window = {
        Peticiones: { enviar: async (url, options) => {
            requests.push({ url, options });
            return url.startsWith('/Lavados/RecursosInicio?')
                ? { ok: true, json: async () => ({ success: true, items: [] }) }
                : { ok: true, json: async () => response };
        } },
        LavadoInsumos: { seleccionar: async () => ({}) },
        Notificaciones: { mostrar: (message, type) => notifications.push({ message, type }) },
        location: { reload() {} }
    };
    const document = { addEventListener() {}, querySelector: () => ({ value: 'token' }) };
    vm.runInNewContext(script, { window, document, FormData, URLSearchParams, console,
        setTimeout: (fn, ms) => timers.push({ fn, ms }) });
    return { window, requests, notifications, timers };
}

test('detalle expone asignaciones verificadas y conversión usa el nombre real del empleado', () => {
    const consulta = fs.readFileSync(path.join(root, 'Services/TurnoConsultaService.cs'), 'utf8');
    const lavado = fs.readFileSync(path.join(root, 'Services/LavadoService.cs'), 'utf8');
    const view = fs.readFileSync(path.join(root, 'Views/Turnos/Detalle.cshtml'), 'utf8');
    assert.match(consulta, /EmpleadosAsignadosIds/);
    assert.match(consulta, /TurnoId/);
    assert.match(consulta, /LavadoId/);
    assert.match(lavado, /ed\.TryGetValue<string>\("Nombre"/);
    assert.match(view, /Model\.EmpleadosAsignados/);
});

test('etapa rechazada muestra el mensaje devuelto sin recargar y envía identificación y token', async () => {
    const message = "No se puede iniciar esta etapa porque la etapa 'Enjuage inicial' está en proceso. Las etapas deben completarse en orden.";
    const app = detail({ success: false, message });
    await app.window.iniciarEtapaDesdeDetalle('lavado', 'servicio', 'etapa');
    assert.equal(app.requests.length, 2);
    const request = app.requests[1];
    assert.equal(request.url, '/Lavados/IniciarEtapa');
    assert.equal(request.options.method, 'POST');
    assert.equal(request.options.headers.RequestVerificationToken, 'token');
    assert.deepEqual([...request.options.body.entries()], [
        ['lavadoId', 'lavado'], ['servicioId', 'servicio'], ['etapaId', 'etapa'], ['seleccionJson', '{}']
    ]);
    assert.deepEqual(app.notifications, [{ message, type: 'error' }]);
    assert.equal(app.timers.length, 0);
});

test('inicio exitoso conserva notificación y recarga programada', async () => {
    const app = detail({ success: true, message: 'Etapa iniciada correctamente.' });
    await app.window.iniciarEtapaDesdeDetalle('l', 's', 'e');
    assert.deepEqual(app.notifications, [{ message: 'Etapa iniciada correctamente.', type: 'success' }]);
    assert.equal(app.timers.length, 1);
    assert.equal(app.timers[0].ms, 1000);
});

test('la vista condiciona el botón de inicio a la siguiente etapa pendiente sin otra en proceso', () => {
    // Esta comprobación de plantilla no sustituye una prueba del HTML Razor renderizado.
    const view = fs.readFileSync(path.join(root, 'Views/Lavados/_LavadoDetail.cshtml'), 'utf8');
    assert.match(view, /var hayEtapaEnProceso\s*=\s*servicio\.Etapas\.Any\(e\s*=>\s*e\.Estado\s*==\s*"EnProceso"\)/);
    assert.match(view, /var siguienteEtapaPendienteId\s*=\s*servicio\.Etapas\.FirstOrDefault\(e\s*=>\s*e\.Estado\s*==\s*"Pendiente"\)\?\.EtapaId/);
    assert.match(view, /@if\s*\(etapa\.Estado\s*==\s*"Pendiente"\s*&&\s*!hayEtapaEnProceso\s*&&\s*etapa\.EtapaId\s*==\s*siguienteEtapaPendienteId\)\s*\{\s*<button[^>]*onclick="iniciarEtapaDesdeDetalle/s);
});
