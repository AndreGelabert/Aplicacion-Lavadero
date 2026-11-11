'use strict';

const { test } = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { probe } = require('../scripts/verify-shared-presentation-differential.cjs');

let pending;
function getCases() { return pending ||= probe(path.resolve(__dirname, '..'), true); }

test('public payment and table consumers delegate and retain traces', async () => {
  const cases = (await getCases()).filter(item => item.kind === 'standard');
  const expected = ['lavado:true', 'lavado-detalle:true', ...['personal', 'roles', 'auditoria', 'cliente', 'lavado', 'servicio', 'paqueteservicio', 'vehiculo'].flatMap(name => [`${name}:true`, `${name}:false`])];
  assert.deepEqual(cases.map(item => `${item.module}:${item.site}`).sort(), expected.sort());
  for (const item of cases) {
    assert.deepEqual(item.failures, [], `${item.module}:${item.site} ${JSON.stringify(item)}`);
    assert.deepEqual(item.errors, []);
    assert.deepEqual(item.network, []);
  }
  for (const item of cases.filter(item => Array.isArray(item.data))) {
    assert.equal(item.data.length, 6);
    assert.equal(item.data[0].id, 'wash');
    assert.equal(item.data[0].amount, '0.00');
    assert.equal(item.data[0].hidden, false);
  }
  for (const item of cases.filter(item => item.module === 'personal')) {
    assert.match(item.data.first.trace[0].url, /roles=one&roles=two&estados=Activo&pageNumber=2&sortBy=NombreCompleto&sortOrder=asc/);
    assert.match(item.data.missing.trace[0].url, /pageNumber=3&sortBy=NombreCompleto&sortOrder=asc/);
    assert.equal(item.data.first.page, '3');
    assert.equal(item.data.missing.page, '3');
  }
  for (const item of cases.filter(item => ['auditoria', 'cliente', 'lavado', 'servicio', 'paqueteservicio'].includes(item.module) && !Array.isArray(item.data))) {
    for (const phase of ['first', 'same', 'next']) {
      const url = item.data[phase].request.url;
      assert.match(url, /roles=one&roles=two&estados=Activo/);
      assert.equal(item.data[phase].page, '3');
      assert.match(url, /sortBy=/);
      assert.match(url, /sortOrder=/);
    }
    assert.match(item.data.next.request.url, /sortBy=Estado&sortOrder=asc/);
  }
  for (const item of cases.filter(item => item.module === 'vehiculo')) {
    assert.deepEqual(item.data, {
      requests: [{ url: '/Vehiculo/SearchPartial?roles=one&roles=two&estados=Activo&searchTerm=placa&pageNumber=1&sortBy=Patente&sortOrder=asc', options: null }],
      errors: [], table: '<input id="current-page-value" value="3">', page: '3',
      entries: [['roles', 'one'], ['roles', 'two'], ['estados', 'Activo']]
    });
  }
  for (const item of cases.filter(item => item.module === 'roles')) {
    assert.match(item.data.first[0].url, /sortBy=Nombre&sortOrder=asc&pageNumber=2/);
    assert.equal(item.data.same.order, 'desc');
    assert.match(item.data.same.trace[0].url, /sortBy=Nombre&sortOrder=desc/);
    assert.equal(item.data.next.order, 'asc');
    assert.match(item.data.next.trace[0].url, /sortBy=Estado&sortOrder=asc/);
  }
});

test('public DOM text consumers escape markup and preserve falsy inputs', async () => {
  const cases = await probe(path.resolve(__dirname, '..'), true, true);
  assert.equal(cases.length, 6);
  for (const item of cases) {
    assert.deepEqual(item.failures, [], `${item.module}:${item.site} ${JSON.stringify(item)}`);
    assert.deepEqual(item.errors, []);
    assert.deepEqual(item.network, []);
    if (item.module === 'lavado') {
      assert.deepEqual(item.data.initialized, { vehicleId: '', selectedHidden: true });
      assert.equal(item.data.requests.length, 20);
    }
    assert.equal(item.data.snapshots.length, 4);
    for (const snapshot of item.data.snapshots) assert.equal(snapshot.injected, 0);
    assert.match(item.data.snapshots[0].html, /&lt;b&gt;&amp;/);
    assert.match(item.data.snapshots[0].text, /<b>& "quoted"<\/b>/);
    for (const snapshot of item.data.snapshots.slice(1)) assert.doesNotMatch(snapshot.text, /null|undefined/);
  }
});
