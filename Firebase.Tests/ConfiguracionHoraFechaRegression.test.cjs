const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

// Simulación de conversión con la especificación de Argentina (America/Argentina/Buenos_Aires - UTC-3)
function convertirUtcAAgentina(dateUtc) {
  const ms = dateUtc.getTime();
  // Argentina UTC-3 offset: -3 horas (-180 minutos)
  const offsetMs = -3 * 60 * 60 * 1000;
  return new Date(ms + offsetMs);
}

function formatearFechaHoraApp(dateUtc) {
  if (!dateUtc) return '';
  const d = convertirUtcAAgentina(dateUtc);
  const pad = n => String(n).padStart(2, '0');
  const day = pad(d.getUTCDate());
  const month = pad(d.getUTCMonth() + 1);
  const year = d.getUTCFullYear();
  const hours = pad(d.getUTCHours());
  const minutes = pad(d.getUTCMinutes());
  return `${day}/${month}/${year} ${hours}:${minutes}`;
}

test('Conversión de hora diurna: resta 3 horas respecto a UTC', () => {
  // Guardado a las 19:30 Argentina -> en UTC fue guardado a las 22:30
  const utc = new Date(Date.UTC(2026, 10, 19, 22, 30, 0));
  const res = formatearFechaHoraApp(utc);
  assert.equal(res, '19/11/2026 19:30');
});

test('Conversión con cruce de medianoche: cambia de fecha (día anterior en hora local)', () => {
  // Guardado a las 22:15 Argentina (19 de noviembre) -> en UTC son las 01:15 del 20 de noviembre
  // Sin la corrección, la UI mostraría erróneamente 20/11/2026 01:15
  const utc = new Date(Date.UTC(2026, 10, 20, 1, 15, 0));
  const res = formatearFechaHoraApp(utc);
  assert.equal(res, '19/11/2026 22:15');
});

test('Conversión con cambio de año/mes: 1 de enero UTC -> 31 de diciembre año anterior', () => {
  // Guardado a las 23:00 del 31/12/2025 en Argentina -> en UTC son las 02:00 del 01/01/2026
  const utc = new Date(Date.UTC(2026, 0, 1, 2, 0, 0));
  const res = formatearFechaHoraApp(utc);
  assert.equal(res, '31/12/2025 23:00');
});

test('Utils/TimeZoneHelper.cs define la zona oficial de Argentina con fallback y métodos de conversión', () => {
  const filePath = path.join(__dirname, '..', 'Utils', 'TimeZoneHelper.cs');
  assert.ok(fs.existsSync(filePath), 'Utils/TimeZoneHelper.cs debe existir');
  const content = fs.readFileSync(filePath, 'utf8');

  assert.match(content, /America\/Argentina\/Buenos_Aires/);
  assert.match(content, /Argentina Standard Time/);
  assert.match(content, /ConvertirUtcAZonaApp/);
  assert.match(content, /FormatearFechaHoraApp/);
  assert.match(content, /DateTimeKind\.Utc/);
});

test('Models/Configuracion.cs expone FechaActualizacionLocal sin persistirlo en Firestore', () => {
  const filePath = path.join(__dirname, '..', 'Models', 'Configuracion.cs');
  const content = fs.readFileSync(filePath, 'utf8');

  assert.match(content, /public\s+DateTime\?\s+FechaActualizacionLocal/);
  assert.match(content, /TimeZoneHelper\.ConvertirUtcAZonaApp/);

  // FechaActualizacion debe tener [FirestoreProperty] pero FechaActualizacionLocal NO debe tenerlo
  const metadataBlock = content.slice(content.indexOf('#region Metadata'), content.indexOf('#endregion', content.indexOf('#region Metadata')));
  assert.match(metadataBlock, /\[FirestoreProperty\]\s+public\s+DateTime\?\s+FechaActualizacion/);
  assert.doesNotMatch(metadataBlock, /\[FirestoreProperty\]\s+public\s+DateTime\?\s+FechaActualizacionLocal/);
});

test('Views/Configuracion/Index.cshtml renderiza FechaActualizacionLocal y no la hora UTC cruda', () => {
  const filePath = path.join(__dirname, '..', 'Views', 'Configuracion', 'Index.cshtml');
  const content = fs.readFileSync(filePath, 'utf8');

  assert.match(content, /FechaActualizacionLocal\?\.ToString\("dd\/MM\/yyyy HH:mm"\)/);
  assert.doesNotMatch(content, /Model\.FechaActualizacion\.Value\.ToString\("dd\/MM\/yyyy HH:mm"\)/);
});

test('Services/ConfiguracionService.cs conserva almacenamiento en UTC (regla de oro)', () => {
  const filePath = path.join(__dirname, '..', 'Services', 'ConfiguracionService.cs');
  const content = fs.readFileSync(filePath, 'utf8');

  assert.match(content, /config\.FechaActualizacion\s*=\s*DateTime\.UtcNow;/);
});

test('Preservación del trabajo ajeno de Turnos en Configuracion.cs e Index.cshtml', () => {
  const modelContent = fs.readFileSync(path.join(__dirname, '..', 'Models', 'Configuracion.cs'), 'utf8');
  assert.match(modelContent, /IntervaloInicioTurnosMinutos/);
  assert.match(modelContent, /RespuestaOfertaAdelantoMinutos/);
  assert.match(modelContent, /ToleranciaLlegadaTurnosMinutos/);

  const viewContent = fs.readFileSync(path.join(__dirname, '..', 'Views', 'Configuracion', 'Index.cshtml'), 'utf8');
  assert.match(viewContent, /asp-for="IntervaloInicioTurnosMinutos"/);
  assert.match(viewContent, /asp-for="RespuestaOfertaAdelantoMinutos"/);
  assert.match(viewContent, /asp-for="ToleranciaLlegadaTurnosMinutos"/);
});
