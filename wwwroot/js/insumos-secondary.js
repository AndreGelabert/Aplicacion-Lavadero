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
    document.addEventListener('click', event => {
        const open = event.target.closest('[data-open-modal]');
        if (open) openModal(document.getElementById(open.dataset.openModal), open);
        const close = event.target.closest('[data-close-modal]');
        if (close) closeModal(close.closest('[role="dialog"]'));
        const edit = event.target.closest('[data-provider-edit]');
        if (edit) {
            const form = document.getElementById('provider-form');
            for (const [name, value] of Object.entries({ Id: edit.dataset.id, IdentificacionTributaria: edit.dataset.tax, RazonSocial: edit.dataset.name, Contacto: edit.dataset.contact, EmailOrdenes: edit.dataset.email, crear: 'false' })) form.elements.namedItem(name).value = value || '';
            form.elements.namedItem('IdentificacionTributaria').readOnly = true;
            document.getElementById('provider-title').textContent = 'Editando un Proveedor';
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
            document.getElementById('provider-title').textContent = form.dataset.canCreate === 'true' ? 'Registrando un Proveedor' : 'Editar Proveedor';
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
            document.getElementById('provider-confirm-submit').textContent = action;
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
        const controls = document.createElement('div'); controls.className = 'inline-flex items-stretch';
        const previous = document.createElement('button'); previous.type = 'button'; previous.textContent = '‹'; previous.setAttribute('aria-label', 'Anterior');
        const current = document.createElement('span');
        const next = document.createElement('button'); next.type = 'button'; next.textContent = '›'; next.setAttribute('aria-label', 'Siguiente');
        for (const element of [previous, current, next]) element.className = 'py-2 px-3 border border-gray-300 bg-white text-gray-700 dark:bg-gray-800 dark:text-white';
        controls.append(previous, current, next); nav.append(label, controls); table.parentElement.after(nav);
        function renderPage() {
            rows.forEach((row, index) => { row.hidden = index < (page - 1) * pageSize || index >= page * pageSize; });
            label.textContent = `Mostrando página ${page} de ${pages}`; current.textContent = page;
            previous.disabled = page === 1; next.disabled = page === pages;
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
        dropdown.querySelectorAll('[data-quotation-option]').forEach(option => { option.hidden = !option.dataset.name.toLocaleLowerCase().includes(input.value.toLocaleLowerCase()) || items.some(item => item.id === option.dataset.id); });
        dropdown.classList.remove('hidden');
    }
    input.addEventListener('focus', filter);
    input.addEventListener('input', filter);
    dropdown.addEventListener('click', event => {
        const option = event.target.closest('[data-quotation-option]');
        if (!option) return;
        selectedOption = option.dataset; input.value = option.dataset.name; dropdown.classList.add('hidden');
    });
    function render() {
        list.replaceChildren();
        items.forEach((item, index) => {
            const row = document.createElement('div'); row.className = 'flex items-center gap-3 p-2 bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-600';
            const number = document.createElement('span'); number.className = 'w-8 h-8 flex items-center justify-center bg-blue-100 text-blue-800 rounded-full font-bold'; number.textContent = index + 1;
            const info = document.createElement('div'); info.className = 'flex-1 min-w-0';
            const name = document.createElement('div'); name.className = 'font-medium dark:text-white'; name.textContent = item.name;
            const unit = document.createElement('div'); unit.className = 'text-sm text-gray-500'; unit.textContent = item.unit; info.append(name, unit);
            const id = document.createElement('input'); id.type = 'hidden'; id.name = `items[${index}].InsumoId`; id.value = item.id;
            const quantity = document.createElement('input'); quantity.type = 'number'; quantity.name = `items[${index}].Cantidad`; quantity.min = '0.0001'; quantity.step = 'any'; quantity.required = true; quantity.value = item.quantity; quantity.setAttribute('aria-label', `Cantidad de ${item.name}`); quantity.className = 'w-24 rounded-lg border p-2 dark:bg-gray-700'; quantity.addEventListener('input', () => { item.quantity = quantity.value; });
            const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'w-8 h-8 rounded-full text-red-600'; remove.setAttribute('aria-label', `Quitar ${item.name}`); remove.textContent = '✕'; remove.addEventListener('click', () => { items.splice(index, 1); render(); });
            row.append(number, info, id, quantity, remove); list.append(row);
        });
    }
    document.getElementById('quotation-add').addEventListener('click', () => {
        if (!selectedOption || items.some(item => item.id === selectedOption.id)) { error.textContent = 'Seleccione un insumo del listado.'; return; }
        items.push({ id: selectedOption.id, name: selectedOption.name, unit: selectedOption.unit, quantity: '' }); selectedOption = null; input.value = ''; error.textContent = ''; dropdown.classList.add('hidden'); render();
    });
    form.addEventListener('submit', event => {
        if (!items.length || !form.querySelector('input[name="proveedorIds"]:checked')) { event.preventDefault(); error.textContent = 'Seleccione al menos un proveedor y un insumo.'; return; }
        if (items.some(item => !item.quantity.trim() || !Number.isFinite(Number(item.quantity)) || Number(item.quantity) < 0.0001)) { event.preventDefault(); error.textContent = 'Ingrese una cantidad válida para cada insumo.'; }
    });
})();
