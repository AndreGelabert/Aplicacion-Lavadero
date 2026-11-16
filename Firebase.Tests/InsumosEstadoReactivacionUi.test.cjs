'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

test('el modal alterna reactivación verde y desactivación roja', () => {
  const buttons = ['Activo', 'Inactivo'].map((estado, i) => ({dataset: {estado, id: String(i), nombre: i ? 'Jabón' : 'Cera'}, addEventListener(_, fn) { this.click = fn; }, focus() {}}));
  const id = {}, estado = {}, title = {}, message = {replaceChildren(...nodes) { this.children = nodes; this.textContent = nodes.map(x => typeof x === 'string' ? x : x.textContent).join(''); }}, submit = {className: ''}, icon = {innerHTML: '', className: {baseVal: ''}}, wrapper = {className: ''};
  const modal = {classList: {remove() {}, add() {}}, querySelector(s) { return ({'[name="id"]': id, '[name="estado"]': estado})[s] || null; }};
  const nodes = {insumoConfirmModal: modal, insumoConfirmTitle: title, insumoConfirmMessage: message, insumoConfirmSubmit: submit, insumoConfirmIcon: icon, insumoConfirmIconWrapper: wrapper};
  const document = {createElement: tag => ({tagName: tag, textContent: ''}), getElementById: id => nodes[id] || null, querySelector: () => null, querySelectorAll: s => s === '[data-estado-insumo]' ? buttons : [], addEventListener() {}};
  vm.runInNewContext(fs.readFileSync('wwwroot/js/insumos.js', 'utf8'), {document, window: {}});
  buttons[0].click();
  assert.equal(id.value, '0'); assert.equal(estado.value, 'Activo'); assert.equal(title.textContent, 'Reactivar Insumo'); assert.match(message.textContent, /Cera/); assert.equal(message.children[1].tagName, 'strong'); assert.equal(message.children[1].textContent, 'Cera'); assert.equal(submit.textContent, 'Reactivar');
  assert.match(submit.className, /(?:green|success)/); assert.match(wrapper.className, /green/); assert.match(icon.innerHTML, /M2\.25 12/);
  buttons[1].click();
  assert.equal(id.value, '1'); assert.equal(estado.value, 'Inactivo'); assert.equal(title.textContent, 'Desactivar Insumo'); assert.match(message.textContent, /Jabón/); assert.equal(message.children[1].tagName, 'strong'); assert.equal(message.children[1].textContent, 'Jabón'); assert.equal(submit.textContent, 'Desactivar');
  assert.match(submit.className, /(?:red|danger)/); assert.match(wrapper.className, /red/); assert.doesNotMatch(icon.innerHTML, /M2\.25 12/);
});

test('limpiar filtros restaura activo y limpia búsqueda, tipos y mínimo', () => {
  const inputs = [{name: 'estados', value: 'Activo', checked: false}, {name: 'estados', value: 'Inactivo', checked: true}, {name: 'tipos', checked: true}, {name: 'soloBajoMinimo', checked: true}];
  const search = {value: 'jabón'}, clear = {addEventListener(_, fn) { this.click = fn; }};
  const document = {getElementById: id => id === 'insumos-search' ? search : null, querySelector: s => s === '[data-clear-insumos-filters]' ? clear : null, querySelectorAll: s => s === '#insumosFiltros input[type="checkbox"]' ? inputs : [], addEventListener() {}};
  vm.runInNewContext(fs.readFileSync('wwwroot/js/insumos.js', 'utf8'), {document, window: {}});
  clear.click(); assert.deepEqual(inputs.map(i => i.checked), [true, false, false, false]); assert.equal(search.value, '');
});
