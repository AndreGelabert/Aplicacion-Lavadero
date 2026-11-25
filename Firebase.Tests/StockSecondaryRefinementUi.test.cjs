const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const { readFileSync } = require('node:fs');

test('el desplegable de insumos se cierra al pulsar fuera sin perder la selección', () => {
  const handlers = {};
  const node = () => ({ children: [], dataset: {}, classList: { hidden: true, add() { this.hidden = true; }, remove() { this.hidden = false; } }, addEventListener(n, f) { this[n] = f; }, setAttribute() {}, append(...x) { this.children.push(...x); }, replaceChildren(...x) { this.children = x; } });
  const form = node(); form.elements = { namedItem() { return null; } }; form.querySelector = () => ({});
  const picker = node(); picker.closest = () => form; picker.contains = target => target.inside === true;
  const search = node(), dropdown = node(), list = node(), error = node(), add = node();
  const option = node(); option.dataset = { id: 'i1', name: 'Jabón', unit: 'L' }; option.closest = () => option;
  dropdown.querySelectorAll = selector => selector === '[data-quotation-option]' ? [option] : [];
  const ids = { 'quotation-picker': picker, 'quotation-search': search, 'quotation-dropdown': dropdown, 'quotation-selected': list, 'quotation-error': error, 'quotation-add': add };
  const document = { addEventListener(n, f) { (handlers[n] ||= []).push(f); }, getElementById(id) { return ids[id] || null; }, querySelector() { return null; }, querySelectorAll() { return []; }, createElement: node, createElementNS: node };
  vm.runInNewContext(readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), { document });
  search.value = ''; search.focus(); assert.equal(dropdown.classList.hidden, false);
  handlers.click.forEach(fn => fn({ target: { inside: false, closest() { return null; } } }));
  assert.equal(dropdown.classList.hidden, true);
  dropdown.click({ target: option }); add.click(); assert.equal(list.children.length, 1);
  search.focus(); handlers.click.forEach(fn => fn({ target: { inside: false, closest() { return null; } } }));
  assert.equal(list.children.length, 1);
});

test('la selección de proveedores rechaza agregar dos veces el mismo proveedor', () => {
  const node = () => ({ children: [], dataset: {}, classList: { hidden: true, add() { this.hidden = true; }, remove() { this.hidden = false; } }, addEventListener(n, f) { this[n] = f; }, setAttribute() {}, append(...nodes) { this.children.push(...nodes); }, querySelectorAll() { return []; } });
  const picker = node(), search = node(), dropdown = node(), selected = node(), add = node(); picker.contains = target => target.inside === true;
  const option = node(); option.dataset = { id: 'p1', name: 'Proveedor' }; option.closest = () => option;
  selected.querySelector = () => null;
  const ids = { 'supplier-picker': picker, 'supplier-search': search, 'supplier-dropdown': dropdown, 'supplier-selected': selected, 'supplier-add': add };
  const handlers = {};
  const document = { addEventListener(n, f) { (handlers[n] ||= []).push(f); }, getElementById(id) { return ids[id] || null; }, querySelector() { return null; }, querySelectorAll() { return []; }, createElement() { const el = node(); el.remove = () => { selected.children.splice(selected.children.indexOf(el), 1); }; return el; }, createElementNS: node };
  vm.runInNewContext(readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), { document });
  search.value = ''; search.focus(); assert.equal(dropdown.classList.hidden, false);
  handlers.click.forEach(fn => fn({ target: { inside: false, closest() { return null; } } })); assert.equal(dropdown.classList.hidden, true);
  dropdown.click({ target: option }); add.click(); dropdown.click({ target: option }); add.click();
  assert.equal(selected.children.length, 1); assert.equal(selected.children[0].children[1].value, 'p1');
  selected.children[0].children[2].click(); assert.equal(selected.children.length, 0);
  dropdown.click({ target: option }); add.click(); assert.equal(selected.children.length, 1);
});

