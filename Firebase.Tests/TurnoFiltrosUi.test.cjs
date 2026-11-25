const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

function setup(query = '') {
  const handlers = () => ({ events: {}, addEventListener(name, fn) { this.events[name] = fn; } });
  const search = { ...handlers(), value: '' };
  const checks = ['Pendiente', 'Completado', 'Cancelado', 'Ausente'].map(value => ({ ...handlers(), value, checked: true }));
  const rows = [
    { textContent: 'Ana ABC123', estado: 'Pendiente', style: {} },
    { textContent: 'Beto DEF456', estado: 'Completado', style: {} }
  ];
  rows.forEach(row => row.getAttribute = () => row.estado);
  const button = handlers();
  const filter = { querySelectorAll: () => checks };
  const sort = { ...handlers(), href: 'https://example.test/Turnos?sortBy=Cliente&fechaDesde=2026-01-01' };
  const elements = { 'simple-search': search, turnoEstadoFiltros: filter, limpiarFiltrosTurno: button };
  const document = { getElementById: id => elements[id], querySelectorAll: selector => selector === '#turno-table-body tr[data-estado]' ? rows : [sort] };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/turno-filtros.js', 'utf8'), { document, URL, URLSearchParams, window: { location: { search: query } } });
  return { search, checks, rows, button, sort };
}

test('consult-only list filters combine text and status, including none and reset', () => {
  const index = fs.readFileSync('Views/Turnos/Index.cshtml', 'utf8');
  assert.match(index, /src="~\/js\/turno-filtros\.js"/);
  const { search, checks, rows, button } = setup();
  search.value = 'aNa'; search.events.input();
  assert.deepEqual(rows.map(r => r.style.display), ['', 'none']);
  checks.forEach(c => c.checked = false); checks[0].events.change();
  assert.deepEqual(rows.map(r => r.style.display), ['none', 'none']);
  button.events.click();
  assert.deepEqual(rows.map(r => r.style.display), ['', 'none']);
  search.value = ''; search.events.input();
  assert.deepEqual(rows.map(r => r.style.display), ['', '']);
});

test('sorting retains search and selected statuses alongside existing range parameters', () => {
  const first = setup();
  first.search.value = 'Ana'; first.checks[1].checked = false;
  first.sort.events.click();
  const url = new URL(first.sort.href);
  assert.equal(url.searchParams.get('fechaDesde'), '2026-01-01');
  assert.equal(url.searchParams.get('turnoBusqueda'), 'Ana');
  const next = setup(url.search);
  assert.equal(next.search.value, 'Ana');
  assert.equal(next.checks[1].checked, false);
  assert.deepEqual(next.rows.map(r => r.style.display), ['', 'none']);
});

test('create form remains loaded independently and no duplicate list handlers remain', () => {
  assert.match(fs.readFileSync('Views/Turnos/_TurnoForm.cshtml', 'utf8'), /src="~\/js\/turnos\.js"/);
  assert.doesNotMatch(fs.readFileSync('wwwroot/js/turnos.js', 'utf8'), /filtrarTablaTurnos/);
});

test('clearTurnoFilters oculta dropdown y limpia estilos de posicionamiento', () => {
  const dropdown = {
    classList: { classes: new Set(), add(c) { this.classes.add(c); } },
    style: { transform: 'translate3d(100px, 100px, 0px)', removeProperty(p) { delete this[p]; } },
    removeAttribute(a) { delete this[a]; }
  };
  const windowObj = { location: { search: '' }, clearTurnoFilters: null };
  const document = {
    getElementById: id => id === 'filterDropdown' ? dropdown : null,
    querySelectorAll: () => []
  };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/turno-filtros.js', 'utf8'), {
    document, URL, URLSearchParams, window: windowObj, fetch: () => Promise.resolve({ ok: true, text: () => '' })
  });
  assert.equal(typeof windowObj.clearTurnoFilters, 'function');
  windowObj.clearTurnoFilters();
  assert.ok(dropdown.classList.classes.has('hidden'));
  assert.equal(dropdown.style.transform, undefined);
});
