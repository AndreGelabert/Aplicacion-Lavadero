const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

class Element {
  constructor(tag) { this.tagName = tag; this.children = []; this.attributes = {}; this.listeners = {}; this.dataset = {}; }
  append(...children) { this.children.push(...children); }
  after(node) { this.next = node; }
  setAttribute(key, value) { this.attributes[key] = value; }
  removeAttribute(key) { delete this.attributes[key]; }
  getAttribute(key) { return this.attributes[key]; }
  addEventListener(key, fn) { this.listeners[key] = fn; }
  click() { if (!this.disabled) this.listeners.click?.(); }
  replaceChildren(...children) { this.children = children; }
}
function setup(count) {
  const rows = Array.from({length: count}, (_, i) => ({ hidden: false, cells: [{dataset: {}, textContent: String(count - i)}] }));
  const header = new Element('th');
  const sort = new Element('button'); sort.dataset.sortColumn = '0'; sort.dataset.sortType = 'number'; sort.closest = () => header;
  const parent = new Element('div');
  const table = { tBodies: [{rows, appendChild() {}}], parentElement: parent, querySelectorAll: selector => selector === '[data-sort-column]' ? [sort] : [header] };
  const document = { getElementById: () => null, addEventListener() {}, querySelector: () => null, querySelectorAll: selector => selector === '[data-finite-table]' ? [table] : [], createElement: tag => new Element(tag), createElementNS: (_, tag) => new Element(tag) };
  vm.runInNewContext(fs.readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), {document});
  return { rows, sort, nav: parent.next };
}
test('finite tables provide styled numeric navigation and retain sorting and counts', () => {
  const {rows, sort, nav} = setup(21);
  const [label, controls] = nav.children;
  assert.equal(label.textContent, 'Mostrando página 1 de 3');
  assert.equal(controls.children.length, 5);
  const [prev, one, two, three, next] = controls.children;
  assert.equal(prev.disabled, true); assert.equal(one.attributes['aria-current'], 'page');
  assert.match(one.className, /text-primary-600/); assert.match(prev.className, /rounded-l-lg/);
  two.click(); assert.equal(label.textContent, 'Mostrando página 2 de 3'); assert.equal(rows.filter(r => !r.hidden).length, 10);
  assert.equal(two.attributes['aria-current'], 'page'); next.click(); assert.equal(next.disabled, true); assert.equal(rows.filter(r => !r.hidden).length, 1);
  prev.click(); assert.equal(label.textContent, 'Mostrando página 2 de 3'); sort.click(); assert.equal(label.textContent, 'Mostrando página 1 de 3');
  assert.equal(rows.filter(r => !r.hidden).length, 10);
});
test('server pager retains query parameters for every link and matches standard controls', () => {
  const view = fs.readFileSync('Views/Insumos/Index.cshtml', 'utf8');
  const pager = view.slice(view.indexOf('@if ((int)ViewBag.TotalPages > 1)'));
  assert.doesNotMatch(pager, /href="\?page=/);
  assert.match(pager, /QueryString\.Create/);
  assert.match(pager, /text-primary-600/);
  assert.match(pager, /w-5 h-5/);
});