test('limpiar cotización elimina proveedores, insumos y cantidades y restablece el formulario', () => {
  const node = () => ({ children: [], dataset: {}, classList: { add() {}, remove() {} }, addEventListener(n, f) { this[n] = f; }, setAttribute() {}, append(...x) { this.children.push(...x); }, replaceChildren(...x) { this.children = x; }, querySelectorAll() { return []; } });
  const picker = node(), supplier = node(), search = node(), dropdown = node(), selected = node(), add = node();
  const input = node(), choices = node(), items = node(), error = node(), itemAdd = node(), reset = node();
  const listeners = []; reset.addEventListener = (n, f) => listeners.push(f);
  const form = node(); form.elements = { namedItem() { return null; } }; let resetCount = 0; form.reset = () => { resetCount++; }; form.querySelector = () => selected.children.length ? {} : null;
  picker.closest = () => form; picker.contains = () => false; supplier.contains = () => false;
  const option = node(); option.dataset = { id: 'i1', name: 'Jabón', unit: 'L' }; option.closest = () => option;
  const provider = node(); provider.dataset = { id: 'p1', name: 'Proveedor' }; provider.closest = () => provider;
  const ids = { 'supplier-picker': supplier, 'supplier-search': search, 'supplier-dropdown': dropdown, 'supplier-selected': selected, 'supplier-add': add, 'quotation-picker': picker, 'quotation-search': input, 'quotation-dropdown': choices, 'quotation-selected': items, 'quotation-error': error, 'quotation-add': itemAdd };
  const document = { addEventListener() {}, getElementById(id) { return ids[id] || null; }, querySelector() { return reset; }, querySelectorAll() { return []; }, createElement: node, createElementNS: node };
  vm.runInNewContext(readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), { document });
  dropdown.click({ target: provider }); add.click(); choices.click({ target: option }); itemAdd.click();
  const quantity = items.children[0].children[2].children[0]; quantity.value = '4'; quantity.input();
  assert.equal(selected.children.length, 1); assert.equal(items.children.length, 1);
  listeners.forEach(fn => fn());
  assert.equal(resetCount, 1); assert.equal(selected.children.length, 0); assert.equal(items.children.length, 0); assert.equal(error.textContent, '');
  let prevented = false; form.submit({ preventDefault() { prevented = true; } }); assert.equal(prevented, true);
  dropdown.click({ target: provider }); add.click(); assert.equal(selected.children.length, 1);
});

test('la validación compartida muestra y limpia errores al recibir input y blur', () => {
  const listeners = {}, timers = [];
  const classes = (...initial) => { const set = new Set(initial); return { add(...xs) { xs.forEach(x => set.add(x)); }, remove(...xs) { xs.forEach(x => set.delete(x)); }, contains(x) { return set.has(x); } }; };
  function field(id, name, type) { return { id, name, type, tagName: 'INPUT', value: '', dataset: {}, classList: classes('border-gray-300'), labels: [{ textContent: name, querySelectorAll() { return [{ textContent: '*', classList: classes(), setAttribute() {} }]; } }], addEventListener(n, fn) { this[n] = fn; }, hasAttribute(n) { return n === 'required'; }, getAttribute() { return null; } }; }
  const phone = field('provider-phone', 'Teléfono', 'tel'), email = field('provider-email', 'Email de órdenes', 'email');
  const errors = Object.fromEntries([phone, email].map(f => [f.id + '-validation-error', { textContent: '', classList: classes('hidden') }]));
  const form = { id: 'provider-form', dataset: {}, elements: { namedItem(name) { return name === 'Contacto' ? phone : email; } }, querySelectorAll() { return [phone, email]; }, addEventListener() {} };
  const document = { addEventListener(n, fn) { (listeners[n] ||= []).push(fn); }, getElementById(id) { return id === 'provider-form' ? form : errors[id] || null; }, querySelectorAll(s) { return s === 'form[data-validate="true"]' ? [form] : []; } };
  const window = {}; const context = { document, window, console: { log() {}, warn() {} }, setTimeout(fn) { timers.push(fn); return timers.length; }, clearTimeout() {} };
  vm.runInNewContext(readFileSync('wwwroot/js/form-validation.js', 'utf8'), context);
  vm.runInNewContext(readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), context);
  listeners.DOMContentLoaded.forEach(fn => fn());
  phone.value = '123'; phone.blur(); assert.equal(errors['provider-phone-validation-error'].textContent, 'Exactamente 10 dígitos'); assert.equal(phone.classList.contains('border-red-500'), true);
  phone.input(); timers.shift()(); assert.equal(errors['provider-phone-validation-error'].textContent, 'Exactamente 10 dígitos'); assert.equal(phone.classList.contains('border-red-500'), true);
  phone.value = '1234567890'; phone.input(); timers.shift()(); assert.equal(errors['provider-phone-validation-error'].textContent, ''); assert.equal(phone.classList.contains('border-red-500'), false);
  email.value = 'a'; email.input(); timers.shift()(); assert.equal(errors['provider-email-validation-error'].textContent, 'Ingrese un correo electrónico válido'); assert.equal(email.classList.contains('border-red-500'), true);
  email.value = 'a@example.com'; email.blur(); assert.equal(errors['provider-email-validation-error'].textContent, ''); assert.equal(email.classList.contains('border-red-500'), false);
});

