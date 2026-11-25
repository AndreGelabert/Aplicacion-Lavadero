'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/lavado.js'), 'utf8');

test('legacy global employee maximum and dead clamp are absent', () => {
  assert.doesNotMatch(source, /empleadosMaximosPorLavado|cargarInfoEmpleados|validarCantidadEmpleados/);
});

test('selecting a vehicle displays its full required staffing and availability', async () => {
  const elements = Object.fromEntries(['vehiculoId', 'patenteSearch', 'patenteResultados', 'vehiculoSeleccionado', 'vehiculoNombre', 'empleadosInfo', 'clientesAsociadosSection', 'serviciosSection', 'serviciosPorVehiculo', 'resumenSection'].map(id => [id, {
    value: '', textContent: '', innerHTML: '', style: {}, classList: { add() {}, remove() {} }, appendChild() {}
  }]));
  const calls = [];
  const document = {
    addEventListener() {}, getElementById: id => elements[id] || null,
    createElement: () => ({ style: {}, innerHTML: '' })
  };
  const window = {
    SiteModule: { escapeHtmlText: text => text },
    lavadoData: { vehiculosSeleccionados: [], serviciosPorVehiculo: {}, serviciosDisponibles: {}, paquetesDisponibles: {} },
    Peticiones: { enviar: async url => {
      calls.push(url);
      const data = url.includes('ObtenerClientesVehiculo') ? []
        : url.includes('ObtenerEmpleadosPorTipoVehiculo') ? { cantidadEmpleadosRequeridos: 4 }
        : url.includes('ObtenerEmpleadosDisponibles') ? { totalActivos: 6, totalDisponibles: 5, empleadosMaximosPorLavado: 2 }
        : [];
      return { json: async () => data };
    } },
    Notificaciones: { mostrar() {} }
  };
  vm.runInNewContext(source, { window, document, console, URLSearchParams });
  await window.seleccionarVehiculo('v1', 'ABC', 'Camión', 'Marca', 'Modelo', 'Azul', '');
  assert.equal(window.lavadoData.cantidadEmpleadosPorTipo, 4);
  assert.equal(window.lavadoData.empleadosInfo.totalDisponibles, 5);
  assert.equal(window.lavadoData.empleadosInfo.totalActivos, 6);
  assert.equal('empleadosMaximosPorLavado' in window.lavadoData.empleadosInfo, false);
  assert.match(elements.empleadosInfo.innerHTML, /Empleados requeridos para Camión:[\s\S]*?>4<\/span>/);
  assert.match(elements.empleadosInfo.innerHTML, /Empleados disponibles:[\s\S]*?>5<\/span> de 6 activos/);
  assert.doesNotMatch(elements.empleadosInfo.innerHTML, /Máximo/i);
  assert.ok(calls.includes('/Lavados/ObtenerEmpleadosDisponibles'));
  assert.ok(calls.includes('/Lavados/ObtenerEmpleadosPorTipoVehiculo?tipoVehiculo=Cami%C3%B3n'));
});
