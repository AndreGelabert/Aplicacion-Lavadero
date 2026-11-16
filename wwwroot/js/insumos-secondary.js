(() => {
    'use strict';
    let returnFocus = null;
    function openModal(modal, trigger) {
        if (!modal) return;
        returnFocus = trigger;
        modal.classList.remove('hidden');
        modal.setAttribute('aria-hidden', 'false');
        modal.querySelector('button, input')?.focus();
    }
    function closeModal(modal) {
        if (!modal) return;
        modal.classList.add('hidden');
        modal.setAttribute('aria-hidden', 'true');
        returnFocus?.focus();
    }
    function lockProviderEmail(form, locked, value) {
        const email = document.getElementById('provider-email');
        const original = document.getElementById('provider-original-email');
        email.disabled = locked; original.disabled = !locked; original.name = locked ? 'EmailOrdenes' : ''; original.value = locked ? value : '';
        document.getElementById('provider-email-lock-note').classList.toggle('hidden', !locked);
    }
    const providerForm = document.getElementById('provider-form');
    if (providerForm && typeof window !== 'undefined') {
        document.addEventListener('DOMContentLoaded', () => {
            window.FormValidation?.registerCustomValidator('provider-phone', () => {
                const value = providerForm.elements.namedItem('Contacto').value.trim();
                return { isValid: /^[0-9]{10}$/.test(value), message: 'Exactamente 10 dígitos' };
            });
            providerForm.addEventListener('submit', event => {
                if (!window.FormValidation) return;
                const email = document.getElementById('provider-email');
                const wasRequired = email.required;
                if (email.disabled) email.required = false;
                const valid = window.FormValidation.validateForm(providerForm);
                email.required = wasRequired;
                if (!valid) event.preventDefault();
            });
        });
    }
    document.addEventListener('click', event => {
        const open = event.target.closest('[data-open-modal]');
        if (open) openModal(document.getElementById(open.dataset.openModal), open);
        const close = event.target.closest('[data-close-modal]');
        if (close) closeModal(close.closest('[role="dialog"]'));
        const edit = event.target.closest('[data-provider-edit]');
        if (edit) {
            const form = document.getElementById('provider-form');
            for (const [name, value] of Object.entries({ Id: edit.dataset.id, IdentificacionTributaria: edit.dataset.tax, RazonSocial: edit.dataset.name, Contacto: edit.dataset.contact, crear: 'false' })) form.elements.namedItem(name).value = value || '';
            document.getElementById('provider-email').value = edit.dataset.email || '';
            form.elements.namedItem('IdentificacionTributaria').readOnly = true;
            lockProviderEmail(form, edit.dataset.emailOrdenesBloqueado === 'true', edit.dataset.email || '');
            if (typeof window !== 'undefined') window.FormValidation?.clearErrors(form);
            document.getElementById('provider-title').textContent = 'Editando un Proveedor';
            const resetLabel = document.getElementById('provider-reset-label'); if (resetLabel) resetLabel.textContent = 'Cancelar';
            const button = document.querySelector('[data-accordion-target="#provider-body"]');
            if (button.getAttribute('aria-expanded') !== 'true') button.click();
            form.scrollIntoView({ block: 'start' });
            form.elements.namedItem('RazonSocial').focus();
        }
        const reset = event.target.closest('[data-provider-reset]');
        if (reset) {
            const form = document.getElementById('provider-form');
            form.reset(); form.elements.namedItem('Id').value = ''; form.elements.namedItem('crear').value = 'true';
            form.elements.namedItem('IdentificacionTributaria').readOnly = false;
            lockProviderEmail(form, false, '');
            if (typeof window !== 'undefined') window.FormValidation?.clearErrors(form);
            document.getElementById('provider-title').textContent = form.dataset.canCreate === 'true' ? 'Registrando un Proveedor' : 'Editar Proveedor';
            const resetLabel = document.getElementById('provider-reset-label'); if (resetLabel) resetLabel.textContent = 'Limpiar campos';
            if (form.dataset.canCreate !== 'true') document.querySelector('[data-accordion-target="#provider-body"]').click();
        }
        const state = event.target.closest('[data-provider-state]');
        if (state) {
            const modal = document.getElementById('provider-confirm');
            const action = state.dataset.state === 'Activo' ? 'Reactivar' : 'Desactivar';
            modal.querySelector('[name="id"]').value = state.dataset.id;
            modal.querySelector('[name="estado"]').value = state.dataset.state;
            document.getElementById('provider-confirm-title').textContent = `${action} Proveedor`;
            document.getElementById('provider-confirm-message').textContent = `¿Confirma ${action.toLowerCase()} el proveedor ${state.dataset.name}?`;
            document.getElementById('provider-confirm-submit').querySelector('span').textContent = action;
            openModal(modal, state);
        }
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') { const modal = document.querySelector('[role="dialog"][aria-hidden="false"]'); if (modal) closeModal(modal); }
    });
    const search = document.getElementById('kardex-search');
    search?.addEventListener('input', () => document.querySelectorAll('[data-kardex-row]').forEach(row => { row.hidden = !row.textContent.toLocaleLowerCase().includes(search.value.trim().toLocaleLowerCase()); }));
    document.querySelectorAll('[data-finite-table]').forEach(table => {
        const rows = Array.from(table.tBodies[0]?.rows || []);
        const pageSize = 10;
        let page = 1;
        const pages = Math.max(1, Math.ceil(rows.length / pageSize));
        const nav = document.createElement('nav');
        nav.className = 'flex flex-col md:flex-row justify-between items-center gap-3 p-4';
        nav.setAttribute('aria-label', 'Paginación de tabla');
        const label = document.createElement('span'); label.className = 'text-sm text-gray-500 dark:text-gray-400';
        const controls = document.createElement('div'); controls.className = 'inline-flex items-stretch -space-x-px';
        const arrowClass = 'flex items-center justify-center h-full py-1.5 px-3 bg-white border border-gray-300 dark:bg-gray-800 dark:border-gray-700';
        const numberClass = 'flex items-center justify-center text-sm py-2 px-3 leading-tight text-gray-500 bg-white border border-gray-300 hover:bg-gray-100 hover:text-gray-700 dark:bg-gray-800 dark:border-gray-700 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-white';
        const activeClass = 'flex items-center justify-center text-sm z-10 py-2 px-3 leading-tight text-primary-600 bg-primary-50 border border-primary-300 dark:border-gray-700 dark:bg-gray-700 dark:text-white';
        function arrow(label, path, rounded) {
            const button = document.createElement('button'); button.type = 'button'; button.setAttribute('aria-label', label);
            button.className = `${arrowClass} ${rounded}`;
            button.innerHTML = `<svg class="w-5 h-5" aria-hidden="true" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="${path}" clip-rule="evenodd" /></svg>`;
            return button;
        }
        const previous = arrow('Anterior', 'M12.707 5.293a1 1 0 010 1.414L9.414 10l3.293 3.293a1 1 0 01-1.414 1.414l-4-4a1 1 0 010-1.414l4-4a1 1 0 011.414 0z', 'rounded-l-lg');
        const next = arrow('Siguiente', 'M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z', 'rounded-r-lg');
        const numbers = Array.from({ length: pages }, (_, index) => {
            const button = document.createElement('button'); button.type = 'button'; button.textContent = index + 1;
            button.addEventListener('click', () => { page = index + 1; renderPage(); }); return button;
        });
        controls.append(previous, ...numbers, next); nav.append(label, controls); table.parentElement.after(nav);
        function renderPage() {
            rows.forEach((row, index) => { row.hidden = index < (page - 1) * pageSize || index >= page * pageSize; });
            label.textContent = `Mostrando página ${page} de ${pages}`;
            numbers.forEach((number, index) => {
                const active = index + 1 === page; number.className = active ? activeClass : numberClass;
                if (active) number.setAttribute('aria-current', 'page'); else number.removeAttribute('aria-current');
            });
            previous.disabled = page === 1; next.disabled = page === pages;
            for (const button of [previous, next]) button.className = `${arrowClass} ${button === previous ? 'rounded-l-lg' : 'rounded-r-lg'} ${button.disabled ? 'text-gray-300 cursor-not-allowed' : 'text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-white'}`;
        }
        table.querySelectorAll('[data-sort-column]').forEach(button => {
            button.addEventListener('click', () => {
                const column = Number(button.dataset.sortColumn);
                const header = button.closest('th');
                const ascending = header.getAttribute('aria-sort') !== 'ascending';
                table.querySelectorAll('th[aria-sort]').forEach(th => th.removeAttribute('aria-sort'));
                header.setAttribute('aria-sort', ascending ? 'ascending' : 'descending');
                rows.sort((a, b) => {
                    const left = a.cells[column].dataset.sortValue ?? a.cells[column].textContent.trim();
                    const right = b.cells[column].dataset.sortValue ?? b.cells[column].textContent.trim();
                    const type = button.dataset.sortType;
                    const comparison = type === 'number' ? Number(left) - Number(right) : type === 'date' ? Date.parse(left) - Date.parse(right) : left.localeCompare(right, 'es', { numeric: true, sensitivity: 'base' });
                    return (ascending ? 1 : -1) * (Number.isFinite(comparison) ? comparison : 0);
                });
                rows.forEach(row => table.tBodies[0].appendChild(row));
                page = 1; renderPage();
            });
        });
        previous.addEventListener('click', () => { if (page > 1) { page--; renderPage(); } });
        next.addEventListener('click', () => { if (page < pages) { page++; renderPage(); } });
        renderPage();
    });
    function removeIcon(button) {
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('fill', 'currentColor'); svg.setAttribute('class', 'w-5 h-5'); svg.setAttribute('aria-hidden', 'true');
        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        path.setAttribute('fill-rule', 'evenodd'); path.setAttribute('clip-rule', 'evenodd');
        path.setAttribute('d', 'M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25Zm-1.72 6.97a.75.75 0 1 0-1.06 1.06L10.94 12l-1.72 1.72a.75.75 0 1 0 1.06 1.06L12 13.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L13.06 12l1.72-1.72a.75.75 0 1 0-1.06-1.06L12 10.94l-1.72-1.72Z');
        svg.append(path); button.append(svg);
    }
    const supplierPicker = document.getElementById('supplier-picker');
    if (supplierPicker) {
        const search = document.getElementById('supplier-search');
        const dropdown = document.getElementById('supplier-dropdown');
        const selected = document.getElementById('supplier-selected');
        let choice = null;
        const selectedIds = new Set();
        function filterSuppliers() {
            choice = null;
            dropdown.querySelectorAll('[data-supplier-option]').forEach(option => {
                option.hidden = !option.dataset.name.toLocaleLowerCase().includes(search.value.toLocaleLowerCase()) || selectedIds.has(option.dataset.id);
            });
            dropdown.classList.remove('hidden');
        }
        search.addEventListener('focus', filterSuppliers); search.addEventListener('input', filterSuppliers);
        dropdown.addEventListener('click', event => {
            const option = event.target.closest('[data-supplier-option]'); if (!option) return;
            choice = { ...option.dataset }; search.value = choice.name; dropdown.classList.add('hidden');
        });
        document.getElementById('supplier-add').addEventListener('click', () => {
            if (!choice || selectedIds.has(choice.id)) return;
            selectedIds.add(choice.id);
            const row = document.createElement('div'); row.className = 'flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600';
            const name = document.createElement('span'); name.className = 'flex-1 font-medium'; name.textContent = choice.name;
            const id = document.createElement('input'); id.type = 'hidden'; id.name = 'proveedorIds'; id.value = choice.id;
            const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'w-8 h-8 rounded-full text-red-600'; removeIcon(remove); remove.setAttribute('aria-label', `Quitar ${choice.name}`); const selectedId = choice.id; remove.addEventListener('click', () => { selectedIds.delete(selectedId); row.remove(); });
            row.append(name, id, remove); selected.append(row); choice = null; search.value = ''; dropdown.classList.add('hidden');
        });
        document.addEventListener('click', event => { if (!supplierPicker.contains(event.target)) dropdown.classList.add('hidden'); });
        document.querySelector('[data-quotation-reset]')?.addEventListener('click', () => {
            selectedIds.clear(); selected.replaceChildren(); choice = null; search.value = ''; dropdown.classList.add('hidden');
        });
    }
    const picker = document.getElementById('quotation-picker');
    if (!picker) return;
    const input = document.getElementById('quotation-search');
    const dropdown = document.getElementById('quotation-dropdown');
    const list = document.getElementById('quotation-selected');
    const error = document.getElementById('quotation-error');
    const form = picker.closest('form');
    let selectedOption = null;
    const items = [];
    function filter() {
        selectedOption = null;
        dropdown.querySelectorAll('[data-quotation-option]').forEach(option => { option.hidden = !(option.dataset.name + ' ' + (option.dataset.category || '')).toLocaleLowerCase().includes(input.value.toLocaleLowerCase()) || items.some(item => item.id === option.dataset.id); });
        dropdown.querySelectorAll('[data-quotation-group]').forEach(group => { group.hidden = !Array.from(group.querySelectorAll('[data-quotation-option]')).some(option => !option.hidden); });
        dropdown.classList.remove('hidden');
    }
    input.addEventListener('focus', filter);
    input.addEventListener('input', filter);
    document.addEventListener('click', event => {
        if (!picker.contains(event.target)) dropdown.classList.add('hidden');
    });
    dropdown.addEventListener('click', event => {
        const option = event.target.closest('[data-quotation-option]');
        if (!option) return;
        selectedOption = option.dataset; input.value = option.dataset.name; dropdown.classList.add('hidden');
    });
    function render() {
        list.replaceChildren();
        items.forEach((item, index) => {
            const row = document.createElement('div'); row.className = 'flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600';
            const info = document.createElement('div'); info.className = 'flex-1 min-w-0';
            const name = document.createElement('div'); name.className = 'font-medium dark:text-white'; name.textContent = `${item.name} (${item.unit})`; info.append(name);
            const id = document.createElement('input'); id.type = 'hidden'; id.name = `items[${index}].InsumoId`; id.value = item.id;
            const quantity = document.createElement('input'); quantity.type = 'number'; quantity.name = `items[${index}].Cantidad`; quantity.min = '0.0001'; quantity.step = 'any'; quantity.required = true; quantity.value = item.quantity; quantity.setAttribute('aria-label', `Cantidad de ${item.name}`); quantity.className = 'w-24 bg-gray-50 border border-gray-300 text-gray-900 text-sm rounded-lg p-2.5 dark:bg-gray-700 dark:text-white'; quantity.addEventListener('input', () => { item.quantity = quantity.value; });
            const quantityLabel = document.createElement('label'); quantityLabel.className = 'flex items-center gap-2 text-sm'; quantityLabel.textContent = 'Cantidad:'; quantityLabel.append(quantity);
            const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'w-8 h-8 rounded-full text-red-600'; remove.setAttribute('aria-label', `Quitar ${item.name}`); removeIcon(remove); remove.addEventListener('click', () => { items.splice(index, 1); render(); });
            row.append(info, id, quantityLabel, remove); list.append(row);
        });
    }
    document.getElementById('quotation-add').addEventListener('click', () => {
        if (!selectedOption || items.some(item => item.id === selectedOption.id)) { error.textContent = 'Seleccione un insumo del listado.'; return; }
        items.push({ id: selectedOption.id, name: selectedOption.name, unit: selectedOption.unit, quantity: '' }); selectedOption = null; input.value = ''; error.textContent = ''; dropdown.classList.add('hidden'); render();
    });
    document.querySelector('[data-quotation-reset]')?.addEventListener('click', () => {
        form.reset(); items.length = 0; selectedOption = null; error.textContent = ''; dropdown.classList.add('hidden'); render();
    });
    form.addEventListener('submit', event => {
        if (!items.length || !form.querySelector('input[name="proveedorIds"]')) { event.preventDefault(); error.textContent = 'Seleccione al menos un proveedor y un insumo.'; return; }
        if (items.some(item => !item.quantity.trim() || !Number.isFinite(Number(item.quantity)) || Number(item.quantity) < 0.0001)) { event.preventDefault(); error.textContent = 'Ingrese una cantidad válida para cada insumo.'; }
    });
})();