test('la vista agrupa por concurso y muestra detalle por proveedor en la comparativa', () => {
  const source = readFileSync('Views/Insumos/Cotizaciones.cshtml', 'utf8');
  assert.match(source, /GroupBy\(s => s\.GrupoId\)/);
  assert.match(source, /asp-action="AprobarGrupo"/);
  assert.match(source, /name="grupoId" value="@grupo\.Key"/);
  assert.match(source, /name="id" value="@item\.Solicitud\.Id"/);
});

test('el formulario de proveedor impide guardar datos inválidos mediante la validación compartida', () => {
  const handlers = {};
  const email = { value: '' };
  const form = { elements: { namedItem() { return { value: '' }; } }, addEventListener(n, f) { handlers[n] = f; } };
  const callbacks = {};
  const document = { addEventListener(n, f) { callbacks[n] = f; }, getElementById(id) { return id === 'provider-form' ? form : id === 'provider-email' ? email : null; }, querySelectorAll() { return []; } };
  let calls = 0;
  const window = { FormValidation: { registerCustomValidator() {}, validateForm() { calls++; return false; } } };
  vm.runInNewContext(readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), { document, window });
  callbacks.DOMContentLoaded();
  let prevented = false;
  handlers.submit({ preventDefault() { prevented = true; } });
  assert.equal(calls, 1); assert.equal(prevented, true);
});

test('el correo bloqueado no impide validar cambios en otros campos', () => {
  const callbacks = {}, handlers = {};
  const email = { disabled: true, required: true };
  const form = { elements: { namedItem(n) { return n === 'EmailOrdenes' ? email : { value: '1234567890' }; } }, addEventListener(n, f) { handlers[n] = f; } };
  const document = { addEventListener(n, f) { callbacks[n] = f; }, getElementById(id) { return id === 'provider-form' ? form : id === 'provider-email' ? email : null; }, querySelectorAll() { return []; } };
  const window = { FormValidation: { registerCustomValidator() {}, validateForm() { return !email.required; } } };
  vm.runInNewContext(readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), { document, window });
  callbacks.DOMContentLoaded(); let prevented = false; handlers.submit({ preventDefault() { prevented = true; } });
  assert.equal(prevented, false); assert.equal(email.required, true);
});

test('editar proveedor bloqueado conserva email enviado y desbloquea al limpiar', () => {
  const handlers = {};
  const field = value => ({ value, classList: { add() {}, remove() {} }, focus() {}, addEventListener() {} });
  const fields = Object.fromEntries(['Id','IdentificacionTributaria','RazonSocial','Contacto','EmailOrdenes','crear'].map(x => [x, field('')]));
  const hidden = field(''); hidden.disabled = true; hidden.name = '';
  fields.EmailOrdenes.required = true;
  const form = { elements: { namedItem: n => n === 'EmailOrdenes' && hidden.name === n ? [fields[n], hidden] : fields[n] }, dataset: { canCreate: 'true' }, reset() { Object.values(fields).forEach(f => { f.value = ''; }); }, scrollIntoView() {}, addEventListener(n, f) { this[n] = f; } };
  const note = { classList: { hidden: true, toggle(_, v) { this.hidden = v; } } };
  const title = {}; const button = { getAttribute() { return 'true'; }, click() {} };
  const doc = { addEventListener(n, f) { handlers[n] = f; }, getElementById(id) { return ({'provider-form':form,'provider-email':fields.EmailOrdenes,'provider-original-email':hidden,'provider-email-lock-note':note,'provider-title':title})[id] || null; }, querySelector() { return button; }, querySelectorAll() { return []; } };
  const window = { FormValidation: { registerCustomValidator() {}, clearErrors() {}, validateForm() { return !fields.EmailOrdenes.required && fields.Contacto.value === '1234567890'; } } };
  vm.runInNewContext(readFileSync('wwwroot/js/insumos-secondary.js', 'utf8'), { document: doc, window });
  handlers.DOMContentLoaded();
  const edit = { dataset: { id:'1', contact:'1234567890', email:'a@example.com', emailOrdenesBloqueado:'true' } };
  handlers.click({ target: { closest(selector) { return selector === '[data-provider-edit]' ? edit : null; } } });
  assert.equal(fields.EmailOrdenes.disabled, true); assert.equal(hidden.disabled, false); assert.equal(hidden.name, 'EmailOrdenes'); assert.equal(hidden.value, 'a@example.com'); assert.equal(note.classList.hidden, false);
  let prevented = false; form.submit({ preventDefault() { prevented = true; } }); assert.equal(prevented, false); assert.equal(fields.EmailOrdenes.required, true);
  handlers.click({ target: { closest(selector) { return selector === '[data-provider-reset]' ? {} : null; } } });
  assert.equal(fields.EmailOrdenes.disabled, false); assert.equal(hidden.disabled, true); assert.equal(hidden.name, ''); assert.equal(hidden.value, ''); assert.equal(note.classList.hidden, true);
});
